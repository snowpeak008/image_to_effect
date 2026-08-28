from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator


REPOSITORY = Path(__file__).resolve().parents[3]
SCHEMA_PATHS = {
    "legacy_s0b": REPOSITORY / "docs/schemas/w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json",
    "legacy_s3": REPOSITORY / "docs/schemas/w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json",
    "revisioned_s3": REPOSITORY / "docs/schemas/w24-s5-evidence-revision-revisioned-s3-v1.schema.json",
}
TOP_LEVEL_FIELDS = {
    "schema",
    "descriptorStatus",
    "writer",
    "effectId",
    "candidateId",
    "candidateRevision",
    "contractRevision",
    "evidenceRevision",
    "candidate",
    "rawCapture",
    "captureTool",
    "evaluationInput",
    "predecessor",
    "selfHashEncoding",
    "selfHash",
}


def _hash(nibble: str) -> str:
    return f"sha256:{nibble * 64}"


def _source_snapshot(root: str, area: str, ordinal: int = 0) -> dict[str, Any]:
    return {
        "ordinal": ordinal,
        "sourcePath": f"project/Packages/com.vfxcomposer.unity/Editor/W24/S5/{area}.cs",
        "sourceSha256": _hash("1"),
        "snapshotPath": f"{root}/snapshots/{area.lower()}/sources/{ordinal:04d}.source",
        "snapshotFileHash": _hash("2"),
    }


def _writer(candidate_root: str, evidence_revision: int, schema_filename: str) -> dict[str, Any]:
    evidence_root = f"{candidate_root}/evidence/E{evidence_revision}"
    root = f"{evidence_root}/snapshots/writer"
    return {
        "writerId": "w24_s5_evidence_writer",
        "writerVersion": "w24-s5-evidence-writer/1.0",
        "bundleSnapshotPath": f"{root}/writer.bundle.json",
        "bundleSnapshotFileHash": _hash("3"),
        "bundleTypedHash": _hash("4"),
        "sourceSnapshots": [_source_snapshot(evidence_root, "writer", 0)],
        "sourceSetTypedHash": _hash("5"),
        "descriptorSchemaSnapshotPath": f"{evidence_root}/snapshots/schema/{schema_filename}.schema.json",
        "descriptorSchemaSnapshotFileHash": _hash("6"),
    }


def _capture_tool(candidate_root: str, evidence_revision: int, tool_version: str) -> dict[str, Any]:
    evidence_root = f"{candidate_root}/evidence/E{evidence_revision}"
    return {
        "toolVersion": tool_version,
        "bundleSnapshotPath": f"{evidence_root}/snapshots/capture-tool/capture-tool.bundle.json",
        "bundleSnapshotFileHash": _hash("7"),
        "bundleCanonicalHash": _hash("8"),
        "sourceSnapshots": [_source_snapshot(evidence_root, "capture-tool", 0)],
        "sourceSetTypedHash": _hash("9"),
    }


def _raw(root: str, *, legacy: bool) -> dict[str, Any]:
    return {
        "layout": "LEGACY_C0_FLAT_E1" if legacy else "EVIDENCE_REVISION_RAW",
        "root": root,
        "captureMetadataPath": f"{root}/capture-metadata.json",
        "captureMetadataFileHash": _hash("a"),
        "evidenceSealPath": f"{root}/evidence-seal.json",
        "evidenceSealFileHash": _hash("b"),
        "evidenceSealHash": _hash("c"),
        "evidenceLockPath": f"{root}/evidence-lock.json",
        "evidenceLockFileHash": _hash("d"),
        "diagnosticPassManifestPath": f"{root}/diagnostic-pass-manifest.json",
        "diagnosticPassManifestFileHash": _hash("e"),
        "artifactCount": 12,
        "totalBytes": 4096,
        "fileSetTypedHash": _hash("f"),
    }


def _predecessor(candidate_root: str, evidence_revision: int) -> dict[str, Any]:
    if evidence_revision == 1:
        return {"kind": "NONE"}
    root = f"{candidate_root}/evidence/E1"
    return {
        "kind": "EVIDENCE_INVALID",
        "evidenceRevision": 1,
        "descriptorPath": f"{root}/evidence-revision.json",
        "descriptorFileHash": _hash("0"),
        "gateReportPath": f"{root}/terminal/machine-gate-report.json",
        "gateReportFileHash": _hash("1"),
        "evidenceInvalidReceiptPath": f"{root}/terminal/evidence-invalid-receipt.json",
        "evidenceInvalidReceiptFileHash": _hash("2"),
    }


def _legacy_candidate(effect: str) -> dict[str, Any]:
    root = f"docs/vfx-candidates/{effect}/C0"
    return {
        "receiptPath": f"{root}/candidate-receipt.json",
        "receiptFileHash": _hash("3"),
        "receiptVersion": "w24-candidate/1.0",
        "contractPath": f"{root}/design-contract.json",
        "contractFileHash": _hash("4"),
        "contractHash": _hash("5"),
        "pendingTracePath": f"{root}/implementation-trace.json",
        "pendingTraceFileHash": _hash("6"),
        "bootstrapManifestSnapshotPath": f"{root}/bootstrap-manifest.json",
        "bootstrapManifestSnapshotFileHash": _hash("7"),
        "buildHash": _hash("8"),
        "captureProfileHash": _hash("9"),
        "runtimeEntryPath": f"Assets/VFX/Generated/{effect}/VFX_{effect}.prefab",
        "runtimeEntryGuid": "0123456789abcdef0123456789abcdef",
        "previewScenePath": "Assets/VFX/Preview/W24S3/Preview.unity",
        "previewSceneFileHash": _hash("a"),
    }


def _revisioned_candidate(effect: str, contract_revision: int, candidate_id: str) -> dict[str, Any]:
    root = f"docs/vfx-candidates/{effect}/R{contract_revision}/{candidate_id}"
    asset_root = f"Assets/VFX/Candidates/R{contract_revision}/{candidate_id}/{effect}"
    return {
        "receiptPath": f"{root}/candidate-receipt.json",
        "receiptFileHash": _hash("3"),
        "receiptVersion": "w24-candidate-revision/2.0",
        "contractRevisionNamespace": f"R{contract_revision}",
        "previousCandidateReceiptPath": f"docs/vfx-candidates/{effect}/C0/candidate-receipt.json",
        "previousCandidateReceiptFileHash": _hash("4"),
        "contractPath": f"{root}/design-contract.json",
        "contractFileHash": _hash("5"),
        "contractHash": _hash("6"),
        "pendingTracePath": f"{root}/implementation-trace.json",
        "pendingTraceFileHash": _hash("7"),
        "productionManifestSnapshotPath": f"{root}/production-manifest.json",
        "productionManifestSnapshotFileHash": _hash("8"),
        "productionManifestInputFileHash": _hash("9"),
        "buildHash": _hash("a"),
        "captureProfileHash": _hash("b"),
        "ownedOutputRoot": asset_root,
        "runtimeEntryPath": f"{asset_root}/VFX_{effect}.prefab",
        "runtimeEntryGuid": "0123456789abcdef0123456789abcdef",
        "previewScenePath": f"{asset_root}/Preview/VFXPREVIEW_{effect}.unity",
        "previewSceneFileHash": _hash("c"),
    }


def _s0b_evaluation(raw_root: str) -> dict[str, Any]:
    diagnostics = f"{raw_root}/diagnostics"
    return {
        "schema": "w24-s5-eval-input-s0b-legacy/1",
        "operatorCommandPath": f"{diagnostics}/operator-command.json",
        "operatorCommandFileHash": _hash("0"),
        "semanticTelemetryPath": f"{diagnostics}/semantic-telemetry.json",
        "semanticTelemetryFileHash": _hash("1"),
        "receiverOffPath": f"{diagnostics}/receiver-light-off.png",
        "receiverOffFileHash": _hash("2"),
        "receiverOnPath": f"{diagnostics}/receiver-light-on.png",
        "receiverOnFileHash": _hash("3"),
        "receiverSummaryPath": f"{diagnostics}/receiver-light-ab.json",
        "receiverSummaryFileHash": _hash("4"),
        "replayPolicyVersion": "w24-s0b-replay-policy/1.0",
    }


def _s3_evaluation(candidate_root: str, raw_root: str, evidence_revision: int) -> dict[str, Any]:
    diagnostics = f"{raw_root}/diagnostics"
    evaluation = f"{candidate_root}/evidence/E{evidence_revision}/snapshots/evaluation"
    return {
        "schema": "w24-s5-eval-input-s3-render-metrics/1",
        "metricsInputPath": f"{diagnostics}/metrics-input.json",
        "metricsInputFileHash": _hash("0"),
        "capturedMetricsReportPath": f"{diagnostics}/metrics-report.json",
        "capturedMetricsReportFileHash": _hash("1"),
        "metricsToolSnapshotPath": f"{evaluation}/render_metrics.py",
        "metricsToolSnapshotFileHash": _hash("2"),
        "metricsEnvironmentPath": f"{evaluation}/metrics-environment.json",
        "metricsEnvironmentFileHash": _hash("3"),
        "requiredEvidenceMatrixHash": _hash("4"),
        "typedRawSetHash": _hash("5"),
    }


def legacy_s0b_descriptor(evidence_revision: int = 1) -> dict[str, Any]:
    effect = "sustained_flame_3d"
    candidate_root = f"docs/vfx-candidates/{effect}/C0"
    raw_root = f"artifacts/vfx-evidence/{effect}/C0" if evidence_revision == 1 else f"artifacts/vfx-evidence/{effect}/C0/E2/raw"
    return {
        "schema": "w24-s5-evidence-revision-legacy-c0-s0b/1",
        "descriptorStatus": "RAW_CAPTURE_SEALED",
        "writer": _writer(candidate_root, evidence_revision, "w24-s5-evidence-revision-legacy-c0-s0b-v1"),
        "effectId": effect,
        "candidateId": "C0",
        "candidateRevision": 0,
        "contractRevision": 17,
        "evidenceRevision": evidence_revision,
        "candidate": _legacy_candidate(effect),
        "rawCapture": _raw(raw_root, legacy=evidence_revision == 1),
        "captureTool": _capture_tool(candidate_root, evidence_revision, "w24-s0b-formal-capture/1.2.12"),
        "evaluationInput": _s0b_evaluation(raw_root),
        "predecessor": _predecessor(candidate_root, evidence_revision),
        "selfHashEncoding": "w24-typed-binary-v1",
        "selfHash": _hash("6"),
    }


def legacy_s3_descriptor(evidence_revision: int = 1) -> dict[str, Any]:
    effect = "w24_moving_projectile_trail"
    candidate_root = f"docs/vfx-candidates/{effect}/C0"
    raw_root = f"artifacts/vfx-evidence/{effect}/C0" if evidence_revision == 1 else f"artifacts/vfx-evidence/{effect}/C0/E2/raw"
    return {
        "schema": "w24-s5-evidence-revision-legacy-c0-s3/1",
        "descriptorStatus": "RAW_CAPTURE_SEALED",
        "writer": _writer(candidate_root, evidence_revision, "w24-s5-evidence-revision-legacy-c0-s3-v1"),
        "effectId": effect,
        "candidateId": "C0",
        "candidateRevision": 0,
        "contractRevision": 26,
        "evidenceRevision": evidence_revision,
        "candidate": _legacy_candidate(effect),
        "rawCapture": _raw(raw_root, legacy=evidence_revision == 1),
        "captureTool": _capture_tool(candidate_root, evidence_revision, "w24-s3-capture/3.6"),
        "evaluationInput": _s3_evaluation(candidate_root, raw_root, evidence_revision),
        "predecessor": _predecessor(candidate_root, evidence_revision),
        "selfHashEncoding": "w24-typed-binary-v1",
        "selfHash": _hash("6"),
    }


def revisioned_s3_descriptor(
    evidence_revision: int = 1,
    candidate_id: str = "C1",
    contract_revision: int = 27,
) -> dict[str, Any]:
    effect = "w24_moving_projectile_trail"
    candidate_revision = int(candidate_id[1:])
    candidate_root = f"docs/vfx-candidates/{effect}/R{contract_revision}/{candidate_id}"
    raw_root = f"artifacts/vfx-evidence/{effect}/R{contract_revision}/{candidate_id}/E{evidence_revision}/raw"
    return {
        "schema": "w24-s5-evidence-revision-revisioned-s3/1",
        "descriptorStatus": "RAW_CAPTURE_SEALED",
        "writer": _writer(candidate_root, evidence_revision, "w24-s5-evidence-revision-revisioned-s3-v1"),
        "effectId": effect,
        "candidateId": candidate_id,
        "candidateRevision": candidate_revision,
        "contractRevision": contract_revision,
        "evidenceRevision": evidence_revision,
        "candidate": _revisioned_candidate(effect, contract_revision, candidate_id),
        "rawCapture": _raw(raw_root, legacy=False),
        "captureTool": _capture_tool(candidate_root, evidence_revision, "w24-s3-capture/3.6"),
        "evaluationInput": _s3_evaluation(candidate_root, raw_root, evidence_revision),
        "predecessor": _predecessor(candidate_root, evidence_revision),
        "selfHashEncoding": "w24-typed-binary-v1",
        "selfHash": _hash("6"),
    }


class W24S5EvidenceRevisionSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.schemas = {name: json.loads(path.read_text(encoding="utf-8")) for name, path in SCHEMA_PATHS.items()}
        cls.validators = {name: Draft202012Validator(schema) for name, schema in cls.schemas.items()}

    def assert_valid(self, schema_name: str, value: dict[str, Any]) -> None:
        errors = sorted(self.validators[schema_name].iter_errors(value), key=lambda error: list(error.absolute_path))
        self.assertEqual([], errors, "\n".join(error.message for error in errors))

    def assert_invalid(self, schema_name: str, value: dict[str, Any]) -> None:
        self.assertTrue(list(self.validators[schema_name].iter_errors(value)))

    def test_schemas_are_draft_2020_12_and_every_object_is_exact(self) -> None:
        def walk(node: Any) -> None:
            if isinstance(node, dict):
                if node.get("type") == "object":
                    self.assertIs(node.get("additionalProperties"), False)
                    self.assertEqual(set(node.get("properties", {})), set(node.get("required", [])))
                self.assertNotIn("anyOf", node)
                node_type = node.get("type")
                self.assertFalse(isinstance(node_type, list), "type unions are forbidden")
                for value in node.values():
                    walk(value)
            elif isinstance(node, list):
                for value in node:
                    walk(value)

        for schema in self.schemas.values():
            Draft202012Validator.check_schema(schema)
            self.assertEqual("https://json-schema.org/draft/2020-12/schema", schema["$schema"])
            self.assertEqual(TOP_LEVEL_FIELDS, set(schema["required"]))
            self.assertEqual(TOP_LEVEL_FIELDS, set(schema["properties"]))
            self.assertEqual(1000000, schema["$defs"]["positiveRevision"]["maximum"])
            self.assertEqual(512, schema["$defs"]["rawCaptureBase"]["properties"]["artifactCount"]["maximum"])
            self.assertEqual(1073741824, schema["$defs"]["rawCaptureBase"]["properties"]["totalBytes"]["maximum"])
            self.assertEqual(96, schema["$defs"]["boundedToken"]["maxLength"])
            self.assertEqual(96, schema["$defs"]["versionToken"]["maxLength"])
            self.assertNotIn('"null"', json.dumps(schema, sort_keys=True))
            walk(schema)

    def test_writer_shaped_success_descriptors_validate_both_legacy_routes(self) -> None:
        self.assert_valid("legacy_s0b", legacy_s0b_descriptor())
        self.assert_valid("legacy_s3", legacy_s3_descriptor())

    def test_descriptor_and_version_tokens_accept_96_and_reject_97_characters(self) -> None:
        descriptor_96 = "A" * 96
        descriptor_97 = "A" * 97
        version_96 = "v" * 94 + "/1"
        version_97 = "v" * 95 + "/1"
        for schema_name, factory in (("legacy_s0b", legacy_s0b_descriptor), ("legacy_s3", legacy_s3_descriptor)):
            accepted = factory()
            accepted["writer"]["writerId"] = descriptor_96
            accepted["writer"]["writerVersion"] = version_96
            accepted["captureTool"]["toolVersion"] = version_96
            self.assert_valid(schema_name, accepted)
            for path, value in (
                (("writer", "writerId"), descriptor_97),
                (("writer", "writerVersion"), version_97),
                (("captureTool", "toolVersion"), version_97),
            ):
                rejected = copy.deepcopy(accepted)
                rejected[path[0]][path[1]] = value
                self.assert_invalid(schema_name, rejected)

    def test_legacy_phase_a_shapes_accept_only_e1_while_revisioned_schema_retains_e2(self) -> None:
        self.assert_valid("legacy_s0b", legacy_s0b_descriptor(1))
        self.assert_valid("legacy_s3", legacy_s3_descriptor(1))
        self.assert_invalid("legacy_s0b", legacy_s0b_descriptor(2))
        self.assert_invalid("legacy_s3", legacy_s3_descriptor(2))
        for revision in (1, 2):
            self.assert_valid("revisioned_s3", revisioned_s3_descriptor(revision, "C1"))
        self.assert_valid("revisioned_s3", revisioned_s3_descriptor(1, "C2"))

    def test_unknown_fields_are_rejected_at_top_and_nested_levels(self) -> None:
        top = legacy_s0b_descriptor()
        top["unexpected"] = True
        self.assert_invalid("legacy_s0b", top)
        nested = legacy_s3_descriptor()
        nested["writer"]["callerVerdict"] = "MACHINE_FAIL"
        self.assert_invalid("legacy_s3", nested)
        source = revisioned_s3_descriptor()
        source["captureTool"]["sourceSnapshots"][0]["extra"] = 1
        self.assert_invalid("revisioned_s3", source)

    def test_missing_fields_are_rejected_at_top_and_nested_levels(self) -> None:
        top = legacy_s0b_descriptor()
        del top["selfHash"]
        self.assert_invalid("legacy_s0b", top)
        candidate = legacy_s3_descriptor()
        del candidate["candidate"]["receiptPath"]
        self.assert_invalid("legacy_s3", candidate)
        evaluation = revisioned_s3_descriptor()
        del evaluation["evaluationInput"]["typedRawSetHash"]
        self.assert_invalid("revisioned_s3", evaluation)

    def test_hashes_require_prefixed_lowercase_sha256(self) -> None:
        for schema_name, value in (
            ("legacy_s0b", legacy_s0b_descriptor()),
            ("legacy_s3", legacy_s3_descriptor()),
            ("revisioned_s3", revisioned_s3_descriptor()),
        ):
            raw = copy.deepcopy(value)
            raw["selfHash"] = "0" * 64
            self.assert_invalid(schema_name, raw)
            uppercase = copy.deepcopy(value)
            uppercase["candidate"]["contractHash"] = "sha256:" + "A" * 64
            self.assert_invalid(schema_name, uppercase)
            short = copy.deepcopy(value)
            short["rawCapture"]["fileSetTypedHash"] = "sha256:00"
            self.assert_invalid(schema_name, short)

    def test_paths_are_relative_bounded_and_version_namespaced(self) -> None:
        traversal = legacy_s0b_descriptor()
        traversal["writer"]["sourceSnapshots"][0]["sourcePath"] = "project/../secret.cs"
        self.assert_invalid("legacy_s0b", traversal)
        backslash = legacy_s3_descriptor()
        backslash["candidate"]["runtimeEntryPath"] = "Assets\\VFX\\bad.prefab"
        self.assert_invalid("legacy_s3", backslash)
        unicode_path = legacy_s3_descriptor()
        unicode_path["writer"]["sourceSnapshots"][0]["sourcePath"] = "project/工具/source.cs"
        self.assert_invalid("legacy_s3", unicode_path)
        oversized_segment = legacy_s0b_descriptor()
        oversized_segment["writer"]["sourceSnapshots"][0]["sourcePath"] = "project/" + "a" * 129 + ".cs"
        self.assert_invalid("legacy_s0b", oversized_segment)
        wrong_raw_namespace = revisioned_s3_descriptor()
        wrong_raw_namespace["rawCapture"]["root"] = "artifacts/vfx-evidence/w24_moving_projectile_trail/C0"
        self.assert_invalid("revisioned_s3", wrong_raw_namespace)
        wrong_descriptor_namespace = revisioned_s3_descriptor(2)
        wrong_descriptor_namespace["predecessor"]["descriptorPath"] = "docs/vfx-candidates/w24_moving_projectile_trail/C0/evidence/E1/evidence-revision.json"
        self.assert_invalid("revisioned_s3", wrong_descriptor_namespace)
        old_writer_layout = legacy_s0b_descriptor()
        old_writer_layout["writer"]["bundleSnapshotPath"] = "docs/vfx-candidates/sustained_flame_3d/C0/evidence/E1/writer/evidence-revision-writer.bundle.json"
        self.assert_invalid("legacy_s0b", old_writer_layout)
        old_writer_source = legacy_s0b_descriptor()
        old_writer_source["writer"]["sourceSnapshots"][0]["snapshotPath"] = "docs/vfx-candidates/sustained_flame_3d/C0/evidence/E1/writer/sources/Writer.cs"
        self.assert_invalid("legacy_s0b", old_writer_source)
        old_schema_layout = legacy_s0b_descriptor()
        old_schema_layout["writer"]["descriptorSchemaSnapshotPath"] = "docs/vfx-candidates/sustained_flame_3d/C0/evidence/E1/writer/evidence-revision.schema.json"
        self.assert_invalid("legacy_s0b", old_schema_layout)
        swapped_schema_snapshot = legacy_s0b_descriptor()
        swapped_schema_snapshot["writer"]["descriptorSchemaSnapshotPath"] = "docs/vfx-candidates/sustained_flame_3d/C0/evidence/E1/snapshots/schema/w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json"
        self.assert_invalid("legacy_s0b", swapped_schema_snapshot)
        old_capture_layout = legacy_s3_descriptor()
        old_capture_layout["captureTool"]["bundleSnapshotPath"] = "docs/vfx-candidates/w24_moving_projectile_trail/C0/evidence/E1/capture-tool/capture-tool.bundle.json"
        self.assert_invalid("legacy_s3", old_capture_layout)
        old_capture_source = legacy_s3_descriptor()
        old_capture_source["captureTool"]["sourceSnapshots"][0]["snapshotPath"] = "docs/vfx-candidates/w24_moving_projectile_trail/C0/evidence/E1/capture-tool/sources/Capture.cs"
        self.assert_invalid("legacy_s3", old_capture_source)
        old_evaluation_layout = revisioned_s3_descriptor()
        old_evaluation_layout["evaluationInput"]["metricsToolSnapshotPath"] = "docs/vfx-candidates/w24_moving_projectile_trail/R27/C1/evidence/E1/evaluation/render_metrics.py"
        self.assert_invalid("revisioned_s3", old_evaluation_layout)
        old_environment_layout = revisioned_s3_descriptor()
        old_environment_layout["evaluationInput"]["metricsEnvironmentPath"] = "docs/vfx-candidates/w24_moving_projectile_trail/R27/C1/evidence/E1/evaluation/metrics-environment.json"
        self.assert_invalid("revisioned_s3", old_environment_layout)

    def test_revision_candidate_and_bounds_constraints_are_closed(self) -> None:
        invalid_revision = legacy_s0b_descriptor()
        invalid_revision["evidenceRevision"] = 3
        self.assert_invalid("legacy_s0b", invalid_revision)
        candidate_mismatch = revisioned_s3_descriptor(candidate_id="C1")
        candidate_mismatch["candidateRevision"] = 2
        self.assert_invalid("revisioned_s3", candidate_mismatch)
        test_only_version_alias = revisioned_s3_descriptor()
        test_only_version_alias["candidate"]["receiptVersion"] = "TEST_ONLY_TRANSACTION_INFRASTRUCTURE"
        self.assert_invalid("revisioned_s3", test_only_version_alias)
        ordinal = legacy_s3_descriptor()
        ordinal["captureTool"]["sourceSnapshots"][0]["ordinal"] = 128
        self.assert_invalid("legacy_s3", ordinal)
        maximums = revisioned_s3_descriptor(contract_revision=1000000)
        maximums["rawCapture"]["artifactCount"] = 512
        maximums["rawCapture"]["totalBytes"] = 1073741824
        self.assert_valid("revisioned_s3", maximums)
        revision_bound = legacy_s0b_descriptor()
        revision_bound["contractRevision"] = 1000001
        self.assert_invalid("legacy_s0b", revision_bound)
        artifact_bound = legacy_s3_descriptor()
        artifact_bound["rawCapture"]["artifactCount"] = 513
        self.assert_invalid("legacy_s3", artifact_bound)
        bytes_bound = revisioned_s3_descriptor()
        bytes_bound["rawCapture"]["totalBytes"] = 1073741825
        self.assert_invalid("revisioned_s3", bytes_bound)

    def test_revisioned_assets_require_candidate_isolation_root(self) -> None:
        generated = revisioned_s3_descriptor()
        generated["candidate"]["ownedOutputRoot"] = "Assets/VFX/Generated/w24_moving_projectile_trail"
        generated["candidate"]["runtimeEntryPath"] = "Assets/VFX/Generated/w24_moving_projectile_trail/VFX_w24_moving_projectile_trail.prefab"
        self.assert_invalid("revisioned_s3", generated)
        shared_preview = revisioned_s3_descriptor()
        shared_preview["candidate"]["previewScenePath"] = "Assets/VFX/Preview/W24S3/Preview.unity"
        self.assert_invalid("revisioned_s3", shared_preview)
        wrong_extensions = revisioned_s3_descriptor()
        wrong_extensions["candidate"]["runtimeEntryPath"] = "Assets/VFX/Candidates/R27/C1/w24_moving_projectile_trail/VFX.asset"
        wrong_extensions["candidate"]["previewScenePath"] = "Assets/VFX/Candidates/R27/C1/w24_moving_projectile_trail/Preview/Preview.prefab"
        self.assert_invalid("revisioned_s3", wrong_extensions)

    def test_predecessor_branch_is_exact_and_correlated_to_evidence_revision(self) -> None:
        e1_with_e2 = legacy_s0b_descriptor(1)
        e1_with_e2["predecessor"] = _predecessor("docs/vfx-candidates/sustained_flame_3d/C0", 2)
        self.assert_invalid("legacy_s0b", e1_with_e2)
        self.assert_invalid("legacy_s3", legacy_s3_descriptor(2))
        wrong_revision = revisioned_s3_descriptor(2)
        wrong_revision["predecessor"]["evidenceRevision"] = 2
        self.assert_invalid("revisioned_s3", wrong_revision)
        unknown_predecessor_field = revisioned_s3_descriptor(2)
        unknown_predecessor_field["predecessor"]["verdict"] = "MACHINE_FAIL"
        self.assert_invalid("revisioned_s3", unknown_predecessor_field)

    def test_s0b_and_s3_evaluation_inputs_cannot_be_swapped(self) -> None:
        s0b = legacy_s0b_descriptor()
        s0b["evaluationInput"] = copy.deepcopy(legacy_s3_descriptor()["evaluationInput"])
        self.assert_invalid("legacy_s0b", s0b)
        s3 = legacy_s3_descriptor()
        s3["evaluationInput"] = copy.deepcopy(legacy_s0b_descriptor()["evaluationInput"])
        self.assert_invalid("legacy_s3", s3)

    def test_schema_ids_and_complete_shapes_are_mutually_exclusive(self) -> None:
        examples = {
            "legacy_s0b": legacy_s0b_descriptor(),
            "legacy_s3": legacy_s3_descriptor(),
            "revisioned_s3": revisioned_s3_descriptor(),
        }
        for own_name, value in examples.items():
            self.assert_valid(own_name, value)
            for other_name, other_schema in self.schemas.items():
                if other_name == own_name:
                    continue
                self.assert_invalid(other_name, value)
                schema_swap = copy.deepcopy(value)
                schema_swap["schema"] = other_schema["$id"]
                self.assert_invalid(own_name, schema_swap)


if __name__ == "__main__":
    unittest.main()
