"""Deterministic W24 S0a formal-capture to blind-review projection.

The Unity fixture owns capture.  This module is a read-only verifier of that
write-once capture and a write-once projector into an ``artifacts`` directory.
It deliberately does not create labels, review answers, metrics, QA reports,
or an S0a terminal status.

The blind side receives only a no-answer contract, opaque per-sample design
requirement IDs, the fixed three-seed/eight-frame Beauty set, and integrity
metadata.  Mutation commands, label blueprints, error classes, strengths, and
expected routes remain operator-only and are never copied into the projection.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
import math
import os
import re
import shutil
import stat
import struct
import tempfile
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, UnidentifiedImageError, __version__ as PILLOW_VERSION

try:  # package import in tests and module import in compatibility CLIs
    from .s0a_calibration import (
        COHORTS,
        COHORT_IDS,
        CalibrationValidationError,
        normalized_json,
        normalized_sha256,
        validate_operator_ledger,
        verify_metrics,
    )
except ImportError:  # pragma: no cover - direct script execution
    from s0a_calibration import (
        COHORTS,
        COHORT_IDS,
        CalibrationValidationError,
        normalized_json,
        normalized_sha256,
        validate_operator_ledger,
        verify_metrics,
    )


PROJECTION_VERSION = "w24-s0a-blind-projection/1.2.0"
POLICY_SCHEMA = "s0a-capture-profile-policy/v1"
EVIDENCE_SCHEMA = "s0a-projected-blind-evidence/v1"
CONTRACT_SCHEMA = "s0a-no-answer-review-contract/v1"
BLIND_SCHEMA = "s0a-projected-blind-manifest/v1"
CONTEXT_SCHEMA = "s0a-projected-review-freeze-context/v1"
BUNDLE_SCHEMA = "s0a-projected-operator-evidence-bundle/v1"
WORKSHEET_SCHEMA = "s0a-human-adjudication-worksheet/v1"
RECEIPT_SCHEMA = "s0a-projection-receipt/v3"

REPO_ROOT = Path(__file__).absolute().parents[2]
CAPTURE_TOOL_VERSION = "w24-s0a-formal-calibration-capture/1.1.3"
CAPTURE_TOOL_RELATIVE_PATHS = (
    "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24CaptureProfile.cs",
    "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs",
    "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24EvidenceStore.cs",
    "Packages/com.vfxcomposer.unity/Editor/W24/S0a/W24S0aFixtureAdapter.cs",
    "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/W24S0aFormalCalibrationCaptureTests.cs",
    "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/VFXComposer.Tests.PlayMode.W24S0aFormal.asmdef",
    "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/W24S0aFormalPlayModeProxyTests.cs",
    "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/VFXComposer.Tests.PlayMode.W24S0aFormalRuntime.asmdef",
)
CAPTURE_TOOL_IDENTITY_PATH = ";".join(CAPTURE_TOOL_RELATIVE_PATHS)
PROJECTION_TOOL_SOURCE_SCHEMA = "s0a-projection-tool-sources/v2"
PROJECTION_TOOL_RELATIVE_PATHS = (
    "tools/vfx/project_s0a_capture.py",
    "tools/vfx/s0a_calibration.py",
    "tools/vfx/s0a_projection.py",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-blind-review-contract.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-capture-profile-policy.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-human-adjudication-worksheet.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-blind-evidence.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-blind-manifest.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-operator-evidence-bundle.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-review-freeze-context.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projection-receipt.schema.json",
)
FROZEN_PILLOW_VERSION = "12.2.0"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_DIMENSIONS = (960, 540)
_VALIDATED_PNG_HASHES: set[str] = set()

HASH_RE = re.compile(r"^sha256:[a-f0-9]{64}$")
SAMPLE_RE = re.compile(r"^s0a-[a-f0-9]{20}$")
REQ_RE = re.compile(r"^s0a\.visual\.req\.[a-f0-9]{20}$")
FRAME_TABLE = (1, 21, 60, 120, 180, 240, 300, 360)
COHORT_BY_ID = {value: key for key, value in COHORT_IDS.items()}
PROFILE_FIELDS = frozenset(
    {
        "profileVersion",
        "unityVersion",
        "urpVersion",
        "graphicsApi",
        "graphicsDevice",
        "graphicsDriverVersion",
        "renderTextureFormat",
        "rendererAsset",
        "volume",
        "scenePath",
        "serializedCameraReference",
        "resolution",
        "fps",
        "background",
        "colorSpace",
        "hdr",
        "msaa",
        "bloom",
        "toneMapping",
        "canonicalSeed",
        "robustnessSeeds",
        "retainedFrameIndices",
        "retainedFrameIndicesSha256",
    }
)

# Blind documents must not carry an answer key or an obvious route alias.
FORBIDDEN_BLIND_KEYS = frozenset(
    {
        "kind",
        "injection",
        "errorClass",
        "strength",
        "mutationCommands",
        "targetKey",
        "labelBlueprint",
        "groundTruthRoute",
        "expectedRoute",
        "expectedState",
    }
)
FORBIDDEN_BLIND_TOKENS = (
    "baseline_control",
    "baseline.required",
    "metadata_integrity",
    "evidence_metadata_mismatch",
    "visual_fail",
    "visual_pass",
    "evidence_invalid",
    "visual_uncertain",
)


@dataclass(frozen=True)
class ProjectionInputs:
    capture_root: Path
    fixture_root: Path
    output: Path
    qa_prompt: Path
    image_input_strategy: Path
    three_state_rules: Path
    aggregation_rules: Path
    visual_review_schema: Path
    model_version_id: str
    reduced_metrics: Path | None = None


@dataclass
class VerifiedSample:
    sample_id: str
    design_requirement_id: str
    command_hash: str
    completion_hash: str
    raw_capture_seal_file_hash: str
    selected_source: Path
    selected_metadata_bytes: bytes
    selected_metadata_declared_hash: str
    selected_metadata_actual_hash: str
    metadata: dict[str, Any]
    profile: dict[str, Any]
    profile_instance_hash: str
    fixed_seed: int
    seeds: tuple[int, int, int]
    source_hashes: dict[str, Any]
    frame_records: list[dict[str, Any]]
    invalid_derivation_hash: str | None
    ledger_tail_hash: str


def _sha256_bytes(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def _validate_png(path: Path, expected_hash: str | None = None) -> str:
    """Validate one immutable formal PNG by bytes, structure, and full decode.

    Hashing and decoding the same in-memory byte string prevents a file from
    changing between the integrity and image checks.  A successful content
    hash may be reused for byte-identical copies (raw, blind, and operator
    surfaces); the SHA-256 check is still performed for every path.
    """
    if not path.is_file() or _is_link(path):
        raise CalibrationValidationError(f"Formal PNG is missing or linked: {path}")
    try:
        encoded = path.read_bytes()
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot read formal PNG {path}: {exc}") from exc
    actual_hash = _sha256_bytes(encoded)
    if expected_hash is not None and actual_hash != expected_hash:
        raise CalibrationValidationError(f"Formal PNG hash mismatch: {path}")
    if actual_hash in _VALIDATED_PNG_HASHES:
        return actual_hash

    # PNG signature + the mandatory first IHDR chunk (length, type, payload,
    # and CRC) are checked independently of Pillow before decoding.
    if len(encoded) < 33 or encoded[:8] != PNG_SIGNATURE:
        raise CalibrationValidationError(f"Formal frame is not a complete PNG signature/IHDR stream: {path}")
    chunk_length = struct.unpack(">I", encoded[8:12])[0]
    chunk_type = encoded[12:16]
    if chunk_length != 13 or chunk_type != b"IHDR":
        raise CalibrationValidationError(f"Formal PNG does not begin with the canonical IHDR chunk: {path}")
    ihdr = encoded[16:29]
    declared_crc = struct.unpack(">I", encoded[29:33])[0]
    actual_crc = zlib.crc32(chunk_type + ihdr) & 0xFFFFFFFF
    if declared_crc != actual_crc:
        raise CalibrationValidationError(f"Formal PNG IHDR CRC is invalid: {path}")
    width, height = struct.unpack(">II", ihdr[:8])
    if (width, height) != PNG_DIMENSIONS:
        raise CalibrationValidationError(
            f"Formal PNG must be exactly {PNG_DIMENSIONS[0]}x{PNG_DIMENSIONS[1]}: {path} is {width}x{height}"
        )

    try:
        # verify() walks the complete chunk stream and checks checksums.  A
        # second open/load forces full pixel decode rather than header-only
        # identification.
        with Image.open(io.BytesIO(encoded)) as image:
            if image.format != "PNG" or image.size != PNG_DIMENSIONS:
                raise CalibrationValidationError(f"Formal frame is not a 960x540 PNG: {path}")
            image.verify()
        with Image.open(io.BytesIO(encoded)) as image:
            if image.format != "PNG" or image.size != PNG_DIMENSIONS:
                raise CalibrationValidationError(f"Formal frame is not a 960x540 PNG: {path}")
            image.load()
    except CalibrationValidationError:
        raise
    except (OSError, SyntaxError, ValueError, UnidentifiedImageError) as exc:
        raise CalibrationValidationError(f"Formal PNG is truncated or cannot be fully decoded: {path}: {exc}") from exc

    _VALIDATED_PNG_HASHES.add(actual_hash)
    return actual_hash


def _hash_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def _source_file_set(root: Path, relative_paths: Iterable[str], label: str) -> list[dict[str, str]]:
    safe_root = _secure_existing_directory(root, f"{label} root")
    records: list[dict[str, str]] = []
    for relative in sorted(relative_paths):
        if Path(relative).is_absolute() or ".." in Path(relative).parts or "\\" in relative:
            raise CalibrationValidationError(f"{label} contains an unsafe source path: {relative}")
        source = _secure_existing_file(safe_root / Path(relative), f"{label} source {relative}")
        records.append({"path": relative, "sha256": _sha256_file(source)})
    return records


def _capture_tool_identity() -> tuple[list[dict[str, str]], str]:
    sources = _source_file_set(REPO_ROOT / "project", CAPTURE_TOOL_RELATIVE_PATHS, "formal capture tool")
    identity = "\n".join(f"{item['path']}:{item['sha256'][7:]}" for item in sources)
    return sources, _hash_text(identity)


def _projection_runtime_dependencies() -> list[dict[str, str]]:
    if PILLOW_VERSION != FROZEN_PILLOW_VERSION:
        raise CalibrationValidationError(
            "Projection requires the project-frozen Pillow "
            f"{FROZEN_PILLOW_VERSION}; observed {PILLOW_VERSION}"
        )
    return [{"name": "Pillow", "version": PILLOW_VERSION}]


def _projection_tool_identity() -> tuple[list[dict[str, str]], list[dict[str, str]], str]:
    sources = _source_file_set(REPO_ROOT, PROJECTION_TOOL_RELATIVE_PATHS, "projection tool")
    dependencies = _projection_runtime_dependencies()
    source_hash = normalized_sha256(
        {
            "schemaVersion": PROJECTION_TOOL_SOURCE_SCHEMA,
            "sources": sources,
            "runtimeDependencies": dependencies,
        }
    )
    return sources, dependencies, source_hash


def _with_hash(document: dict[str, Any], field: str) -> dict[str, Any]:
    result = copy.deepcopy(document)
    result.pop(field, None)
    result[field] = normalized_sha256(result, (field,))
    return result


def _require_hash(value: Any, label: str) -> str:
    if not isinstance(value, str) or not HASH_RE.fullmatch(value):
        raise CalibrationValidationError(f"{label} must be sha256:<64 lowercase hex>")
    return value


def _require_exact(document: Any, fields: Iterable[str], label: str) -> dict[str, Any]:
    expected = set(fields)
    if not isinstance(document, dict) or set(document) != expected:
        raise CalibrationValidationError(f"{label} fields must be exactly {sorted(expected)}")
    return document


def _reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON member: {key}")
        result[key] = value
    return result


def _reject_nonfinite_constant(token: str) -> None:
    raise ValueError(f"non-finite JSON number: {token}")


def _validate_json_scalars(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            try:
                key.encode("utf-8", errors="strict")
            except UnicodeError as exc:
                raise ValueError(f"non-Unicode-scalar JSON key at {path}") from exc
            _validate_json_scalars(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _validate_json_scalars(child, f"{path}[{index}]")
    elif isinstance(value, str):
        try:
            value.encode("utf-8", errors="strict")
        except UnicodeError as exc:
            raise ValueError(f"lone surrogate/non-Unicode-scalar string at {path}") from exc
    elif isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"non-finite JSON number at {path}")


def _parse_json_text(raw: str, label: str) -> Any:
    try:
        document = json.loads(
            raw,
            object_pairs_hook=_reject_duplicate_pairs,
            parse_constant=_reject_nonfinite_constant,
        )
        _validate_json_scalars(document)
        return document
    except (UnicodeError, ValueError, json.JSONDecodeError) as exc:
        raise CalibrationValidationError(f"Invalid strict JSON in {label}: {exc}") from exc


def _load_json(path: Path, label: str) -> dict[str, Any]:
    try:
        raw = path.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as exc:
        raise CalibrationValidationError(f"Cannot read {label} {path}: {exc}") from exc
    document = _parse_json_text(raw, f"{label} {path}")
    if not isinstance(document, dict):
        raise CalibrationValidationError(f"{label} must be a JSON object: {path}")
    return document


def _canonical_hash_matches(document: dict[str, Any], field: str, label: str) -> str:
    value = _require_hash(document.get(field), f"{label}.{field}")
    expected = normalized_sha256(document, (field,))
    if value != expected:
        raise CalibrationValidationError(f"{label}.{field} does not bind canonical content")
    return value


def _ordered_compact_hash(document: dict[str, Any], omit: str) -> str:
    material = copy.deepcopy(document)
    material.pop(omit, None)
    return _hash_text(json.dumps(material, ensure_ascii=False, separators=(",", ":")))


def _is_link(path: Path) -> bool:
    try:
        info = path.lstat()
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot inspect path {path}: {exc}") from exc
    junction_probe = getattr(path, "is_junction", None)
    try:
        is_junction = bool(junction_probe()) if callable(junction_probe) else False
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot inspect junction identity {path}: {exc}") from exc
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    attributes = getattr(info, "st_file_attributes", 0)
    return path.is_symlink() or is_junction or stat.S_ISLNK(info.st_mode) or bool(attributes & reparse_flag)


def _lexical_absolute(path: Path) -> Path:
    if ".." in path.parts:
        raise CalibrationValidationError(f"Parent traversal is forbidden in input paths: {path}")
    return Path(os.path.abspath(os.fspath(path)))


def _reject_existing_path_chain(path: Path, label: str) -> Path:
    """Reject reparse traversal before any resolve/realpath operation can erase it."""
    absolute = _lexical_absolute(path)
    chain = [absolute, *absolute.parents]
    for component in reversed(chain):
        if not component.exists() and not component.is_symlink():
            continue
        if _is_link(component):
            raise CalibrationValidationError(f"{label} traverses a symlink/junction/reparse point: {component}")
    return absolute


def _secure_existing_directory(path: Path, label: str) -> Path:
    absolute = _reject_existing_path_chain(path, label)
    if not absolute.exists() or not absolute.is_dir() or _is_link(absolute):
        raise CalibrationValidationError(f"{label} is missing, wrong-kind, or linked: {absolute}")
    return absolute


def _secure_existing_file(path: Path, label: str) -> Path:
    absolute = _reject_existing_path_chain(path, label)
    if not absolute.exists() or not absolute.is_file() or _is_link(absolute):
        raise CalibrationValidationError(f"{label} must be a direct file: {absolute}")
    return absolute


def _secure_output_path(path: Path) -> Path:
    absolute = _reject_existing_path_chain(path, "projection output")
    if absolute.name != "artifacts":
        raise CalibrationValidationError("Projection output directory must be named exactly 'artifacts'")
    if absolute.exists():
        raise CalibrationValidationError(f"Projection output is write-once and already exists: {absolute}")
    parent = _secure_existing_directory(absolute.parent, "projection output parent") if absolute.parent.exists() else _reject_existing_path_chain(absolute.parent, "projection output parent")
    # A not-yet-existing parent may itself have an existing safe ancestor; it is
    # created only after the complete input cohort has passed validation.
    if parent.exists() and not parent.is_dir():
        raise CalibrationValidationError(f"Projection output parent is not a directory: {parent}")
    return absolute


def reject_links_and_reparse(root: Path) -> None:
    """Reject symlinks/junction-like entries before following any input tree."""
    root = _secure_existing_directory(root, "input directory")
    for parent, directories, files in os.walk(root, followlinks=False):
        safe_directories: list[str] = []
        for name in directories:
            entry = Path(parent) / name
            if _is_link(entry):
                raise CalibrationValidationError(f"Linked/reparse input is forbidden: {entry}")
            safe_directories.append(name)
        directories[:] = safe_directories
        for name in files:
            entry = Path(parent) / name
            if _is_link(entry):
                raise CalibrationValidationError(f"Linked/reparse input is forbidden: {entry}")


def _require_direct_shape(root: Path, files: set[str], directories: set[str], label: str) -> None:
    if not root.is_dir() or _is_link(root):
        raise CalibrationValidationError(f"{label} is missing, wrong-kind, or linked")
    actual_files: set[str] = set()
    actual_directories: set[str] = set()
    for entry in root.iterdir():
        if _is_link(entry):
            raise CalibrationValidationError(f"{label} contains a linked entry: {entry.name}")
        if entry.is_dir():
            actual_directories.add(entry.name)
        elif entry.is_file():
            actual_files.add(entry.name)
        else:
            raise CalibrationValidationError(f"{label} contains a foreign entry: {entry.name}")
    if actual_files != files or actual_directories != directories:
        raise CalibrationValidationError(
            f"{label} shape mismatch; files={sorted(actual_files)}, directories={sorted(actual_directories)}"
        )


def _derive_robustness_seeds(fixed_seed: int) -> tuple[int, int]:
    def derive(source: int, salt: int) -> int:
        value = (source ^ salt) & 0xFFFFFFFF
        return ((source + salt + 1) & 0xFFFFFFFF) if value in (0, source) else value

    first = derive(fixed_seed, 0x9E3779B9)
    second = derive(fixed_seed, 0x85EBCA6B)
    while second in (0, fixed_seed, first):
        second = derive((second + 1) & 0xFFFFFFFF, 0xC2B2AE35)
    return first, second


def _opaque_requirement_id(sample_id: str) -> str:
    digest = hashlib.sha256(f"W24-S0a|blind-requirement|v1|{sample_id}".encode("utf-8")).hexdigest()[:20]
    return f"s0a.visual.req.{digest}"


def _validate_no_answer(document: Any, path: str = "$") -> None:
    if isinstance(document, dict):
        for key, value in document.items():
            if key in FORBIDDEN_BLIND_KEYS:
                raise CalibrationValidationError(f"Blind assignment leak at {path}.{key}")
            _validate_no_answer(value, f"{path}.{key}")
    elif isinstance(document, list):
        for index, value in enumerate(document):
            _validate_no_answer(value, f"{path}[{index}]")
    elif isinstance(document, str):
        lowered = document.lower()
        if any(token in lowered for token in FORBIDDEN_BLIND_TOKENS):
            raise CalibrationValidationError(f"Blind assignment leak in string at {path}")


def _validate_operator_inputs(fixture_root: Path) -> tuple[dict[str, Any], dict[str, dict[str, Any]], dict[str, str], str]:
    operator = fixture_root / "operator"
    ledger = _load_json(operator / "generation-ledger.json", "operator ledger")
    errors = validate_operator_ledger(ledger)
    if errors:
        raise CalibrationValidationError("Invalid operator ledger: " + "; ".join(errors))
    command_set = _load_json(operator / "command-set.json", "operator command set")
    _require_exact(command_set, {"schemaVersion", "holdoutCohort", "freezeStatus", "commands", "commandSetHash"}, "command set")
    if command_set["schemaVersion"] != "s0a-operator-command-set/v1" or command_set["freezeStatus"] != "FROZEN_FOR_CAPTURE":
        raise CalibrationValidationError("Operator command set is not capture-frozen v1")
    command_set_hash = _canonical_hash_matches(command_set, "commandSetHash", "command set")
    if command_set["holdoutCohort"] != ledger["holdoutCohort"]:
        raise CalibrationValidationError("Operator ledger and command set cohort identities differ")
    samples = {sample["sampleId"]: sample for sample in ledger["samples"]}
    if len(samples) != len(ledger["samples"]):
        raise CalibrationValidationError("Operator ledger repeats sample IDs")
    declared: dict[str, str] = {}
    for item in command_set["commands"]:
        _require_exact(item, {"sampleId", "commandHash"}, "command set entry")
        sample_id = item["sampleId"]
        if sample_id in declared or sample_id not in samples:
            raise CalibrationValidationError("Operator command set has duplicate or foreign sample ID")
        declared[sample_id] = _require_hash(item["commandHash"], "command hash")
    if set(declared) != set(samples):
        raise CalibrationValidationError("Operator command set and ledger sample sets differ")
    commands_dir = operator / "mutation-commands"
    _require_direct_shape(
        commands_dir,
        {f"{sample_id}.mutation-command.json" for sample_id in samples},
        set(),
        "operator mutation-command directory",
    )
    for sample_id, sample in samples.items():
        command = _load_json(commands_dir / f"{sample_id}.mutation-command.json", "operator mutation command")
        _require_exact(
            command,
            {
                "schemaVersion",
                "sampleId",
                "effectId",
                "fixedSeed",
                "mutationCommands",
                "fixtureApplicationStatus",
                "operatorInstruction",
                "commandHash",
            },
            "operator mutation command",
        )
        if (
            command["schemaVersion"] != "s0a-operator-mutation-command/v1"
            or command["sampleId"] != sample_id
            or command["fixedSeed"] != sample["fixedSeed"]
            or command["mutationCommands"] != sample["injection"]["mutationCommands"]
            or command["fixtureApplicationStatus"] != "NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER"
        ):
            raise CalibrationValidationError(f"Operator command does not match its ledger sample: {sample_id}")
        if _canonical_hash_matches(command, "commandHash", "operator command") != declared[sample_id]:
            raise CalibrationValidationError(f"Operator command hash differs from command set: {sample_id}")
    return ledger, samples, declared, command_set_hash


def _verify_ledger_chain(ledger_dir: Path) -> tuple[str, list[str], dict[str, Any]]:
    files = sorted(ledger_dir.iterdir(), key=lambda path: path.name)
    if not files:
        raise CalibrationValidationError("Candidate lifecycle ledger is empty")
    prior: str | None = None
    kinds: list[str] = []
    first_details: dict[str, Any] | None = None
    for sequence, path in enumerate(files):
        if not path.is_file() or _is_link(path) or path.suffix != ".json":
            raise CalibrationValidationError("Candidate lifecycle ledger contains a foreign entry")
        entry = _load_json(path, "candidate lifecycle ledger entry")
        _require_exact(entry, {"schema", "sequence", "kind", "details", "recordedUtc", "previousEntryHash", "entryHash"}, "candidate ledger entry")
        expected_name = f"{sequence:04d}-{entry['kind']}.json"
        if (
            entry["schema"] != "w24-s0a-fixture-ledger/v2"
            or entry["sequence"] != sequence
            or path.name != expected_name
            or entry["previousEntryHash"] != prior
        ):
            raise CalibrationValidationError(f"Candidate lifecycle ledger chain identity failed at {path.name}")
        if sequence == 0:
            first_details = entry["details"] if isinstance(entry["details"], dict) else None
        kinds.append(entry["kind"])
        prior = _canonical_hash_matches(entry, "entryHash", "candidate ledger entry")
    if prior is None or first_details is None:
        raise CalibrationValidationError("Candidate lifecycle ledger lacks a valid created identity")
    return prior, kinds, first_details


def _artifact_index(seal: dict[str, Any], capture: Path) -> dict[str, str]:
    artifacts = seal.get("artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise CalibrationValidationError("Capture evidence seal has no artifact index")
    declared: dict[str, str] = {}
    for item in artifacts:
        _require_exact(item, {"file", "sha256"}, "capture seal artifact")
        relative = item["file"]
        if not isinstance(relative, str) or not relative or Path(relative).is_absolute() or ".." in Path(relative).parts or "\\" in relative:
            raise CalibrationValidationError("Capture seal has an unsafe artifact path")
        if relative in declared:
            raise CalibrationValidationError("Capture seal repeats an artifact path")
        path = capture / Path(relative)
        if not path.is_file() or _is_link(path):
            raise CalibrationValidationError(f"Capture seal artifact is missing or linked: {relative}")
        declared[relative] = _require_hash(item["sha256"], "capture artifact hash")
        if _sha256_file(path) != declared[relative]:
            raise CalibrationValidationError(f"Capture artifact hash mismatch: {relative}")
    actual = {
        path.relative_to(capture).as_posix()
        for path in capture.rglob("*")
        if path.is_file() and path.name != "evidence-seal.json"
    }
    if actual != set(declared):
        raise CalibrationValidationError("Capture seal does not index exactly the complete capture tree")
    return declared


def _verify_raw_capture(
    capture: Path,
    sample_id: str,
    command_hash: str,
    fixed_seed: int,
    trusted_capture_tool_hash: str,
) -> tuple[dict[str, Any], dict[str, str], str, tuple[int, int, int]]:
    reject_links_and_reparse(capture)
    _require_direct_shape(
        capture,
        {"evidence-lock.json", "diagnostic-pass-manifest.json", "capture-metadata.json", "evidence-seal.json"},
        {"frames", "diagnostics"},
        "raw capture root",
    )
    lock = _load_json(capture / "evidence-lock.json", "capture evidence lock")
    _require_exact(lock, {"schema", "candidateId", "captureProfileSha256"}, "capture evidence lock")
    if lock["schema"] != "w24-s0a-evidence-lock/v1" or lock["candidateId"] != sample_id:
        raise CalibrationValidationError("Capture evidence lock does not bind the candidate")
    metadata_path = capture / "capture-metadata.json"
    metadata = _load_json(metadata_path, "capture metadata")
    seal = _load_json(capture / "evidence-seal.json", "capture evidence seal")
    _require_exact(seal, {"schema", "candidateId", "captureProfileSha256", "artifacts", "provenance", "sealHash"}, "capture evidence seal")
    if seal["schema"] != "w24-s0a-final-evidence-seal/v1" or seal["candidateId"] != sample_id or seal["sealHash"] != _ordered_compact_hash(seal, "sealHash"):
        raise CalibrationValidationError("Capture evidence seal is not a valid ordered final seal")
    provenance = seal["provenance"]
    _require_exact(provenance, {"operatorCommandHash", "captureToolSha256", "sourceHashesSha256", "captureMetadataSha256"}, "capture seal provenance")
    if provenance["operatorCommandHash"] != command_hash:
        raise CalibrationValidationError("Capture evidence seal does not bind its operator command")
    declared = _artifact_index(seal, capture)
    if provenance.get("captureMetadataSha256") != _sha256_file(metadata_path):
        raise CalibrationValidationError("Capture evidence seal does not bind capture metadata bytes")
    _require_exact(
        metadata,
        {
            "schema",
            "candidateId",
            "captureModePolicy",
            "executedInBatchMode",
            "frameRetentionPolicy",
            "retainedFrameIndices",
            "retainedFrameIndicesSha256",
            "formalPlayerLoop",
            "captureProfile",
            "captureProfileSha256",
            "sourceHashes",
            "diagnosticPassManifest",
            "typedRawDiagnostics",
            "metricInputs",
            "metricReports",
            "semanticTelemetry",
            "supplementalDiagnostics",
            "frames",
        },
        "capture metadata",
    )
    if metadata["schema"] != "w24-s0a-capture-evidence/v1" or metadata["candidateId"] != sample_id or metadata["executedInBatchMode"] is not True:
        raise CalibrationValidationError("Capture metadata is not the sample's formal graphics-backed evidence")
    profile = metadata["captureProfile"]
    if not isinstance(profile, dict):
        raise CalibrationValidationError("Capture metadata has no Capture Profile object")
    _require_exact(profile, PROFILE_FIELDS, "formal Capture Profile")
    profile_hash = _hash_text(json.dumps(profile, ensure_ascii=False, separators=(",", ":")))
    if metadata["captureProfileSha256"] != profile_hash or lock.get("captureProfileSha256") != profile_hash or seal.get("captureProfileSha256") != profile_hash:
        raise CalibrationValidationError("Capture Profile instance hash binding failed")
    if (
        profile.get("profileVersion") != "w24-s0a-formal-calibration-capture-profile/v1"
        or profile.get("canonicalSeed") != fixed_seed
        or profile.get("retainedFrameIndices") != list(FRAME_TABLE)
    ):
        raise CalibrationValidationError("Capture Profile has the wrong fixed seed or frame table")
    renderer_asset = _require_exact(profile["rendererAsset"], {"reference", "sha256"}, "Capture Profile renderer asset")
    volume = _require_exact(profile["volume"], {"reference", "sha256"}, "Capture Profile volume")
    _require_hash(renderer_asset["sha256"], "Capture Profile renderer asset hash")
    _require_hash(volume["sha256"], "Capture Profile volume hash")
    if (
        not all(isinstance(profile.get(field), str) and profile[field] for field in ("unityVersion", "urpVersion", "graphicsApi", "graphicsDevice", "graphicsDriverVersion", "renderTextureFormat", "scenePath", "serializedCameraReference", "colorSpace"))
        or profile.get("resolution") != [960, 540]
        or profile.get("fps") != 60
        or profile.get("colorSpace") != "Linear"
        or profile.get("hdr") is not False
        or profile.get("msaa") is not False
        or profile.get("bloom") != {"value": False, "validation": "caller-frozen"}
        or profile.get("toneMapping") != {"value": "None", "validation": "caller-frozen"}
    ):
        raise CalibrationValidationError("Capture Profile does not match the frozen S0a graphics policy")
    robustness = _derive_robustness_seeds(fixed_seed)
    if profile.get("robustnessSeeds") != list(robustness):
        raise CalibrationValidationError("Capture Profile robustness seeds violate the frozen derivation")
    retained_hash = _hash_text(",".join(str(value) for value in FRAME_TABLE))
    if (
        metadata.get("captureModePolicy") != "graphics-device batchmode required; -nographics prohibited; synchronized ReadPixels"
        or metadata.get("frameRetentionPolicy") != "retained-keyframes-only; CaptureFrame may only be called from the frozen retainedFrameIndices table; full-rate raw frames are not formal evidence"
        or metadata.get("retainedFrameIndices") != list(FRAME_TABLE)
        or metadata.get("retainedFrameIndicesSha256") != retained_hash
        or profile.get("retainedFrameIndicesSha256") != retained_hash
    ):
        raise CalibrationValidationError("Capture metadata frame table differs from the frozen 8-frame table")
    player_loop = _require_exact(metadata["formalPlayerLoop"], {"observedSerial", "consumedSerial", "allObservedFramesConsumed"}, "formal PlayerLoop identity")
    if (
        isinstance(player_loop["observedSerial"], bool)
        or not isinstance(player_loop["observedSerial"], int)
        or player_loop["observedSerial"] < 1
        or player_loop["observedSerial"] != player_loop["consumedSerial"]
        or player_loop["allObservedFramesConsumed"] is not True
    ):
        raise CalibrationValidationError("Capture did not consume the exact formal PlayerLoop observations")
    source_hashes = metadata["sourceHashes"]
    _require_exact(source_hashes, {"scene", "prefab", "manifest", "captureTool"}, "capture source identities")
    _require_exact(source_hashes["scene"], {"path", "sha256"}, "capture scene identity")
    _require_exact(source_hashes["prefab"], {"path", "guid", "sha256"}, "capture prefab identity")
    _require_exact(source_hashes["manifest"], {"path", "sha256", "buildHash"}, "capture manifest identity")
    _require_exact(source_hashes["captureTool"], {"path", "version", "sha256"}, "capture tool identity")
    for value, label in (
        (source_hashes["scene"]["sha256"], "capture scene hash"),
        (source_hashes["prefab"]["sha256"], "capture prefab hash"),
        (source_hashes["manifest"]["sha256"], "capture manifest hash"),
        (source_hashes["manifest"]["buildHash"], "capture build hash"),
        (source_hashes["captureTool"]["sha256"], "capture tool hash"),
    ):
        _require_hash(value, label)
    source_hashes_compact = json.dumps(source_hashes, ensure_ascii=False, separators=(",", ":"))
    capture_tool = source_hashes["captureTool"]
    if (
        capture_tool["path"] != CAPTURE_TOOL_IDENTITY_PATH
        or capture_tool["version"] != CAPTURE_TOOL_VERSION
        or capture_tool["sha256"] != trusted_capture_tool_hash
        or provenance["captureToolSha256"] != trusted_capture_tool_hash
        or provenance["sourceHashesSha256"] != _hash_text(source_hashes_compact)
    ):
        raise CalibrationValidationError("Capture seal provenance does not bind source/tool identities")
    diagnostic_manifest = _require_exact(metadata["diagnosticPassManifest"], {"file", "sha256"}, "diagnostic pass manifest identity")
    if diagnostic_manifest["file"] != "diagnostic-pass-manifest.json" or declared.get(diagnostic_manifest["file"]) != diagnostic_manifest["sha256"]:
        raise CalibrationValidationError("Capture metadata does not bind the diagnostic pass manifest")
    semantic_records = metadata["semanticTelemetry"]
    if not isinstance(semantic_records, list) or len(semantic_records) != 1:
        raise CalibrationValidationError("Formal capture requires one semantic telemetry declaration")
    semantic_record = _require_exact(semantic_records[0], {"kind", "description", "file", "sha256"}, "semantic telemetry declaration")
    if (
        semantic_record["kind"] != "semantic-telemetry"
        or semantic_record["file"] != "diagnostics/semantic-telemetry.json"
        or not isinstance(semantic_record["description"], str)
        or not semantic_record["description"].strip()
        or declared.get(semantic_record["file"]) != semantic_record["sha256"]
    ):
        raise CalibrationValidationError("Capture metadata does not bind the formal semantic telemetry")
    if metadata["typedRawDiagnostics"] != [] or metadata["metricInputs"] != [] or metadata["metricReports"] != [] or metadata["supplementalDiagnostics"] != []:
        raise CalibrationValidationError("S0a projection accepts only the frozen baseline diagnostics surface")
    seeds = (fixed_seed, *robustness)
    expected_pairs = {(seed, frame) for seed in seeds for frame in FRAME_TABLE}
    frames = metadata.get("frames")
    if not isinstance(frames, list) or len(frames) != 24:
        raise CalibrationValidationError("Formal capture must contain exactly three seeds x eight retained frames")
    seen: set[tuple[int, int]] = set()
    for frame in frames:
        if not isinstance(frame, dict):
            raise CalibrationValidationError("Capture frame metadata must be an object")
        _require_exact(
            frame,
            {"frameIndex", "simulationTime", "state", "seed", "beauty", "diagnostics"},
            "capture frame metadata",
        )
        seed, index = frame.get("seed"), frame.get("frameIndex")
        key = (seed, index)
        if key not in expected_pairs or key in seen:
            raise CalibrationValidationError("Capture frame set has a foreign or duplicate seed/frame pair")
        seen.add(key)
        beauty = frame.get("beauty")
        diagnostics = frame.get("diagnostics")
        if not isinstance(beauty, dict) or not isinstance(diagnostics, list) or len(diagnostics) != 1:
            raise CalibrationValidationError("Capture frame lacks Beauty or the one effect-only diagnostic")
        # These are deliberately two different recorder contracts.  Beauty is the
        # public visual input and therefore contains only its sealed file identity.
        # The diagnostic is the recorder's effect-only observation and carries its
        # typed pass/method facts.  Treating both as a legacy generic artifact shape
        # would reject every real recorder 1.1.3 capture.
        _require_exact(beauty, {"file", "sha256"}, "capture Beauty artifact")
        diagnostic = diagnostics[0]
        _require_exact(
            diagnostic,
            {"passId", "file", "sha256", "foregroundPixels", "method"},
            "capture effect-only diagnostic",
        )
        if diagnostic["passId"] != "effect-only-rgba":
            raise CalibrationValidationError("Capture frame diagnostic is not the formal effect-only pass")
        foreground_pixels = diagnostic["foregroundPixels"]
        if isinstance(foreground_pixels, bool) or not isinstance(foreground_pixels, int) or foreground_pixels < 0:
            raise CalibrationValidationError("Capture frame effect-only foregroundPixels is invalid")
        if not isinstance(diagnostic["method"], str) or not diagnostic["method"].strip():
            raise CalibrationValidationError("Capture frame effect-only method is missing")
        for artifact in (beauty, diagnostic):
            if declared.get(artifact["file"]) != artifact["sha256"]:
                raise CalibrationValidationError("Capture frame artifact is not bound by the final seal")
            _validate_png(capture / Path(artifact["file"]), artifact["sha256"])
    if seen != expected_pairs:
        raise CalibrationValidationError("Capture frame set is incomplete")
    telemetry = _load_json(capture / "diagnostics" / "semantic-telemetry.json", "formal semantic telemetry")
    _require_exact(telemetry, {"schema", "sampleId", "fixedSeed", "frames"}, "formal semantic telemetry")
    if telemetry["schema"] != "w24-s0a-semantic-telemetry/v1" or telemetry["sampleId"] != sample_id or telemetry["fixedSeed"] != fixed_seed:
        raise CalibrationValidationError("Formal semantic telemetry does not bind the sample/seed")
    telemetry_frames = telemetry["frames"]
    if not isinstance(telemetry_frames, list) or len(telemetry_frames) != 24:
        raise CalibrationValidationError("Formal semantic telemetry must contain the exact three-by-eight frame table")
    telemetry_pairs: set[tuple[int, int]] = set()
    for frame in telemetry_frames:
        _require_exact(frame, {"frameIndex", "state", "seed", "liveParticleCount", "enabledRendererCount", "enabledLightCount", "transitionSerial", "cleanupComplete"}, "semantic telemetry frame")
        pair = (frame["seed"], frame["frameIndex"])
        if pair not in expected_pairs or pair in telemetry_pairs or not isinstance(frame["state"], str) or not frame["state"].strip():
            raise CalibrationValidationError("Semantic telemetry frame set is foreign, duplicate, or state-less")
        telemetry_pairs.add(pair)
    if telemetry_pairs != expected_pairs:
        raise CalibrationValidationError("Semantic telemetry frame set is incomplete")
    return metadata, declared, profile_hash, seeds


def _verify_derived_invalid(
    candidate: Path,
    raw_capture: Path,
    command_hash: str,
    completion: dict[str, Any],
    expected_value: str,
) -> tuple[Path, str]:
    derived = candidate / "invalid-evidence"
    reject_links_and_reparse(derived)
    manifest = _load_json(derived / "invalid-evidence-manifest.json", "derived invalid-evidence manifest")
    _require_exact(manifest, {"schema", "commandHash", "sourceCaptureSealHash", "kind", "derivation", "derivedManifestHash"}, "derived invalid-evidence manifest")
    derived_hash = _canonical_hash_matches(manifest, "derivedManifestHash", "derived invalid-evidence manifest")
    expected_derivations = {
        "missing_key_frame": "deleted-beauty-frame",
        "sha256_mismatch": "metadata-hash-mismatch",
    }
    if expected_value not in expected_derivations:
        raise CalibrationValidationError("Invalid-evidence operator command has an unsupported expected value")
    if (
        manifest["schema"] != "w24-s0a-derived-invalid-evidence/v1"
        or manifest["commandHash"] != command_hash
        or manifest["sourceCaptureSealHash"] != _sha256_file(raw_capture / "evidence-seal.json")
        or manifest["kind"] != expected_value
        or manifest["derivation"] != expected_derivations[expected_value]
        or completion.get("invalidEvidenceManifestHash") != derived_hash
    ):
        raise CalibrationValidationError("Derived invalid evidence does not bind its raw capture/operator command/completion mapping")
    raw_files = {path.relative_to(raw_capture).as_posix() for path in raw_capture.rglob("*") if path.is_file()}
    derived_files = {path.relative_to(derived).as_posix() for path in derived.rglob("*") if path.is_file()}
    if manifest["kind"] == "missing_key_frame" and manifest["derivation"] == "deleted-beauty-frame":
        difference = raw_files - derived_files
        if len(difference) != 1 or not next(iter(difference)).endswith("_beauty.png") or derived_files - raw_files != {"invalid-evidence-manifest.json"}:
            raise CalibrationValidationError("Missing-frame evidence must delete exactly one Beauty frame")
        deleted = next(iter(difference))
        for relative in sorted(raw_files - {deleted}):
            if _sha256_file(raw_capture / relative) != _sha256_file(derived / relative):
                raise CalibrationValidationError(f"Missing-frame derived evidence altered a non-target file: {relative}")
    elif manifest["kind"] == "sha256_mismatch" and manifest["derivation"] == "metadata-hash-mismatch":
        if derived_files != raw_files | {"invalid-evidence-manifest.json"}:
            raise CalibrationValidationError("Metadata-mismatch evidence has a foreign or missing file")
        changed = {relative for relative in raw_files if _sha256_file(raw_capture / relative) != _sha256_file(derived / relative)}
        if changed != {"capture-metadata.json"}:
            raise CalibrationValidationError("Metadata-mismatch evidence must alter only capture-metadata.json")
    else:
        raise CalibrationValidationError("Unsupported derived invalid-evidence mutation")
    return derived, derived_hash


def _verify_candidate(candidate: Path, sample: dict[str, Any], command_hash: str, trusted_capture_tool_hash: str) -> VerifiedSample:
    sample_id = sample["sampleId"]
    invalid = sample["kind"] == "invalid"
    _require_direct_shape(
        candidate,
        {"candidate-completion.json"},
        {"capture", "ledger", *( ["invalid-evidence"] if invalid else [] )},
        f"candidate {sample_id}",
    )
    completion = _load_json(candidate / "candidate-completion.json", "candidate completion")
    _require_exact(completion, {"schema", "sampleId", "commandHash", "captureSealHash", "invalidEvidenceManifestHash", "ledgerTailHash", "completionHash"}, "candidate completion")
    completion_hash = _canonical_hash_matches(completion, "completionHash", "candidate completion")
    if completion["schema"] != "w24-s0a-candidate-completion/v1" or completion["sampleId"] != sample_id or completion["commandHash"] != command_hash:
        raise CalibrationValidationError(f"Candidate completion identity mismatch: {sample_id}")
    ledger_tail, ledger_kinds, created_details = _verify_ledger_chain(candidate / "ledger")
    expected_kinds = ["created"]
    if sample["injection"]["mutationCommands"]:
        expected_kinds.append("queued-invalid-evidence" if invalid else "visual-mutation-applied")
    expected_kinds.extend(
        [
            "capture-begun",
            "seed-started",
            "seed-stop-requested",
            "seed-started",
            "seed-stop-requested",
            "seed-started",
            "seed-stop-requested",
            "raw-capture-sealed",
        ]
    )
    if invalid:
        expected_kinds.append("invalid-evidence-injected")
    expected_kinds.extend(["candidate-finalized", "cleanup"])
    if ledger_kinds != expected_kinds or created_details.get("sampleId") != sample_id or created_details.get("commandHash") != command_hash:
        raise CalibrationValidationError(f"Candidate lifecycle ledger is incomplete, reordered, or foreign: {sample_id}")
    if completion["ledgerTailHash"] != ledger_tail:
        raise CalibrationValidationError(f"Candidate completion does not bind lifecycle ledger: {sample_id}")
    raw = candidate / "capture"
    metadata, raw_index, profile_hash, seeds = _verify_raw_capture(raw, sample_id, command_hash, sample["fixedSeed"], trusted_capture_tool_hash)
    raw_seal_file_hash = _sha256_file(raw / "evidence-seal.json")
    if completion["captureSealHash"] != raw_seal_file_hash:
        raise CalibrationValidationError(f"Candidate completion does not bind raw capture seal: {sample_id}")
    selected = raw
    invalid_hash: str | None = None
    if invalid:
        mutations = sample["injection"]["mutationCommands"]
        if (
            not isinstance(mutations, list)
            or len(mutations) != 1
            or mutations[0].get("operation") != "set"
            or mutations[0].get("targetKey") != "Capture.frameManifestIntegrity"
            or mutations[0].get("value") not in {"missing_key_frame", "sha256_mismatch"}
        ):
            raise CalibrationValidationError(f"Invalid sample has no exact frame-integrity operator command: {sample_id}")
        selected, invalid_hash = _verify_derived_invalid(candidate, raw, command_hash, completion, mutations[0]["value"])
    elif completion["invalidEvidenceManifestHash"] is not None:
        raise CalibrationValidationError(f"Ordinary candidate claims invalid derived evidence: {sample_id}")
    selected_metadata_path = selected / "capture-metadata.json"
    selected_bytes = selected_metadata_path.read_bytes()
    selected_actual = _sha256_bytes(selected_bytes)
    selected_declared = raw_index["capture-metadata.json"]
    try:
        selected_text = selected_bytes.decode("utf-8")
    except UnicodeError as exc:
        raise CalibrationValidationError(f"Selected capture metadata is unreadable: {sample_id}") from exc
    selected_metadata = _parse_json_text(selected_text, f"selected capture metadata {sample_id}")
    if not isinstance(selected_metadata, dict):
        raise CalibrationValidationError(f"Selected capture metadata is not an object: {sample_id}")
    frame_records = metadata["frames"]
    source_hashes = metadata["sourceHashes"]
    if not isinstance(source_hashes, dict):
        raise CalibrationValidationError(f"Capture source hashes are missing: {sample_id}")
    return VerifiedSample(
        sample_id=sample_id,
        design_requirement_id=_opaque_requirement_id(sample_id),
        command_hash=command_hash,
        completion_hash=completion_hash,
        raw_capture_seal_file_hash=raw_seal_file_hash,
        selected_source=selected,
        selected_metadata_bytes=selected_bytes,
        selected_metadata_declared_hash=selected_declared,
        selected_metadata_actual_hash=selected_actual,
        metadata=selected_metadata,
        profile=metadata["captureProfile"],
        profile_instance_hash=profile_hash,
        fixed_seed=sample["fixedSeed"],
        seeds=seeds,
        source_hashes=source_hashes,
        frame_records=frame_records,
        invalid_derivation_hash=invalid_hash,
        ledger_tail_hash=ledger_tail,
    )


def _common_profile(samples: list[VerifiedSample]) -> dict[str, Any]:
    common: dict[str, Any] | None = None
    for sample in samples:
        profile = copy.deepcopy(sample.profile)
        profile.pop("canonicalSeed", None)
        profile.pop("robustnessSeeds", None)
        if common is None:
            common = profile
        elif profile != common:
            raise CalibrationValidationError("Capture Profile instances differ outside the frozen per-sample seed fields")
    assert common is not None
    return common


def _common_sources(samples: list[VerifiedSample]) -> dict[str, Any]:
    first = samples[0].source_hashes
    if any(sample.source_hashes != first for sample in samples[1:]):
        raise CalibrationValidationError("Formal cohort source identities differ between samples")
    return copy.deepcopy(first)


def _extract_source_identities(source_hashes: dict[str, Any]) -> dict[str, str]:
    try:
        return {
            "sceneHash": _require_hash(source_hashes["scene"]["sha256"], "source scene hash"),
            "prefabManifestHash": _require_hash(source_hashes["manifest"]["sha256"], "source manifest hash"),
            "buildHash": _require_hash(source_hashes["manifest"]["buildHash"], "source build hash"),
            "captureToolHash": _require_hash(source_hashes["captureTool"]["sha256"], "source capture-tool hash"),
        }
    except (KeyError, TypeError) as exc:
        raise CalibrationValidationError("Formal capture sourceHashes shape is incomplete") from exc


def _file_identity(path: Path, label: str) -> str:
    return _sha256_file(_secure_existing_file(path, label))


def _validated_reduced_metrics_hash(path: Path) -> str:
    metrics_path = _secure_existing_file(path, "reduced metrics")
    metrics = _load_json(metrics_path, "reduced metrics")
    errors = verify_metrics(metrics)
    per_requirement = metrics.get("perRequirementMetrics") if isinstance(metrics, dict) else None
    if errors:
        raise CalibrationValidationError("Reduced metrics verification failed: " + "; ".join(errors))
    if (
        metrics.get("holdoutCohort") != COHORT_IDS["reduced"]
        or not isinstance(per_requirement, dict)
        or per_requirement.get("falsePassCount") != 0
    ):
        raise CalibrationValidationError("Expanded full projection requires reduced cohort metrics with falsePassCount=0")
    return _require_hash(metrics.get("reportHash"), "reduced metrics reportHash")


def _model_identity(model_version_id: str) -> str:
    if not isinstance(model_version_id, str) or not model_version_id.strip():
        raise CalibrationValidationError("model_version_id must be a non-empty frozen identifier")
    return _hash_text("W24-S0a-model-version|" + model_version_id.strip())


def _blind_order(sample_id: str) -> str:
    return hashlib.sha256(f"W24-S0a-projected-blind-order|v1|{sample_id}".encode("utf-8")).hexdigest()


def _copy_new(source: Path, destination: Path) -> str:
    if not source.is_file() or _is_link(source):
        raise CalibrationValidationError(f"Projection source is missing or linked: {source}")
    if destination.exists():
        raise CalibrationValidationError(f"Projection destination is write-once: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    if destination.suffix.lower() == ".png":
        return _validate_png(destination, _sha256_file(source))
    return _sha256_file(destination)


def _write_json_new(path: Path, document: dict[str, Any]) -> None:
    if path.exists():
        raise CalibrationValidationError(f"Projection artifact is write-once: {path}")
    try:
        _validate_json_scalars(document)
        encoded = json.dumps(document, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    except (UnicodeError, ValueError) as exc:
        raise CalibrationValidationError(f"Projection document is not strict JSON: {path}: {exc}") from exc
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(encoded, encoding="utf-8", newline="\n")


def _frame_lookup(sample: VerifiedSample) -> dict[tuple[int, int], dict[str, Any]]:
    return {(frame["seed"], frame["frameIndex"]): frame for frame in sample.frame_records}


def _project_sample(stage: Path, sample: VerifiedSample, contract_hash: str, policy_hash: str, frame_table_hash: str, source_ids: dict[str, str]) -> tuple[dict[str, Any], dict[str, Any]]:
    lookup = _frame_lookup(sample)
    frames: list[dict[str, Any]] = []
    diagnostic_records: list[dict[str, Any]] = []
    for seed_ordinal, seed in enumerate(sample.seeds):
        for frame_index in FRAME_TABLE:
            source_record = lookup[(seed, frame_index)]
            source_beauty = source_record["beauty"]
            source_diagnostic = source_record["diagnostics"][0]
            source_beauty_path = sample.selected_source / Path(source_beauty["file"])
            blind_relative = f"frames/{sample.sample_id}/seed_{seed_ordinal}/frame_{frame_index:05d}_beauty.png"
            blind_destination = stage / "blind" / blind_relative
            if source_beauty_path.is_file():
                actual_hash = _copy_new(source_beauty_path, blind_destination)
                availability = "present"
                declared_hash: str | None = source_beauty["sha256"]
                file_value: str | None = blind_relative
            else:
                actual_hash = None
                availability = "missing"
                declared_hash = source_beauty["sha256"]
                file_value = None
            frames.append(
                {
                    "seedOrdinal": seed_ordinal,
                    "frameIndex": frame_index,
                    "stateRef": source_record.get("state", "unknown"),
                    "beauty": {
                        "availability": availability,
                        "file": file_value,
                        "declaredHash": declared_hash,
                        "actualHash": actual_hash,
                    },
                }
            )
            diagnostic_source = sample.selected_source / Path(source_diagnostic["file"])
            operator_relative = f"operator/evidence/{sample.sample_id}/effect-only/seed_{seed_ordinal}/frame_{frame_index:05d}_effect-only.png"
            diagnostic_hash = _copy_new(diagnostic_source, stage / operator_relative)
            diagnostic_records.append(
                {
                    "seedOrdinal": seed_ordinal,
                    "frameIndex": frame_index,
                    "file": operator_relative,
                    "declaredHash": source_diagnostic["sha256"],
                    "actualHash": diagnostic_hash,
                }
            )
    evidence = _with_hash(
        {
            "schemaVersion": EVIDENCE_SCHEMA,
            "sampleId": sample.sample_id,
            "designRequirementIds": [sample.design_requirement_id],
            "reviewContractHash": contract_hash,
            "captureProfilePolicyHash": policy_hash,
            "captureProfileInstanceHash": sample.profile_instance_hash,
            "frameTableHash": frame_table_hash,
            "sourceIdentity": source_ids,
            "captureMetadata": {
                "declaredHash": sample.selected_metadata_declared_hash,
                "actualHash": sample.selected_metadata_actual_hash,
            },
            "frames": frames,
        },
        "evidenceHash",
    )
    _validate_no_answer(evidence)
    evidence_relative = f"evidence/{sample.sample_id}.evidence.json"
    _write_json_new(stage / "blind" / evidence_relative, evidence)
    metadata_relative = f"operator/evidence/{sample.sample_id}/capture-metadata.json"
    metadata_destination = stage / metadata_relative
    metadata_destination.parent.mkdir(parents=True, exist_ok=True)
    metadata_destination.write_bytes(sample.selected_metadata_bytes)
    telemetry_source = sample.selected_source / "diagnostics" / "semantic-telemetry.json"
    telemetry_relative = f"operator/evidence/{sample.sample_id}/semantic-telemetry.json"
    telemetry_hash = _copy_new(telemetry_source, stage / telemetry_relative)
    operator_record = {
        "sampleId": sample.sample_id,
        "commandHash": sample.command_hash,
        "completionHash": sample.completion_hash,
        "rawCaptureSealFileHash": sample.raw_capture_seal_file_hash,
        "selectedEvidenceDerivationHash": sample.invalid_derivation_hash,
        "ledgerTailHash": sample.ledger_tail_hash,
        "captureProfileInstanceHash": sample.profile_instance_hash,
        "blindEvidenceHash": evidence["evidenceHash"],
        "blindEvidenceManifest": f"blind/{evidence_relative}",
        "selectedCaptureMetadata": {
            "file": metadata_relative,
            "declaredHash": sample.selected_metadata_declared_hash,
            "actualHash": sample.selected_metadata_actual_hash,
        },
        "semanticTelemetry": {"file": telemetry_relative, "sha256": telemetry_hash},
        "diagnosticFrames": diagnostic_records,
    }
    return evidence, operator_record


def _tree_index_hash(root: Path, excluded: set[str] | None = None) -> tuple[int, str]:
    excluded = excluded or set()
    rows: list[str] = []
    for path in sorted((path for path in root.rglob("*") if path.is_file()), key=lambda item: item.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        if relative in excluded:
            continue
        rows.append(relative + "|" + _sha256_file(path))
    return len(rows), _hash_text("\n".join(rows))


def _cohort_expected_count(ledger: dict[str, Any]) -> int:
    cohort = COHORT_BY_ID.get(ledger["holdoutCohort"])
    if cohort not in {"reduced", "full"}:
        raise CalibrationValidationError("Projection accepts only exact reduced or full formal cohorts")
    return sum(COHORTS[cohort].values())


def project_formal_capture(inputs: ProjectionInputs) -> dict[str, Any]:
    """Verify an exact cohort and atomically publish one write-once projection."""
    capture_root = _secure_existing_directory(inputs.capture_root, "formal capture root")
    fixture_root = _secure_existing_directory(inputs.fixture_root, "operator fixture root")
    output = _secure_output_path(inputs.output)
    reject_links_and_reparse(capture_root)
    reject_links_and_reparse(fixture_root)
    _capture_tool_sources, trusted_capture_tool_hash = _capture_tool_identity()
    ledger, operator_samples, command_hashes, command_set_hash = _validate_operator_inputs(fixture_root)
    expected_count = _cohort_expected_count(ledger)
    actual_candidates = {path.name: path for path in capture_root.iterdir() if path.is_dir() and not _is_link(path)}
    if len(actual_candidates) != expected_count or set(actual_candidates) != set(operator_samples):
        raise CalibrationValidationError("Capture root must contain exactly the frozen cohort's completed sample directories")
    if any(not path.is_dir() or _is_link(path) for path in capture_root.iterdir()):
        raise CalibrationValidationError("Capture root contains a foreign file or linked candidate")
    verified = [
        _verify_candidate(actual_candidates[sample_id], operator_samples[sample_id], command_hashes[sample_id], trusted_capture_tool_hash)
        for sample_id in sorted(operator_samples)
    ]
    profile_common = _common_profile(verified)
    source_hashes = _common_sources(verified)
    source_ids = _extract_source_identities(source_hashes)
    frame_table = {"retainedFrameIndices": list(FRAME_TABLE), "seedRows": ["canonical", "robustness-1", "robustness-2"]}
    frame_table_hash = normalized_sha256(frame_table)
    policy = _with_hash(
        {
            "schemaVersion": POLICY_SCHEMA,
            "holdoutCohort": ledger["holdoutCohort"],
            "profileTemplateWithoutSeeds": profile_common,
            "seedPolicy": "operator-fixed canonical uint32 + frozen xor-derived robustness pair/v1",
            "frameTable": frame_table,
            "instances": [
                {"sampleId": sample.sample_id, "captureProfileInstanceHash": sample.profile_instance_hash}
                for sample in verified
            ],
        },
        "policyHash",
    )
    contract = _with_hash(
        {
            "schemaVersion": CONTRACT_SCHEMA,
            "holdoutCohort": ledger["holdoutCohort"],
            "effectId": "sustained_flame_3d",
            "answerDisclosure": "NONE",
            "reviewInstructions": [
                "Read every fixed Beauty frame from all three seed rows; do not select a best frame.",
                "Judge the sustained-flame lifecycle as start, steady, stop, and cleanup visual evidence.",
                "Use exactly one top-level route from the frozen Visual QA protocol.",
                "Do not infer hidden implementation facts or a terminal S0a status.",
            ],
            "frameTable": frame_table,
            "assignments": [
                {
                    "sampleId": sample.sample_id,
                    "designRequirementIds": [sample.design_requirement_id],
                    "criterion": "The complete required sustained-flame visual lifecycle is observable and coherent in the fixed evidence set.",
                }
                for sample in sorted(verified, key=lambda item: item.sample_id)
            ],
        },
        "contractHash",
    )
    _validate_no_answer(contract)
    file_hashes = {
        "qaPromptHash": _file_identity(inputs.qa_prompt, "QA prompt"),
        "imageInputStrategyHash": _file_identity(inputs.image_input_strategy, "image input strategy"),
        "threeStateRulesHash": _file_identity(inputs.three_state_rules, "three-state rules"),
        "aggregationRulesHash": _file_identity(inputs.aggregation_rules, "aggregation rules"),
        "visualReviewSchemaHash": _file_identity(inputs.visual_review_schema, "visual review schema"),
        "modelVersionHash": _model_identity(inputs.model_version_id),
    }
    reduced_metrics_hash: str | None = None
    if inputs.reduced_metrics is not None:
        reduced_metrics_hash = _validated_reduced_metrics_hash(inputs.reduced_metrics)
    if ledger["holdoutCohort"] == COHORT_IDS["full"] and ledger["holdoutMode"] == "expanded" and reduced_metrics_hash is None:
        raise CalibrationValidationError("Expanded full projection requires its frozen reduced metrics artifact")
    if (ledger["holdoutCohort"] != COHORT_IDS["full"] or ledger["holdoutMode"] != "expanded") and reduced_metrics_hash is not None:
        raise CalibrationValidationError("Reduced metrics may bind only an expanded full projection")
    holdout_mode = (
        "INITIAL_REDUCED"
        if ledger["holdoutCohort"] == COHORT_IDS["reduced"]
        else ("EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS" if ledger["holdoutMode"] == "expanded" else "FRESH_INDEPENDENT_FULL")
    )
    context = _with_hash(
        {
            "schemaVersion": CONTEXT_SCHEMA,
            "holdoutCohort": ledger["holdoutCohort"],
            "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
            "holdoutIsolationMode": holdout_mode,
            "operatorLedgerHash": ledger["ledgerHash"],
            "reducedMetricsHash": reduced_metrics_hash,
            **file_hashes,
            "reviewContractHash": contract["contractHash"],
            "buildHash": source_ids["buildHash"],
            "captureProfilePolicyHash": policy["policyHash"],
            "frameTableHash": frame_table_hash,
            "sceneHash": source_ids["sceneHash"],
            "prefabManifestHash": source_ids["prefabManifestHash"],
            "captureToolHash": source_ids["captureToolHash"],
        },
        "contextHash",
    )
    staging_parent = output.parent
    staging_parent.mkdir(parents=True, exist_ok=True)
    staging_parent = _secure_existing_directory(staging_parent, "projection staging parent")
    stage = Path(tempfile.mkdtemp(prefix=".s0a-artifacts-staging-", dir=staging_parent))
    try:
        _write_json_new(stage / "blind" / "review-contract.json", contract)
        _write_json_new(stage / "operator" / "capture-profile-policy.json", policy)
        projected: list[tuple[VerifiedSample, dict[str, Any], dict[str, Any]]] = []
        for sample in verified:
            evidence, operator_record = _project_sample(stage, sample, contract["contractHash"], policy["policyHash"], frame_table_hash, source_ids)
            projected.append((sample, evidence, operator_record))
        blind_samples = [
            {
                "sampleId": sample.sample_id,
                "designRequirementIds": [sample.design_requirement_id],
                "captureProfileInstanceHash": sample.profile_instance_hash,
                "evidenceManifest": f"evidence/{sample.sample_id}.evidence.json",
                "evidenceHash": evidence["evidenceHash"],
            }
            for sample, evidence, _ in sorted(projected, key=lambda item: _blind_order(item[0].sample_id))
        ]
        blind_manifest = _with_hash(
            {
                "schemaVersion": BLIND_SCHEMA,
                "holdoutCohort": ledger["holdoutCohort"],
                "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
                "reviewContractHash": contract["contractHash"],
                "captureProfilePolicyHash": policy["policyHash"],
                "reviewContextHash": context["contextHash"],
                "samples": blind_samples,
            },
            "manifestHash",
        )
        _validate_no_answer(blind_manifest)
        _write_json_new(stage / "blind" / "blind-submission-manifest.json", blind_manifest)
        _write_json_new(stage / "blind" / "review-freeze-context.json", context)
        bundle = _with_hash(
            {
                "schemaVersion": BUNDLE_SCHEMA,
                "holdoutCohort": ledger["holdoutCohort"],
                "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
                "operatorLedgerHash": ledger["ledgerHash"],
                "commandSetHash": command_set_hash,
                "blindManifestHash": blind_manifest["manifestHash"],
                "reviewContextHash": context["contextHash"],
                "captureProfilePolicyHash": policy["policyHash"],
                "samples": [record for _, _, record in sorted(projected, key=lambda item: item[0].sample_id)],
            },
            "bundleHash",
        )
        _write_json_new(stage / "operator" / "evidence-bundle.json", bundle)
        worksheet = _with_hash(
            {
                "schemaVersion": WORKSHEET_SCHEMA,
                "holdoutCohort": ledger["holdoutCohort"],
                "reviewStatus": "UNREVIEWED",
                "blindManifestHash": blind_manifest["manifestHash"],
                "reviewContractHash": contract["contractHash"],
                "rows": [
                    {
                        "sampleId": sample.sample_id,
                        "designRequirementIds": [sample.design_requirement_id],
                        "evidenceHash": evidence["evidenceHash"],
                        "groundTruthRoute": None,
                        "perRequirement": [{"designRequirementId": sample.design_requirement_id, "state": None}],
                        "visuallyObservable": None,
                        "eligibleForVisualMetrics": None,
                        "reviewer": None,
                        "adjudicationNotes": "",
                    }
                    for sample, evidence, _ in sorted(projected, key=lambda item: item[0].sample_id)
                ],
            },
            "worksheetHash",
        )
        _write_json_new(stage / "operator" / "human-adjudication-worksheet.json", worksheet)
        completion_identities = [sample.sample_id + "|" + sample.completion_hash for sample in verified]
        capture_root_identity_hash = _hash_text("\n".join(completion_identities))
        (
            projection_tool_sources,
            projection_tool_runtime_dependencies,
            projection_tool_source_hash,
        ) = _projection_tool_identity()
        artifact_count, artifact_tree_hash = _tree_index_hash(stage)
        receipt = _with_hash(
            {
                "schemaVersion": RECEIPT_SCHEMA,
                "projectionToolVersion": PROJECTION_VERSION,
                "projectionToolSourceSchema": PROJECTION_TOOL_SOURCE_SCHEMA,
                "projectionToolSources": projection_tool_sources,
                "projectionToolRuntimeDependencies": projection_tool_runtime_dependencies,
                "projectionToolSourceHash": projection_tool_source_hash,
                "status": "PROJECTED_NOT_REVIEWED",
                "holdoutCohort": ledger["holdoutCohort"],
                "operatorLedgerHash": ledger["ledgerHash"],
                "commandSetHash": command_set_hash,
                "captureRootIdentityHash": capture_root_identity_hash,
                "captureProfilePolicyHash": policy["policyHash"],
                "reviewContractHash": contract["contractHash"],
                "reviewContextHash": context["contextHash"],
                "blindManifestHash": blind_manifest["manifestHash"],
                "operatorEvidenceBundleHash": bundle["bundleHash"],
                "humanWorksheetHash": worksheet["worksheetHash"],
                "artifactCountExcludingReceipt": artifact_count,
                "artifactTreeHashExcludingReceipt": artifact_tree_hash,
                "assertions": {
                    "labelsCreated": False,
                    "labelsFrozen": False,
                    "qaReportsCreated": False,
                    "metricsEmitted": False,
                    "terminalStatusClaimed": False,
                },
            },
            "receiptHash",
        )
        _write_json_new(stage / "projection-receipt.json", receipt)
        verify_projection(stage)
        os.replace(stage, output)
        return receipt
    except Exception:
        shutil.rmtree(stage, ignore_errors=True)
        raise


def verify_projection(root: Path) -> dict[str, Any]:
    root = _secure_existing_directory(root, "projected artifacts root")
    reject_links_and_reparse(root)
    _require_direct_shape(root, {"projection-receipt.json"}, {"blind", "operator"}, "projection root")
    receipt = _load_json(root / "projection-receipt.json", "projection receipt")
    _require_exact(
        receipt,
        {
            "schemaVersion",
            "projectionToolVersion",
            "projectionToolSourceSchema",
            "projectionToolSources",
            "projectionToolRuntimeDependencies",
            "projectionToolSourceHash",
            "status",
            "holdoutCohort",
            "operatorLedgerHash",
            "commandSetHash",
            "captureRootIdentityHash",
            "captureProfilePolicyHash",
            "reviewContractHash",
            "reviewContextHash",
            "blindManifestHash",
            "operatorEvidenceBundleHash",
            "humanWorksheetHash",
            "artifactCountExcludingReceipt",
            "artifactTreeHashExcludingReceipt",
            "assertions",
            "receiptHash",
        },
        "projection receipt",
    )
    if (
        receipt["schemaVersion"] != RECEIPT_SCHEMA
        or receipt["projectionToolVersion"] != PROJECTION_VERSION
        or receipt["projectionToolSourceSchema"] != PROJECTION_TOOL_SOURCE_SCHEMA
        or receipt["status"] != "PROJECTED_NOT_REVIEWED"
    ):
        raise CalibrationValidationError("Projection receipt version/status is invalid")
    _canonical_hash_matches(receipt, "receiptHash", "projection receipt")
    (
        current_projection_sources,
        current_projection_runtime_dependencies,
        current_projection_source_hash,
    ) = _projection_tool_identity()
    if (
        receipt["projectionToolSources"] != current_projection_sources
        or receipt["projectionToolRuntimeDependencies"] != current_projection_runtime_dependencies
        or receipt["projectionToolSourceHash"] != current_projection_source_hash
    ):
        raise CalibrationValidationError("Projection tool source set/hash has drifted from the sealed receipt")
    expected_assertions = {
        "labelsCreated": False,
        "labelsFrozen": False,
        "qaReportsCreated": False,
        "metricsEmitted": False,
        "terminalStatusClaimed": False,
    }
    if receipt.get("assertions") != expected_assertions:
        raise CalibrationValidationError("Projection receipt exceeds the authorized S0a pre-review boundary")
    cohort_key = COHORT_BY_ID.get(receipt["holdoutCohort"])
    if cohort_key not in {"reduced", "full"}:
        raise CalibrationValidationError("Projection receipt names an unsupported cohort")
    expected_count = sum(COHORTS[cohort_key].values())
    count, tree_hash = _tree_index_hash(root, {"projection-receipt.json"})
    if count != receipt["artifactCountExcludingReceipt"] or tree_hash != receipt["artifactTreeHashExcludingReceipt"]:
        raise CalibrationValidationError("Projection artifact tree differs from its write-once receipt")
    contract = _load_json(root / "blind" / "review-contract.json", "blind review contract")
    blind = _load_json(root / "blind" / "blind-submission-manifest.json", "blind manifest")
    context = _load_json(root / "blind" / "review-freeze-context.json", "review context")
    bundle = _load_json(root / "operator" / "evidence-bundle.json", "operator evidence bundle")
    worksheet = _load_json(root / "operator" / "human-adjudication-worksheet.json", "human worksheet")
    policy = _load_json(root / "operator" / "capture-profile-policy.json", "capture profile policy")
    _require_direct_shape(
        root / "blind",
        {"review-contract.json", "blind-submission-manifest.json", "review-freeze-context.json"},
        {"evidence", "frames"},
        "blind projection",
    )
    _require_direct_shape(
        root / "operator",
        {"capture-profile-policy.json", "evidence-bundle.json", "human-adjudication-worksheet.json"},
        {"evidence"},
        "operator projection",
    )
    for document, field, label in (
        (contract, "contractHash", "blind review contract"),
        (blind, "manifestHash", "blind manifest"),
        (context, "contextHash", "review context"),
        (bundle, "bundleHash", "operator evidence bundle"),
        (worksheet, "worksheetHash", "human worksheet"),
        (policy, "policyHash", "capture profile policy"),
    ):
        _canonical_hash_matches(document, field, label)
    _capture_sources, current_capture_tool_hash = _capture_tool_identity()
    _require_exact(
        contract,
        {"schemaVersion", "holdoutCohort", "effectId", "answerDisclosure", "reviewInstructions", "frameTable", "assignments", "contractHash"},
        "blind review contract",
    )
    _require_exact(
        blind,
        {"schemaVersion", "holdoutCohort", "freezeStatus", "reviewContractHash", "captureProfilePolicyHash", "reviewContextHash", "samples", "manifestHash"},
        "blind manifest",
    )
    _require_exact(
        context,
        {
            "schemaVersion", "holdoutCohort", "freezeStatus", "holdoutIsolationMode", "operatorLedgerHash", "reducedMetricsHash",
            "qaPromptHash", "imageInputStrategyHash", "threeStateRulesHash", "aggregationRulesHash", "visualReviewSchemaHash",
            "modelVersionHash", "reviewContractHash", "buildHash", "captureProfilePolicyHash", "frameTableHash", "sceneHash",
            "prefabManifestHash", "captureToolHash", "contextHash",
        },
        "review context",
    )
    _require_exact(
        bundle,
        {"schemaVersion", "holdoutCohort", "freezeStatus", "operatorLedgerHash", "commandSetHash", "blindManifestHash", "reviewContextHash", "captureProfilePolicyHash", "samples", "bundleHash"},
        "operator evidence bundle",
    )
    _require_exact(
        worksheet,
        {"schemaVersion", "holdoutCohort", "reviewStatus", "blindManifestHash", "reviewContractHash", "rows", "worksheetHash"},
        "human worksheet",
    )
    _require_exact(
        policy,
        {"schemaVersion", "holdoutCohort", "profileTemplateWithoutSeeds", "seedPolicy", "frameTable", "instances", "policyHash"},
        "capture profile policy",
    )
    expected_frame_table = {"retainedFrameIndices": list(FRAME_TABLE), "seedRows": ["canonical", "robustness-1", "robustness-2"]}
    if (
        contract["schemaVersion"] != CONTRACT_SCHEMA
        or blind["schemaVersion"] != BLIND_SCHEMA
        or context["schemaVersion"] != CONTEXT_SCHEMA
        or bundle["schemaVersion"] != BUNDLE_SCHEMA
        or worksheet["schemaVersion"] != WORKSHEET_SCHEMA
        or policy["schemaVersion"] != POLICY_SCHEMA
        or any(document["holdoutCohort"] != receipt["holdoutCohort"] for document in (contract, blind, context, bundle, worksheet, policy))
        or contract["effectId"] != "sustained_flame_3d"
        or contract["answerDisclosure"] != "NONE"
        or contract["frameTable"] != expected_frame_table
        or policy["frameTable"] != expected_frame_table
        or policy["seedPolicy"] != "operator-fixed canonical uint32 + frozen xor-derived robustness pair/v1"
        or blind["freezeStatus"] != "FROZEN_FOR_BLIND_REVIEW"
        or context["freezeStatus"] != "FROZEN_FOR_BLIND_REVIEW"
        or bundle["freezeStatus"] != "FROZEN_FOR_BLIND_REVIEW"
        or worksheet["reviewStatus"] != "UNREVIEWED"
        or context["captureToolHash"] != current_capture_tool_hash
    ):
        raise CalibrationValidationError("Projection document version, cohort, or frozen protocol identity is inconsistent")
    profile_template = policy["profileTemplateWithoutSeeds"]
    if not isinstance(profile_template, dict) or not profile_template or "canonicalSeed" in profile_template or "robustnessSeeds" in profile_template:
        raise CalibrationValidationError("Capture Profile policy does not separate cohort policy from per-sample seed instances")
    _validate_no_answer(contract)
    _validate_no_answer(blind)

    assignments = contract["assignments"]
    instances = policy["instances"]
    blind_entries = blind["samples"]
    bundle_entries = bundle["samples"]
    rows = worksheet["rows"]
    if any(not isinstance(items, list) or len(items) != expected_count for items in (assignments, instances, blind_entries, bundle_entries, rows)):
        raise CalibrationValidationError("Projected cohort does not have the exact reduced/full sample count")

    assignment_map: dict[str, list[str]] = {}
    for assignment in assignments:
        _require_exact(assignment, {"sampleId", "designRequirementIds", "criterion"}, "review assignment")
        sample_id = assignment["sampleId"]
        requirement_ids = assignment["designRequirementIds"]
        if (
            not isinstance(sample_id, str)
            or not SAMPLE_RE.fullmatch(sample_id)
            or sample_id in assignment_map
            or not isinstance(requirement_ids, list)
            or len(requirement_ids) != 1
            or not isinstance(requirement_ids[0], str)
            or not REQ_RE.fullmatch(requirement_ids[0])
            or not isinstance(assignment["criterion"], str)
            or not assignment["criterion"].strip()
        ):
            raise CalibrationValidationError("Blind review assignment is duplicate, malformed, or non-opaque")
        assignment_map[sample_id] = requirement_ids

    instance_map: dict[str, str] = {}
    for instance in instances:
        _require_exact(instance, {"sampleId", "captureProfileInstanceHash"}, "Capture Profile instance")
        sample_id = instance["sampleId"]
        if sample_id in instance_map or sample_id not in assignment_map:
            raise CalibrationValidationError("Capture Profile instance sample set is duplicate or foreign")
        instance_map[sample_id] = _require_hash(instance["captureProfileInstanceHash"], "Capture Profile instance hash")
    if set(instance_map) != set(assignment_map) or len(set(instance_map.values())) != expected_count:
        raise CalibrationValidationError("Capture Profile instances are incomplete or replayed across samples")

    expected_ids = set(assignment_map)
    if [entry.get("sampleId") for entry in blind_entries] != sorted(expected_ids, key=_blind_order):
        raise CalibrationValidationError("Blind manifest order is not the deterministic sealed shuffle")
    _require_direct_shape(
        root / "blind" / "evidence",
        {f"{sample_id}.evidence.json" for sample_id in expected_ids},
        set(),
        "blind evidence manifests",
    )
    _require_direct_shape(root / "blind" / "frames", set(), expected_ids, "blind frame roots")
    _require_direct_shape(root / "operator" / "evidence", set(), expected_ids, "operator evidence roots")

    evidence_hashes: dict[str, str] = {}
    evidence_records: dict[str, dict[str, Any]] = {}
    for entry in blind_entries:
        _require_exact(entry, {"sampleId", "designRequirementIds", "captureProfileInstanceHash", "evidenceManifest", "evidenceHash"}, "blind sample entry")
        sample_id = entry["sampleId"]
        if sample_id not in expected_ids or sample_id in evidence_hashes:
            raise CalibrationValidationError("Blind manifest sample set is duplicate or foreign")
        if entry["designRequirementIds"] != assignment_map[sample_id] or entry["captureProfileInstanceHash"] != instance_map[sample_id]:
            raise CalibrationValidationError("Blind manifest assignment or Capture Profile instance mismatch")
        if entry["evidenceManifest"] != f"evidence/{sample_id}.evidence.json":
            raise CalibrationValidationError("Blind evidence manifest path is not deterministic")
        evidence_path = root / "blind" / entry["evidenceManifest"]
        evidence = _load_json(evidence_path, "blind evidence")
        _require_exact(
            evidence,
            {"schemaVersion", "sampleId", "designRequirementIds", "reviewContractHash", "captureProfilePolicyHash", "captureProfileInstanceHash", "frameTableHash", "sourceIdentity", "captureMetadata", "frames", "evidenceHash"},
            "blind evidence",
        )
        _canonical_hash_matches(evidence, "evidenceHash", "blind evidence")
        _validate_no_answer(evidence)
        if (
            evidence["schemaVersion"] != EVIDENCE_SCHEMA
            or evidence["sampleId"] != sample_id
            or evidence["evidenceHash"] != entry["evidenceHash"]
            or evidence["reviewContractHash"] != contract["contractHash"]
            or evidence["captureProfilePolicyHash"] != policy["policyHash"]
            or evidence["captureProfileInstanceHash"] != instance_map[sample_id]
            or evidence["frameTableHash"] != context["frameTableHash"]
        ):
            raise CalibrationValidationError("Blind manifest/evidence identity mismatch")
        if evidence["designRequirementIds"] != assignment_map[sample_id]:
            raise CalibrationValidationError("Blind design requirement assignment mismatch")
        source_identity = _require_exact(evidence["sourceIdentity"], {"sceneHash", "prefabManifestHash", "buildHash", "captureToolHash"}, "blind source identity")
        if any(source_identity[field] != context[field] for field in ("sceneHash", "prefabManifestHash", "buildHash", "captureToolHash")):
            raise CalibrationValidationError("Blind evidence source identity differs from frozen review context")
        capture_metadata = _require_exact(evidence["captureMetadata"], {"declaredHash", "actualHash"}, "blind capture metadata identity")
        _require_hash(capture_metadata["declaredHash"], "blind capture metadata declared hash")
        _require_hash(capture_metadata["actualHash"], "blind capture metadata actual hash")
        if not isinstance(evidence["frames"], list) or len(evidence["frames"]) != 24:
            raise CalibrationValidationError("Blind evidence must declare all three-by-eight frame slots")
        seen_pairs: set[tuple[int, int]] = set()
        present_names: dict[int, set[str]] = {0: set(), 1: set(), 2: set()}
        for frame in evidence["frames"]:
            _require_exact(frame, {"seedOrdinal", "frameIndex", "stateRef", "beauty"}, "blind evidence frame")
            pair = (frame["seedOrdinal"], frame["frameIndex"])
            if pair not in {(ordinal, index) for ordinal in range(3) for index in FRAME_TABLE} or pair in seen_pairs:
                raise CalibrationValidationError("Blind evidence frame set is foreign or duplicate")
            seen_pairs.add(pair)
            if not isinstance(frame["stateRef"], str) or not frame["stateRef"].strip():
                raise CalibrationValidationError("Blind evidence frame state is missing")
            beauty = _require_exact(frame["beauty"], {"availability", "file", "declaredHash", "actualHash"}, "blind Beauty slot")
            _require_hash(beauty["declaredHash"], "blind Beauty declared hash")
            ordinal, frame_index = pair
            expected_relative = f"frames/{sample_id}/seed_{ordinal}/frame_{frame_index:05d}_beauty.png"
            if beauty["availability"] == "present":
                if beauty["file"] != expected_relative:
                    raise CalibrationValidationError("Blind Beauty path is not the deterministic frame slot")
                actual_hash = _require_hash(beauty["actualHash"], "blind Beauty actual hash")
                path = root / "blind" / expected_relative
                if not path.is_file() or _is_link(path) or _sha256_file(path) != actual_hash:
                    raise CalibrationValidationError("Blind Beauty file/hash identity mismatch")
                _validate_png(path, actual_hash)
                present_names[ordinal].add(Path(expected_relative).name)
            elif beauty["availability"] == "missing":
                if beauty["file"] is not None or beauty["actualHash"] is not None or (root / "blind" / expected_relative).exists():
                    raise CalibrationValidationError("Blind missing Beauty slot unexpectedly has a file or hash")
            else:
                raise CalibrationValidationError("Blind Beauty availability is invalid")
        if seen_pairs != {(ordinal, index) for ordinal in range(3) for index in FRAME_TABLE}:
            raise CalibrationValidationError("Blind evidence does not contain the fixed three-by-eight table")
        _require_direct_shape(root / "blind" / "frames" / sample_id, set(), {"seed_0", "seed_1", "seed_2"}, f"blind frame sample {sample_id}")
        for ordinal in range(3):
            _require_direct_shape(root / "blind" / "frames" / sample_id / f"seed_{ordinal}", present_names[ordinal], set(), f"blind frame seed {sample_id}:{ordinal}")
        evidence_hashes[sample_id] = entry["evidenceHash"]
        evidence_records[sample_id] = evidence
    if set(evidence_hashes) != expected_ids:
        raise CalibrationValidationError("Blind evidence sample set is incomplete")

    bundle_map: dict[str, dict[str, Any]] = {}
    for record in bundle_entries:
        _require_exact(
            record,
            {"sampleId", "commandHash", "completionHash", "rawCaptureSealFileHash", "selectedEvidenceDerivationHash", "ledgerTailHash", "captureProfileInstanceHash", "blindEvidenceHash", "blindEvidenceManifest", "selectedCaptureMetadata", "semanticTelemetry", "diagnosticFrames"},
            "operator evidence sample",
        )
        sample_id = record["sampleId"]
        if sample_id not in expected_ids or sample_id in bundle_map:
            raise CalibrationValidationError("Operator evidence sample set is duplicate or foreign")
        for field in ("commandHash", "completionHash", "rawCaptureSealFileHash", "ledgerTailHash"):
            _require_hash(record[field], f"operator evidence {field}")
        if record["selectedEvidenceDerivationHash"] is not None:
            _require_hash(record["selectedEvidenceDerivationHash"], "selected invalid-evidence derivation hash")
        if (
            record["captureProfileInstanceHash"] != instance_map[sample_id]
            or record["blindEvidenceHash"] != evidence_hashes[sample_id]
            or record["blindEvidenceManifest"] != f"blind/evidence/{sample_id}.evidence.json"
        ):
            raise CalibrationValidationError("Operator evidence does not bind its blind sample")
        selected_metadata = _require_exact(record["selectedCaptureMetadata"], {"file", "declaredHash", "actualHash"}, "selected capture metadata")
        expected_metadata_file = f"operator/evidence/{sample_id}/capture-metadata.json"
        if selected_metadata["file"] != expected_metadata_file:
            raise CalibrationValidationError("Operator metadata path is not deterministic")
        metadata_path = root / expected_metadata_file
        if not metadata_path.is_file() or _is_link(metadata_path) or _sha256_file(metadata_path) != selected_metadata["actualHash"]:
            raise CalibrationValidationError("Operator selected metadata file/hash identity mismatch")
        if selected_metadata["declaredHash"] != evidence_records[sample_id]["captureMetadata"]["declaredHash"] or selected_metadata["actualHash"] != evidence_records[sample_id]["captureMetadata"]["actualHash"]:
            raise CalibrationValidationError("Operator/blind metadata identity mismatch")
        telemetry = _require_exact(record["semanticTelemetry"], {"file", "sha256"}, "operator semantic telemetry")
        expected_telemetry_file = f"operator/evidence/{sample_id}/semantic-telemetry.json"
        telemetry_path = root / expected_telemetry_file
        if telemetry["file"] != expected_telemetry_file or not telemetry_path.is_file() or _is_link(telemetry_path) or _sha256_file(telemetry_path) != telemetry["sha256"]:
            raise CalibrationValidationError("Operator semantic telemetry file/hash identity mismatch")
        diagnostic_frames = record["diagnosticFrames"]
        if not isinstance(diagnostic_frames, list) or len(diagnostic_frames) != 24:
            raise CalibrationValidationError("Operator evidence must retain all 24 effect-only diagnostics")
        diagnostic_pairs: set[tuple[int, int]] = set()
        for diagnostic in diagnostic_frames:
            _require_exact(diagnostic, {"seedOrdinal", "frameIndex", "file", "declaredHash", "actualHash"}, "operator effect-only frame")
            pair = (diagnostic["seedOrdinal"], diagnostic["frameIndex"])
            if pair not in {(ordinal, index) for ordinal in range(3) for index in FRAME_TABLE} or pair in diagnostic_pairs:
                raise CalibrationValidationError("Operator effect-only frame set is foreign or duplicate")
            diagnostic_pairs.add(pair)
            ordinal, frame_index = pair
            expected_file = f"operator/evidence/{sample_id}/effect-only/seed_{ordinal}/frame_{frame_index:05d}_effect-only.png"
            path = root / expected_file
            if diagnostic["file"] != expected_file or not path.is_file() or _is_link(path) or _sha256_file(path) != diagnostic["actualHash"]:
                raise CalibrationValidationError("Operator effect-only file/hash identity mismatch")
            _validate_png(path, diagnostic["actualHash"])
            _require_hash(diagnostic["declaredHash"], "operator effect-only declared hash")
        if diagnostic_pairs != {(ordinal, index) for ordinal in range(3) for index in FRAME_TABLE}:
            raise CalibrationValidationError("Operator effect-only frame set is incomplete")
        _require_direct_shape(root / "operator" / "evidence" / sample_id, {"capture-metadata.json", "semantic-telemetry.json"}, {"effect-only"}, f"operator sample {sample_id}")
        _require_direct_shape(root / "operator" / "evidence" / sample_id / "effect-only", set(), {"seed_0", "seed_1", "seed_2"}, f"operator effect-only root {sample_id}")
        for ordinal in range(3):
            _require_direct_shape(
                root / "operator" / "evidence" / sample_id / "effect-only" / f"seed_{ordinal}",
                {f"frame_{index:05d}_effect-only.png" for index in FRAME_TABLE},
                set(),
                f"operator effect-only seed {sample_id}:{ordinal}",
            )
        missing_beauty = sum(frame["beauty"]["availability"] == "missing" for frame in evidence_records[sample_id]["frames"])
        metadata_mismatch = selected_metadata["declaredHash"] != selected_metadata["actualHash"]
        if record["selectedEvidenceDerivationHash"] is None:
            if missing_beauty != 0 or metadata_mismatch:
                raise CalibrationValidationError("Ordinary projected evidence contains an invalid-evidence mutation")
        elif not ((missing_beauty == 1 and not metadata_mismatch) or (missing_beauty == 0 and metadata_mismatch)):
            raise CalibrationValidationError("Derived invalid evidence is not exactly one missing Beauty or one metadata mismatch")
        bundle_map[sample_id] = record
    if set(bundle_map) != expected_ids:
        raise CalibrationValidationError("Operator evidence sample set is incomplete")

    if {row.get("sampleId") for row in rows if isinstance(row, dict)} != set(evidence_hashes):
        raise CalibrationValidationError("Human worksheet sample set differs from blind evidence")
    for row in rows:
        _require_exact(row, {"sampleId", "designRequirementIds", "evidenceHash", "groundTruthRoute", "perRequirement", "visuallyObservable", "eligibleForVisualMetrics", "reviewer", "adjudicationNotes"}, "human worksheet row")
        if any(row.get(field) is not None for field in ("groundTruthRoute", "visuallyObservable", "eligibleForVisualMetrics", "reviewer")):
            raise CalibrationValidationError("Human worksheet contains an adjudication answer")
        if row.get("adjudicationNotes") != "" or row.get("designRequirementIds") != assignment_map.get(row.get("sampleId")):
            raise CalibrationValidationError("Human worksheet assignment or blank state is invalid")
        per_requirement = row.get("perRequirement")
        if (
            not isinstance(per_requirement, list)
            or len(per_requirement) != 1
            or set(per_requirement[0]) != {"designRequirementId", "state"}
            or per_requirement[0].get("designRequirementId") != assignment_map[row["sampleId"]][0]
            or per_requirement[0].get("state") is not None
        ):
            raise CalibrationValidationError("Human worksheet contains per-requirement answers")
        if row.get("evidenceHash") != evidence_hashes.get(row.get("sampleId")):
            raise CalibrationValidationError("Human worksheet evidence identity mismatch")
    if any((root / name).exists() for name in ("calibration-labels.json", "metrics.json", "qa-reports")):
        raise CalibrationValidationError("Projection contains a forbidden label, metric, or QA output")
    for field, expected in (
        ("captureProfilePolicyHash", policy["policyHash"]),
        ("reviewContractHash", contract["contractHash"]),
        ("reviewContextHash", context["contextHash"]),
        ("blindManifestHash", blind["manifestHash"]),
        ("operatorEvidenceBundleHash", bundle["bundleHash"]),
        ("humanWorksheetHash", worksheet["worksheetHash"]),
    ):
        if receipt[field] != expected:
            raise CalibrationValidationError(f"Projection receipt does not bind {field}")
    if (
        blind["reviewContractHash"] != contract["contractHash"]
        or blind["captureProfilePolicyHash"] != policy["policyHash"]
        or blind["reviewContextHash"] != context["contextHash"]
        or context["reviewContractHash"] != contract["contractHash"]
        or context["captureProfilePolicyHash"] != policy["policyHash"]
        or context["operatorLedgerHash"] != receipt["operatorLedgerHash"]
        or bundle["operatorLedgerHash"] != receipt["operatorLedgerHash"]
        or bundle["commandSetHash"] != receipt["commandSetHash"]
        or bundle["blindManifestHash"] != blind["manifestHash"]
        or bundle["reviewContextHash"] != context["contextHash"]
        or bundle["captureProfilePolicyHash"] != policy["policyHash"]
        or worksheet["blindManifestHash"] != blind["manifestHash"]
        or worksheet["reviewContractHash"] != contract["contractHash"]
    ):
        raise CalibrationValidationError("Projected receipt/context/manifest/bundle/worksheet cross-binding failed")
    return receipt


def _default_paths(repo_root: Path) -> dict[str, Path]:
    qa = repo_root / "docs" / "skills" / "unity-vfx-visual-qa"
    return {
        "qa_prompt": qa / "AGENT.md",
        "image_input_strategy": qa / "review-protocol.md",
        "three_state_rules": qa / "review-protocol.md",
        "aggregation_rules": qa / "review-protocol.md",
        "visual_review_schema": qa / "schemas" / "vfx-visual-review.schema.json",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    project = commands.add_parser("project", help="verify one exact formal cohort and write artifacts atomically")
    project.add_argument("--capture-root", type=Path, required=True)
    project.add_argument("--fixture-root", type=Path, required=True)
    project.add_argument("--output", type=Path, required=True, help="write-once destination named artifacts")
    project.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    project.add_argument("--model-version-id", required=True)
    project.add_argument("--qa-prompt", type=Path)
    project.add_argument("--image-input-strategy", type=Path)
    project.add_argument("--three-state-rules", type=Path)
    project.add_argument("--aggregation-rules", type=Path)
    project.add_argument("--visual-review-schema", type=Path)
    project.add_argument("--reduced-metrics", type=Path)
    verify = commands.add_parser("verify", help="verify an existing projected artifacts directory")
    verify.add_argument("--artifacts", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "verify":
            print(json.dumps(verify_projection(args.artifacts), indent=2))
            return 0
        defaults = _default_paths(_secure_existing_directory(args.repo_root, "repository root"))
        receipt = project_formal_capture(
            ProjectionInputs(
                capture_root=args.capture_root,
                fixture_root=args.fixture_root,
                output=args.output,
                qa_prompt=args.qa_prompt or defaults["qa_prompt"],
                image_input_strategy=args.image_input_strategy or defaults["image_input_strategy"],
                three_state_rules=args.three_state_rules or defaults["three_state_rules"],
                aggregation_rules=args.aggregation_rules or defaults["aggregation_rules"],
                visual_review_schema=args.visual_review_schema or defaults["visual_review_schema"],
                model_version_id=args.model_version_id,
                reduced_metrics=args.reduced_metrics,
            )
        )
        print(json.dumps(receipt, indent=2))
        return 0
    except CalibrationValidationError as exc:
        parser.error(str(exc))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
