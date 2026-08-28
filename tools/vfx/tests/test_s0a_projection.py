from __future__ import annotations

import copy
import hashlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import jsonschema
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from vfx.s0a_calibration import CalibrationValidationError, normalized_sha256, write_fixture_set  # noqa: E402
from vfx.s0a_projection import (  # noqa: E402
    CAPTURE_TOOL_IDENTITY_PATH,
    CAPTURE_TOOL_VERSION,
    FRAME_TABLE,
    PROJECTION_TOOL_SOURCE_SCHEMA,
    ProjectionInputs,
    _capture_tool_identity,
    _common_profile,
    _derive_robustness_seeds,
    _load_json,
    _opaque_requirement_id,
    _projection_tool_identity,
    _secure_existing_directory,
    _validate_no_answer,
    _validate_png,
    _validated_reduced_metrics_hash,
    project_formal_capture,
    verify_projection,
)


ID_SALT = "projection-test-operator-salt-0123456789"


def _png_bytes(size: tuple[int, int], color: tuple[int, int, int, int]) -> bytes:
    stream = io.BytesIO()
    Image.new("RGBA", size, color).save(stream, format="PNG")
    return stream.getvalue()


PNG_BYTES = _png_bytes((960, 540), (20, 40, 80, 255))
EFFECT_PNG_BYTES = _png_bytes((960, 540), (120, 40, 10, 64))
HASH = "sha256:" + "1" * 64
TRUSTED_CAPTURE_TOOL_HASH = _capture_tool_identity()[1]


def _hash_bytes(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def _hash_file(path: Path) -> str:
    return _hash_bytes(path.read_bytes())


def _hash_text(value: str) -> str:
    return _hash_bytes(value.encode("utf-8"))


def _compact(document: dict) -> str:
    return json.dumps(document, ensure_ascii=False, separators=(",", ":"))


def _write(path: Path, value: bytes | str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(value, bytes):
        path.write_bytes(value)
    else:
        path.write_text(value, encoding="utf-8", newline="\n")


def _write_json(path: Path, document: dict, *, compact: bool = False) -> None:
    _write(path, _compact(document) if compact else json.dumps(document, indent=2) + "\n")


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _rehash(document: dict, field: str) -> None:
    document[field] = normalized_sha256(document, (field,))


def _ledger_entry(sequence: int, kind: str, details: dict, previous: str | None) -> dict:
    entry = {
        "schema": "w24-s0a-fixture-ledger/v2",
        "sequence": sequence,
        "kind": kind,
        "details": details,
        "recordedUtc": "2026-08-25T00:00:00.0000000Z",
        "previousEntryHash": previous,
    }
    _rehash(entry, "entryHash")
    return entry


def _source_hashes(*, fake_capture_tool: bool = False) -> dict:
    return {
        "scene": {"path": "Assets/VFX/Preview/SustainedFlame3D_Preview.unity", "sha256": "sha256:" + "2" * 64},
        "prefab": {"path": "Assets/VFX/Generated/Aura/VFX_sustained_flame_3d.prefab", "guid": "a" * 32, "sha256": "sha256:" + "3" * 64},
        "manifest": {"path": "Assets/VFX/Generated/Aura/BuildManifest.json", "sha256": "sha256:" + "4" * 64, "buildHash": "sha256:" + "5" * 64},
        "captureTool": {
            "path": "fake-test-tool" if fake_capture_tool else CAPTURE_TOOL_IDENTITY_PATH,
            "version": "fake/0" if fake_capture_tool else CAPTURE_TOOL_VERSION,
            "sha256": "sha256:" + "6" * 64 if fake_capture_tool else TRUSTED_CAPTURE_TOOL_HASH,
        },
    }


def _profile(fixed_seed: int, *, graphics_api: str = "Direct3D11") -> dict:
    robustness = _derive_robustness_seeds(fixed_seed)
    return {
        "profileVersion": "w24-s0a-formal-calibration-capture-profile/v1",
        "unityVersion": "2022.3.62f3",
        "urpVersion": "14.0.12",
        "graphicsApi": graphics_api,
        "graphicsDevice": "Synthetic",
        "graphicsDriverVersion": "test",
        "renderTextureFormat": "ARGB32",
        "rendererAsset": {"reference": "renderer", "sha256": "sha256:" + "7" * 64},
        "volume": {"reference": "volume", "sha256": "sha256:" + "8" * 64},
        "scenePath": "Assets/VFX/Preview/SustainedFlame3D_Preview.unity",
        "serializedCameraReference": "Assets/VFX/Preview/SustainedFlame3D_Preview.unity#MainCamera",
        "resolution": [960, 540],
        "fps": 60,
        "background": [0.035, 0.04, 0.055, 1],
        "colorSpace": "Linear",
        "hdr": False,
        "msaa": False,
        "bloom": {"value": False, "validation": "caller-frozen"},
        "toneMapping": {"value": "None", "validation": "caller-frozen"},
        "canonicalSeed": fixed_seed,
        "robustnessSeeds": list(robustness),
        "retainedFrameIndices": list(FRAME_TABLE),
        "retainedFrameIndicesSha256": _hash_text(",".join(str(value) for value in FRAME_TABLE)),
    }


def _build_raw_capture(
    root: Path,
    sample_id: str,
    fixed_seed: int,
    command_hash: str,
    *,
    profile_graphics_api: str = "Direct3D11",
    fake_capture_tool: bool = False,
) -> tuple[dict, str]:
    profile = _profile(fixed_seed, graphics_api=profile_graphics_api)
    profile_hash = _hash_text(_compact(profile))
    _write_json(root / "evidence-lock.json", {"schema": "w24-s0a-evidence-lock/v1", "candidateId": sample_id, "captureProfileSha256": profile_hash}, compact=True)
    diagnostic_manifest = {
        "schema": "w24-s0a-diagnostic-pass-manifest/v1",
        "passes": [
            {
                "passId": "effect-only-rgba",
                "encoding": "rgba8_png",
                "purpose": "minimal effect-only coverage input for machine measurement; not a Beauty frame or an aesthetic conclusion",
                "camera": "same serialized authority Camera",
                "clear": "transparent black",
                "cullingMask": 1,
                "format": "RGBA32 PNG",
            }
        ],
    }
    _write_json(root / "diagnostic-pass-manifest.json", diagnostic_manifest, compact=True)
    frames: list[dict] = []
    telemetry_frames: list[dict] = []
    seeds = (fixed_seed, *_derive_robustness_seeds(fixed_seed))
    for seed in seeds:
        for frame_index in FRAME_TABLE:
            beauty = f"frames/seed_{seed}/frame_{frame_index:05d}_beauty.png"
            effect = f"frames/seed_{seed}/frame_{frame_index:05d}_effect-only.png"
            _write(root / beauty, PNG_BYTES)
            _write(root / effect, EFFECT_PNG_BYTES)
            frames.append(
                {
                    "frameIndex": frame_index,
                    "simulationTime": frame_index / 60.0,
                    "state": "steady" if frame_index < 300 else "stop",
                    "seed": seed,
                    # Exact W24ContinuousCaptureRecorder 1.1.3 shapes. Beauty and
                    # effect-only diagnostics intentionally do not share a generic
                    # artifact envelope.
                    "beauty": {"file": beauty, "sha256": _hash_file(root / beauty)},
                    "diagnostics": [
                        {
                            "passId": "effect-only-rgba",
                            "file": effect,
                            "sha256": _hash_file(root / effect),
                            "foregroundPixels": 1,
                            "method": "same-serialized-camera; transparent clear; frozen effect LayerMask; RGB-or-alpha nonzero foreground",
                        }
                    ],
                }
            )
            telemetry_frames.append(
                {
                    "frameIndex": frame_index,
                    "state": "steady" if frame_index < 300 else "stop",
                    "seed": seed,
                    "liveParticleCount": 1 if frame_index < 300 else 0,
                    "enabledRendererCount": 1 if frame_index < 300 else 0,
                    "enabledLightCount": 1 if frame_index < 300 else 0,
                    "transitionSerial": 1,
                    "cleanupComplete": frame_index >= 300,
                }
            )
    _write_json(
        root / "diagnostics" / "semantic-telemetry.json",
        {"schema": "w24-s0a-semantic-telemetry/v1", "sampleId": sample_id, "fixedSeed": fixed_seed, "frames": telemetry_frames},
        compact=True,
    )
    diagnostic_manifest_hash = _hash_file(root / "diagnostic-pass-manifest.json")
    semantic_hash = _hash_file(root / "diagnostics" / "semantic-telemetry.json")
    metadata = {
        "schema": "w24-s0a-capture-evidence/v1",
        "candidateId": sample_id,
        "captureModePolicy": "graphics-device batchmode required; -nographics prohibited; synchronized ReadPixels",
        "executedInBatchMode": True,
        "frameRetentionPolicy": "retained-keyframes-only; CaptureFrame may only be called from the frozen retainedFrameIndices table; full-rate raw frames are not formal evidence",
        "retainedFrameIndices": list(FRAME_TABLE),
        "retainedFrameIndicesSha256": profile["retainedFrameIndicesSha256"],
        "formalPlayerLoop": {"observedSerial": 1080, "consumedSerial": 1080, "allObservedFramesConsumed": True},
        "captureProfile": profile,
        "captureProfileSha256": profile_hash,
        "sourceHashes": _source_hashes(fake_capture_tool=fake_capture_tool),
        "diagnosticPassManifest": {"file": "diagnostic-pass-manifest.json", "sha256": diagnostic_manifest_hash},
        "typedRawDiagnostics": [],
        "metricInputs": [],
        "metricReports": [],
        "semanticTelemetry": [
            {
                "kind": "semantic-telemetry",
                "description": "Natural PlayerLoop state and component telemetry for this single operator-command clone.",
                "file": "diagnostics/semantic-telemetry.json",
                "sha256": semantic_hash,
            }
        ],
        "supplementalDiagnostics": [],
        "frames": frames,
    }
    _write_json(root / "capture-metadata.json", metadata, compact=True)
    artifacts = []
    for path in sorted((path for path in root.rglob("*") if path.is_file()), key=lambda value: value.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        artifacts.append({"file": relative, "sha256": _hash_file(path)})
    source_hashes = metadata["sourceHashes"]
    seal = {
        "schema": "w24-s0a-final-evidence-seal/v1",
        "candidateId": sample_id,
        "captureProfileSha256": profile_hash,
        "artifacts": artifacts,
        "provenance": {
            "operatorCommandHash": command_hash,
            "captureToolSha256": source_hashes["captureTool"]["sha256"],
            "sourceHashesSha256": _hash_text(_compact(source_hashes)),
            "captureMetadataSha256": _hash_file(root / "capture-metadata.json"),
        },
    }
    seal["sealHash"] = _hash_text(_compact(seal))
    _write_json(root / "evidence-seal.json", seal, compact=True)
    return metadata, _hash_file(root / "evidence-seal.json")


def _build_candidate(
    candidate: Path,
    sample: dict,
    command_hash: str,
    *,
    profile_graphics_api: str = "Direct3D11",
    fake_capture_tool: bool = False,
) -> None:
    raw = candidate / "capture"
    _metadata, capture_seal_hash = _build_raw_capture(
        raw,
        sample["sampleId"],
        sample["fixedSeed"],
        command_hash,
        profile_graphics_api=profile_graphics_api,
        fake_capture_tool=fake_capture_tool,
    )
    invalid = sample["kind"] == "invalid"
    kinds = ["created"]
    if sample["injection"]["mutationCommands"]:
        kinds.append("queued-invalid-evidence" if invalid else "visual-mutation-applied")
    kinds.extend(["capture-begun", "seed-started", "seed-stop-requested", "seed-started", "seed-stop-requested", "seed-started", "seed-stop-requested", "raw-capture-sealed"])
    invalid_manifest_hash = None
    if invalid:
        derived = candidate / "invalid-evidence"
        shutil.copytree(raw, derived)
        command = sample["injection"]["mutationCommands"][0]
        if command["value"] == "missing_key_frame":
            first = sorted(derived.rglob("*_beauty.png"), key=lambda value: value.as_posix())[0]
            first.unlink()
            kind, derivation = "missing_key_frame", "deleted-beauty-frame"
        else:
            with (derived / "capture-metadata.json").open("ab") as stream:
                stream.write(b"\n")
            kind, derivation = "sha256_mismatch", "metadata-hash-mismatch"
        manifest = {
            "schema": "w24-s0a-derived-invalid-evidence/v1",
            "commandHash": command_hash,
            "sourceCaptureSealHash": capture_seal_hash,
            "kind": kind,
            "derivation": derivation,
        }
        _rehash(manifest, "derivedManifestHash")
        invalid_manifest_hash = manifest["derivedManifestHash"]
        _write_json(derived / "invalid-evidence-manifest.json", manifest, compact=True)
        kinds.append("invalid-evidence-injected")
    kinds.extend(["candidate-finalized", "cleanup"])
    prior = None
    for sequence, kind in enumerate(kinds):
        details = {"sampleId": sample["sampleId"], "commandHash": command_hash} if sequence == 0 else {"test": True}
        entry = _ledger_entry(sequence, kind, details, prior)
        _write_json(candidate / "ledger" / f"{sequence:04d}-{kind}.json", entry, compact=True)
        prior = entry["entryHash"]
    completion = {
        "schema": "w24-s0a-candidate-completion/v1",
        "sampleId": sample["sampleId"],
        "commandHash": command_hash,
        "captureSealHash": capture_seal_hash,
        "invalidEvidenceManifestHash": invalid_manifest_hash,
        "ledgerTailHash": prior,
    }
    _rehash(completion, "completionHash")
    _write_json(candidate / "candidate-completion.json", completion, compact=True)


def _build_formal_cohort(root: Path, *, profile_mismatch: bool = False, fake_capture_tool: bool = False) -> tuple[Path, Path]:
    fixture = root / "fixtures" / "reduced"
    write_fixture_set(fixture, "reduced", ID_SALT)
    ledger = _load(fixture / "operator" / "generation-ledger.json")
    command_set = _load(fixture / "operator" / "command-set.json")
    command_hashes = {item["sampleId"]: item["commandHash"] for item in command_set["commands"]}
    capture = root / "capture"
    for index, sample in enumerate(sorted(ledger["samples"], key=lambda item: item["sampleId"])):
        graphics_api = "Vulkan" if profile_mismatch and index == 1 else "Direct3D11"
        _build_candidate(
            capture / sample["sampleId"],
            sample,
            command_hashes[sample["sampleId"]],
            profile_graphics_api=graphics_api,
            fake_capture_tool=fake_capture_tool and index == 1,
        )
    return fixture, capture


def _inputs(root: Path, fixture: Path, capture: Path, output: Path) -> ProjectionInputs:
    qa = Path(__file__).resolve().parents[3] / "docs" / "skills" / "unity-vfx-visual-qa"
    return ProjectionInputs(
        capture_root=capture,
        fixture_root=fixture,
        output=output,
        qa_prompt=qa / "AGENT.md",
        image_input_strategy=qa / "review-protocol.md",
        three_state_rules=qa / "review-protocol.md",
        aggregation_rules=qa / "review-protocol.md",
        visual_review_schema=qa / "schemas" / "vfx-visual-review.schema.json",
        model_version_id="test-visual-model-immutable-id",
    )


class S0aProjectionTests(unittest.TestCase):
    def test_exact_cohort_projects_deterministically_without_answer_or_authority_leaks(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            first = root / "first" / "artifacts"
            second = root / "second" / "artifacts"
            receipt_a = project_formal_capture(_inputs(root, fixture, capture, first))
            receipt_b = project_formal_capture(_inputs(root, fixture, capture, second))
            self.assertEqual(receipt_a, receipt_b)
            self.assertEqual(verify_projection(first), receipt_a)
            blind_text = "\n".join(path.read_text(encoding="utf-8") for path in (first / "blind").rglob("*.json")).lower()
            for forbidden in ("labelblueprint", "mutationcommands", "targetkey", "baseline_control", "metadata_integrity", "visual_fail", "evidence_invalid"):
                self.assertNotIn(forbidden, blind_text)
            manifest = _load(first / "blind" / "blind-submission-manifest.json")
            policy = _load(first / "operator" / "capture-profile-policy.json")
            self.assertEqual(len(manifest["samples"]), 66)
            self.assertEqual(len(policy["instances"]), 66)
            self.assertEqual(len({item["captureProfileInstanceHash"] for item in policy["instances"]}), 66)
            self.assertTrue(all(_opaque_requirement_id(item["sampleId"]) == item["designRequirementIds"][0] for item in manifest["samples"]))
            worksheet = _load(first / "operator" / "human-adjudication-worksheet.json")
            self.assertTrue(all(row["groundTruthRoute"] is None and row["reviewer"] is None and row["perRequirement"][0]["state"] is None for row in worksheet["rows"]))
            self.assertEqual(receipt_a["status"], "PROJECTED_NOT_REVIEWED")
            self.assertEqual(receipt_a["assertions"]["terminalStatusClaimed"], False)

    def test_partial_cohort_is_rejected_without_partial_projection(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            shutil.rmtree(next(iter(capture.iterdir())))
            output = root / "result" / "artifacts"
            with self.assertRaisesRegex(CalibrationValidationError, "exactly"):
                project_formal_capture(_inputs(root, fixture, capture, output))
            self.assertFalse(output.exists())

    def test_foreign_capture_entry_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            _write(capture / "foreign.txt", "no")
            with self.assertRaises(CalibrationValidationError):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_reparse_or_symlink_input_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            with mock.patch("vfx.s0a_projection._is_link", side_effect=lambda path: path == capture):
                with self.assertRaisesRegex(CalibrationValidationError, "symlink|linked"):
                    project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_root_symlink_and_windows_junction_are_rejected_before_resolution(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            real = root / "real"
            real.mkdir()
            checked = 0
            symlink = root / "symlink-root"
            try:
                os.symlink(real, symlink, target_is_directory=True)
            except OSError:
                pass
            else:
                checked += 1
                with self.assertRaisesRegex(CalibrationValidationError, "symlink|junction|reparse"):
                    _secure_existing_directory(symlink, "test root")
                symlink.unlink()
            if os.name == "nt":
                junction = root / "junction-root"
                created = subprocess.run(
                    ["cmd", "/c", "mklink", "/J", str(junction), str(real)],
                    capture_output=True,
                    text=True,
                    check=False,
                )
                if created.returncode == 0:
                    checked += 1
                    try:
                        with self.assertRaisesRegex(CalibrationValidationError, "symlink|junction|reparse"):
                            _secure_existing_directory(junction, "test root")
                    finally:
                        os.rmdir(junction)
            if checked == 0:
                self.skipTest("platform did not permit creating a symlink or junction")

    def test_missing_ordinary_frame_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            ledger = _load(fixture / "operator" / "generation-ledger.json")
            ordinary = next(sample for sample in ledger["samples"] if sample["kind"] != "invalid")
            frame = sorted((capture / ordinary["sampleId"] / "capture").rglob("*_beauty.png"))[0]
            frame.unlink()
            with self.assertRaisesRegex(CalibrationValidationError, "missing"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_raw_capture_hash_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            target = sorted(capture.rglob("*_effect-only.png"))[0]
            with target.open("ab") as stream:
                stream.write(b"tamper")
            with self.assertRaisesRegex(CalibrationValidationError, "hash mismatch"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_profile_policy_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root, profile_mismatch=True)
            with self.assertRaisesRegex(CalibrationValidationError, "outside the frozen per-sample seed"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_missing_frame_derivation_rejects_tampered_non_target_beauty(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            ledger = _load(fixture / "operator" / "generation-ledger.json")
            sample = next(
                item
                for item in ledger["samples"]
                if item["kind"] == "invalid" and item["injection"]["mutationCommands"][0]["value"] == "missing_key_frame"
            )
            target = sorted((capture / sample["sampleId"] / "invalid-evidence").rglob("*_beauty.png"))[0]
            with target.open("ab") as stream:
                stream.write(b"tampered-non-target-beauty")
            with self.assertRaisesRegex(CalibrationValidationError, "altered a non-target"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_invalid_derivation_must_match_operator_command_value(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            ledger = _load(fixture / "operator" / "generation-ledger.json")
            sample = next(
                item
                for item in ledger["samples"]
                if item["kind"] == "invalid" and item["injection"]["mutationCommands"][0]["value"] == "sha256_mismatch"
            )
            candidate = capture / sample["sampleId"]
            derived = candidate / "invalid-evidence"
            shutil.rmtree(derived)
            shutil.copytree(candidate / "capture", derived)
            sorted(derived.rglob("*_beauty.png"))[0].unlink()
            command_set = _load(fixture / "operator" / "command-set.json")
            command_hash = next(item["commandHash"] for item in command_set["commands"] if item["sampleId"] == sample["sampleId"])
            manifest = {
                "schema": "w24-s0a-derived-invalid-evidence/v1",
                "commandHash": command_hash,
                "sourceCaptureSealHash": _hash_file(candidate / "capture" / "evidence-seal.json"),
                "kind": "missing_key_frame",
                "derivation": "deleted-beauty-frame",
            }
            _rehash(manifest, "derivedManifestHash")
            _write_json(derived / "invalid-evidence-manifest.json", manifest, compact=True)
            completion = _load(candidate / "candidate-completion.json")
            completion["invalidEvidenceManifestHash"] = manifest["derivedManifestHash"]
            _rehash(completion, "completionHash")
            _write_json(candidate / "candidate-completion.json", completion, compact=True)
            with self.assertRaisesRegex(CalibrationValidationError, "operator command"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_fake_capture_tool_path_version_hash_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root, fake_capture_tool=True)
            with self.assertRaisesRegex(CalibrationValidationError, "source/tool identities"):
                project_formal_capture(_inputs(root, fixture, capture, root / "result" / "artifacts"))

    def test_projected_receipt_detects_current_projection_source_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            output = root / "result" / "artifacts"
            project_formal_capture(_inputs(root, fixture, capture, output))
            sources, dependencies, _source_hash = _projection_tool_identity()
            tampered = copy.deepcopy(sources)
            tampered[0]["sha256"] = "sha256:" + "0" * 64
            with mock.patch(
                "vfx.s0a_projection._projection_tool_identity",
                return_value=(
                    tampered,
                    dependencies,
                    normalized_sha256(
                        {
                            "schemaVersion": PROJECTION_TOOL_SOURCE_SCHEMA,
                            "sources": tampered,
                            "runtimeDependencies": dependencies,
                        }
                    ),
                ),
            ):
                with self.assertRaisesRegex(CalibrationValidationError, "source set/hash has drifted"):
                    verify_projection(output)
            drifted_dependencies = [{"name": "Pillow", "version": "0.0.0-tampered"}]
            with mock.patch(
                "vfx.s0a_projection._projection_tool_identity",
                return_value=(
                    sources,
                    drifted_dependencies,
                    normalized_sha256(
                        {
                            "schemaVersion": PROJECTION_TOOL_SOURCE_SCHEMA,
                            "sources": sources,
                            "runtimeDependencies": drifted_dependencies,
                        }
                    ),
                ),
            ):
                with self.assertRaisesRegex(CalibrationValidationError, "source set/hash has drifted"):
                    verify_projection(output)

    def test_projected_receipt_detects_projection_schema_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            output = root / "result" / "artifacts"
            project_formal_capture(_inputs(root, fixture, capture, output))
            sources, dependencies, _source_hash = _projection_tool_identity()
            tampered = copy.deepcopy(sources)
            schema_source = next(item for item in tampered if item["path"].endswith(".schema.json"))
            schema_source["sha256"] = "sha256:" + "0" * 64
            with mock.patch(
                "vfx.s0a_projection._projection_tool_identity",
                return_value=(
                    tampered,
                    dependencies,
                    normalized_sha256(
                        {
                            "schemaVersion": PROJECTION_TOOL_SOURCE_SCHEMA,
                            "sources": tampered,
                            "runtimeDependencies": dependencies,
                        }
                    ),
                ),
            ):
                with self.assertRaisesRegex(CalibrationValidationError, "source set/hash has drifted"):
                    verify_projection(output)

    def test_truncated_png_is_rejected_even_with_a_self_consistent_hash(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "truncated.png"
            # Keep the signature/IHDR and part of IDAT so this exercises full
            # chunk/decode validation rather than only signature recognition.
            _write(path, PNG_BYTES[: max(40, len(PNG_BYTES) // 2)])
            with self.assertRaisesRegex(CalibrationValidationError, "truncated|decode|PNG"):
                _validate_png(path, _hash_file(path))

    def test_wrong_size_png_is_rejected_even_when_fully_decodable(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "wrong-size.png"
            _write(path, _png_bytes((959, 540), (20, 40, 80, 255)))
            with self.assertRaisesRegex(CalibrationValidationError, "960x540"):
                _validate_png(path, _hash_file(path))

    def test_strict_json_rejects_duplicates_nonfinite_and_lone_surrogates(self) -> None:
        cases = (
            '{"value":1,"value":2}',
            '{"value":NaN}',
            '{"value":Infinity}',
            '{"value":1e999}',
            '{"value":"\\ud800"}',
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for index, raw in enumerate(cases):
                path = root / f"invalid-{index}.json"
                _write(path, raw)
                with self.assertRaisesRegex(CalibrationValidationError, "strict JSON"):
                    _load_json(path, "negative JSON fixture")

    def test_expanded_full_reduced_metrics_input_is_verified_not_file_hashed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "arbitrary.json"
            _write(path, '{"reportHash":"sha256:' + "0" * 64 + '"}')
            with self.assertRaisesRegex(CalibrationValidationError, "Reduced metrics verification failed"):
                _validated_reduced_metrics_hash(path)

    def test_assignment_leaks_are_rejected(self) -> None:
        for leaked in (
            {"labelBlueprint": {}},
            {"criterion": "baseline_control"},
            {"designRequirementIds": ["flame.capture.metadata_integrity"]},
            {"groundTruthRoute": "VISUAL_FAIL"},
        ):
            with self.assertRaisesRegex(CalibrationValidationError, "leak"):
                _validate_no_answer(leaked)

    def test_projection_schemas_are_strict_and_accept_generated_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture, capture = _build_formal_cohort(root)
            output = root / "result" / "artifacts"
            project_formal_capture(_inputs(root, fixture, capture, output))
            schemas = Path(__file__).resolve().parents[3] / "docs" / "skills" / "unity-vfx-visual-qa" / "calibration"
            pairs = [
                ("s0a-capture-profile-policy.schema.json", output / "operator" / "capture-profile-policy.json"),
                ("s0a-blind-review-contract.schema.json", output / "blind" / "review-contract.json"),
                ("s0a-projected-blind-manifest.schema.json", output / "blind" / "blind-submission-manifest.json"),
                ("s0a-projected-review-freeze-context.schema.json", output / "blind" / "review-freeze-context.json"),
                ("s0a-projected-operator-evidence-bundle.schema.json", output / "operator" / "evidence-bundle.json"),
                ("s0a-human-adjudication-worksheet.schema.json", output / "operator" / "human-adjudication-worksheet.json"),
                ("s0a-projection-receipt.schema.json", output / "projection-receipt.json"),
            ]
            for schema_name, artifact in pairs:
                schema = _load(schemas / schema_name)
                jsonschema.Draft202012Validator.check_schema(schema)
                jsonschema.validate(_load(artifact), schema)
            evidence_schema = _load(schemas / "s0a-projected-blind-evidence.schema.json")
            jsonschema.Draft202012Validator.check_schema(evidence_schema)
            for artifact in (output / "blind" / "evidence").glob("*.json"):
                jsonschema.validate(_load(artifact), evidence_schema)
            bad = _load(output / "blind" / "blind-submission-manifest.json")
            bad["unexpected"] = True
            with self.assertRaises(jsonschema.ValidationError):
                jsonschema.validate(bad, _load(schemas / "s0a-projected-blind-manifest.schema.json"))


if __name__ == "__main__":
    unittest.main()
