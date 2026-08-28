from __future__ import annotations

import copy
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from jsonschema import Draft202012Validator
from referencing import Registry, Resource

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from vfx.s0a_calibration import (  # noqa: E402
    CalibrationValidationError,
    normalized_sha256,
    verify_labels,
    write_fixture_set,
)
from vfx.s0a_projected_scorer import (  # noqa: E402
    PROJECTED_BLIND_SCHEMA,
    PROJECTED_BUNDLE_SCHEMA,
    PROJECTED_CONTEXT_SCHEMA,
    PROJECTED_CORPUS_SCHEMA,
    PROJECTED_METRICS_SCHEMA,
    PROJECTED_REVIEW_VERSION,
    RICH_SESSION_SCHEMA_V1,
    RICH_SESSION_SCHEMA_V2,
    _load_verified_projected_protocol,
    _manifest_assignments,
    _model_hash,
    _validate_session,
    _write_json_new,
    build_projected_review_corpus,
    calculate_compatible_metrics,
    calculate_projected_metrics,
    load_strict_json,
    rich_validation_checks,
    validate_projected_corpus,
    validate_projected_protocol_bindings,
    verify_projected_metrics,
)


MODEL_ID = "gpt-5.6-sol-calibration-2026-08-25"
RECEIPT_HASH = normalized_sha256({"receipt": "projected-fixture"})
FRAMES = (1, 21, 60, 120, 180, 240, 300, 360)


def _token(name: str) -> str:
    return normalized_sha256({"testIdentity": name})


def _rehash(document: dict, field: str) -> None:
    document[field] = normalized_sha256(document, (field,))


class ProjectedScorerTests(unittest.TestCase):
    @staticmethod
    def _schema_registry() -> tuple[Registry, dict[str, dict]]:
        schema_root = Path(__file__).resolve().parents[3] / "docs" / "skills" / "unity-vfx-visual-qa" / "calibration"
        names = (
            "s0a-metrics.schema.json",
            "s0a-projected-visual-review.schema.json",
            "s0a-isolated-review-session.schema.json",
            "s0a-isolated-review-corpus.schema.json",
            "s0a-projected-metrics.schema.json",
        )
        documents = {
            name: json.loads((schema_root / name).read_text(encoding="utf-8")) for name in names
        }
        registry = Registry()
        for document in documents.values():
            Draft202012Validator.check_schema(document)
            registry = registry.with_resource(document["$id"], Resource.from_contents(document))
        return registry, documents

    def _fixture(self, cohort: str = "reduced", *, fresh_full: bool = False) -> tuple[tempfile.TemporaryDirectory, dict]:
        temp = tempfile.TemporaryDirectory()
        generated = Path(temp.name) / "generated"
        write_fixture_set(
            generated,
            cohort,
            "projected-scorer-test-salt-0123456789",
            fresh_full,
        )
        ledger = json.loads((generated / "operator" / "generation-ledger.json").read_text(encoding="utf-8"))

        context = {
            "schemaVersion": PROJECTED_CONTEXT_SCHEMA,
            "holdoutCohort": ledger["holdoutCohort"],
            "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
            "holdoutIsolationMode": (
                "INITIAL_REDUCED"
                if cohort == "reduced"
                else ("FRESH_INDEPENDENT_FULL" if fresh_full else "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS")
            ),
            "operatorLedgerHash": ledger["ledgerHash"],
            "reducedMetricsHash": None,
            "qaPromptHash": _token("qaPrompt"),
            "imageInputStrategyHash": _token("imageInputStrategy"),
            "threeStateRulesHash": _token("threeStateRules"),
            "aggregationRulesHash": _token("aggregationRules"),
            "visualReviewSchemaHash": _token("visualReviewSchema"),
            "modelVersionHash": _model_hash(MODEL_ID),
            "reviewContractHash": _token("reviewContract"),
            "buildHash": _token("build"),
            "captureProfilePolicyHash": _token("captureProfilePolicy"),
            "frameTableHash": _token("frameTable"),
            "sceneHash": _token("scene"),
            "prefabManifestHash": _token("prefabManifest"),
            "captureToolHash": _token("captureTool"),
        }
        _rehash(context, "contextHash")

        manifest_samples = []
        bundle_samples = []
        labels = {
            "schemaVersion": "calibration-labels/v2",
            "holdoutCohort": context["holdoutCohort"],
            "reviewStatus": "HUMAN_REVIEWED",
            "frozen": True,
            "reviewer": "human-adjudicator-test",
            "frozenAt": "2026-08-25T12:00:00Z",
            "samples": [],
        }
        for index, source in enumerate(ledger["samples"]):
            sample_id = source["sampleId"]
            requirement_id = "s0a.visual.req." + _token(f"requirement:{sample_id}").split(":", 1)[1][:20]
            profile_hash = _token(f"profile:{sample_id}")
            evidence_hash = _token(f"evidence:{sample_id}")
            manifest_samples.append(
                {
                    "sampleId": sample_id,
                    "designRequirementIds": [requirement_id],
                    "captureProfileInstanceHash": profile_hash,
                    "evidenceManifest": f"evidence/{sample_id}.evidence.json",
                    "evidenceHash": evidence_hash,
                }
            )
            blueprint = source["labelBlueprint"]
            invalid = blueprint["groundTruthRoute"] == "EVIDENCE_INVALID"
            diagnostics = [
                {
                    "seedOrdinal": seed,
                    "frameIndex": frame,
                    "file": f"operator/evidence/{sample_id}/seed-{seed}/beauty-{frame:04d}.png",
                    "declaredHash": _token(f"frame:{sample_id}:{seed}:{frame}"),
                    "actualHash": _token(f"frame:{sample_id}:{seed}:{frame}"),
                }
                for seed in range(3)
                for frame in FRAMES
            ]
            bundle_samples.append(
                {
                    "sampleId": sample_id,
                    "commandHash": _token(f"command:{sample_id}"),
                    "completionHash": _token(f"completion:{sample_id}"),
                    "rawCaptureSealFileHash": _token(f"seal:{sample_id}"),
                    "selectedEvidenceDerivationHash": _token(f"derivation:{sample_id}") if invalid else None,
                    "ledgerTailHash": _token(f"tail:{sample_id}"),
                    "captureProfileInstanceHash": profile_hash,
                    "blindEvidenceHash": evidence_hash,
                    "blindEvidenceManifest": f"blind/evidence/{sample_id}.evidence.json",
                    "selectedCaptureMetadata": {
                        "file": f"operator/evidence/{sample_id}/capture-metadata.json",
                        "declaredHash": _token(f"metadata:{sample_id}"),
                        "actualHash": _token(f"metadata:{sample_id}"),
                    },
                    "semanticTelemetry": {
                        "file": f"operator/evidence/{sample_id}/telemetry.json",
                        "sha256": _token(f"telemetry:{sample_id}"),
                    },
                    "diagnosticFrames": diagnostics,
                }
            )
            labels["samples"].append(
                {
                    "sampleId": sample_id,
                    "requirementId": requirement_id,
                    "groundTruthRoute": blueprint["groundTruthRoute"],
                    "perRequirement": [
                        {
                            "requirementId": requirement_id,
                            "state": blueprint["perRequirement"][0]["state"],
                        }
                    ],
                    "labelSource": "HUMAN_ADJUDICATION",
                    "reviewer": "human-adjudicator-test",
                    "visuallyObservable": blueprint["visuallyObservable"],
                    "eligibleForVisualMetrics": blueprint["eligibleForVisualMetrics"],
                    "isBoundary": blueprint["isBoundary"],
                    "evidenceHash": evidence_hash,
                    "verdictVersion": "projected-human-v1",
                }
            )

        manifest = {
            "schemaVersion": PROJECTED_BLIND_SCHEMA,
            "holdoutCohort": context["holdoutCohort"],
            "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
            "reviewContractHash": context["reviewContractHash"],
            "captureProfilePolicyHash": context["captureProfilePolicyHash"],
            "reviewContextHash": context["contextHash"],
            "samples": manifest_samples,
        }
        _rehash(manifest, "manifestHash")
        bundle = {
            "schemaVersion": PROJECTED_BUNDLE_SCHEMA,
            "holdoutCohort": context["holdoutCohort"],
            "freezeStatus": "FROZEN_FOR_BLIND_REVIEW",
            "operatorLedgerHash": ledger["ledgerHash"],
            "commandSetHash": _token("commandSet"),
            "blindManifestHash": manifest["manifestHash"],
            "reviewContextHash": context["contextHash"],
            "captureProfilePolicyHash": context["captureProfilePolicyHash"],
            "samples": bundle_samples,
        }
        _rehash(bundle, "bundleHash")
        _rehash(labels, "manifestHash")
        self.assertEqual(verify_labels(labels), [])
        self.assertEqual(validate_projected_protocol_bindings(manifest, bundle, context), [])
        return temp, {
            "root": Path(temp.name),
            "ledger": ledger,
            "context": context,
            "manifest": manifest,
            "bundle": bundle,
            "labels": labels,
        }

    def _report(self, label: dict, context: dict) -> dict:
        route = label["groundTruthRoute"]
        if route == "VISUAL_PASS":
            state, countable, reason, read, evidence, contract = "pass", True, "OBSERVED", True, "valid", "unambiguous"
        elif route == "VISUAL_FAIL":
            state, countable, reason, read, evidence, contract = "fail", True, "OBSERVED", True, "valid", "unambiguous"
        elif route == "VISUAL_UNCERTAIN":
            state, countable, reason, read, evidence, contract = "uncertain", True, "VISUAL_UNCERTAIN", True, "valid", "unambiguous"
        elif route == "EVIDENCE_INVALID":
            state, countable, reason, read, evidence, contract = "uncertain", False, "EVIDENCE_INVALID", False, "invalid", "unambiguous"
        else:
            state, countable, reason, read, evidence, contract = "uncertain", False, "CONTRACT_AMBIGUOUS", True, "valid", "ambiguous"
        report = {
            "reviewVersion": PROJECTED_REVIEW_VERSION,
            "candidateId": "C0",
            "contractHash": context["reviewContractHash"],
            "buildHash": context["buildHash"],
            "captureProfileHash": context["captureProfilePolicyHash"],
            "evidenceHashes": [label["evidenceHash"]],
            "imageInputRead": read,
            "evidenceStatus": evidence,
            "contractStatus": contract,
            "s0aTerminalStatus": None,
            "qaGateAuthority": "advisory-only",
            "topLevelRoute": route,
            "perRequirement": [
                {
                    "designRequirementId": label["requirementId"],
                    "state": state,
                    "countable": countable,
                    "reasonCode": reason,
                    "stateRef": "fixed-3x8-filmstrip",
                    "frameNumbers": [1, 21, 60, 120, 180, 240, 300, 360],
                    "imageRegion": "full-frame",
                    "observation": "synthetic projected scorer fixture",
                }
            ],
        }
        _rehash(report, "sealedReportHash")
        return report

    def _session(self, artifacts: dict, index: int) -> dict:
        count = len(artifacts["labels"]["samples"])
        session = {
            "sessionId": f"s0a-{count}-session-{index}",
            "reviewerSessionId": f"gpt-5.6-sol-session-{index}",
            "isolated": True,
            "startedAt": f"2026-08-25T1{index}:00:00Z",
            "completedAt": f"2026-08-25T1{index}:30:00Z",
            "reviews": [
                {
                    "sampleId": label["sampleId"],
                    "report": self._session_report(label, artifacts["context"], index),
                }
                for label in artifacts["labels"]["samples"]
            ],
        }
        _rehash(session, "sessionHash")
        return session

    def _session_report(self, label: dict, context: dict, index: int) -> dict:
        report = self._report(label, context)
        report["perRequirement"][0]["observation"] += f"; isolated-session-{index}"
        _rehash(report, "sealedReportHash")
        return report

    def _write_session(self, artifacts: dict, index: int) -> Path:
        root = artifacts["root"] / f"session-{index}"
        reports = root / "reports"
        reports.mkdir(parents=True)
        session = self._session(artifacts, index)
        (root / "session.json").write_text(json.dumps(session, indent=2) + "\n", encoding="utf-8")
        for record in session["reviews"]:
            (reports / f"{record['sampleId']}.report.json").write_text(
                json.dumps(record["report"], indent=2) + "\n", encoding="utf-8"
            )
        return root

    def _convert_session_to_rich(
        self,
        artifacts: dict,
        root: Path,
        schema_version: str = RICH_SESSION_SCHEMA_V2,
    ) -> None:
        simple = json.loads((root / "session.json").read_text(encoding="utf-8"))
        reports = []
        route_counts = {route: 0 for route in (
            "VISUAL_PASS", "VISUAL_FAIL", "EVIDENCE_INVALID", "CONTRACT_AMBIGUOUS", "VISUAL_UNCERTAIN"
        )}
        for record in simple["reviews"]:
            sample_id = record["sampleId"]
            report = record["report"]
            route_counts[report["topLevelRoute"]] += 1
            old = root / "reports" / f"{sample_id}.report.json"
            new = root / "reports" / f"{sample_id}.review.json"
            old.rename(new)
            availability = "missing" if report["topLevelRoute"] == "EVIDENCE_INVALID" else "present"
            slots = [
                {"seedOrdinal": seed, "frameNumber": frame, "availability": availability}
                for seed in range(3) for frame in FRAMES
            ]
            reports.append(
                {
                    "sampleId": sample_id,
                    "reportFile": f"reports/{sample_id}.review.json",
                    "evidenceHash": report["evidenceHashes"][0],
                    "imageReviewComplete": True,
                    "reviewedSeedOrdinals": [0, 1, 2],
                    "reviewedFrameNumbers": list(FRAMES),
                    "expectedBeautyFrameCount": 24,
                    "presentBeautyFrameCount": sum(slot["availability"] == "present" for slot in slots),
                    "reviewedBeautySlotCount": 24,
                    "reviewedBeautySlots": slots,
                    "report": report,
                }
            )
        count = len(reports)
        rich = {
            "schemaVersion": schema_version,
            "reviewVersion": PROJECTED_REVIEW_VERSION,
            "sessionId": simple["sessionId"],
            "reviewerSessionId": simple["reviewerSessionId"],
            "candidateId": "C0",
            "contractHash": artifacts["context"]["reviewContractHash"],
            "buildHash": artifacts["context"]["buildHash"],
            "captureProfileHash": artifacts["context"]["captureProfilePolicyHash"],
            "isolated": True,
            "qaGateAuthority": "advisory-only",
            "s0aTerminalStatus": None,
            "sampleCount": count,
            "routeCounts": route_counts,
            "validationChecks": rich_validation_checks(count),
            "reports": reports,
        }
        _rehash(rich, "sessionHash")
        (root / "session.json").write_text(json.dumps(rich, indent=2) + "\n", encoding="utf-8")

    def _rich_session_errors(self, artifacts: dict, session: dict) -> list[str]:
        return _validate_session(
            session,
            _manifest_assignments(artifacts["manifest"]),
            artifacts["context"],
            "rich test session",
        )

    def _rich_schema(self) -> Draft202012Validator:
        registry, schemas = self._schema_registry()
        return Draft202012Validator(
            schemas["s0a-isolated-review-session.schema.json"], registry=registry
        )

    def _corpus(self, artifacts: dict) -> dict:
        session_dirs = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        return build_projected_review_corpus(
            artifacts["manifest"], artifacts["bundle"], artifacts["context"],
            session_dirs, MODEL_ID, RECEIPT_HASH, artifacts["root"] / "corpus.json"
        )

    def _write_protocol_root(self, artifacts: dict) -> Path:
        root = artifacts["root"] / "protocol-root"
        (root / "blind").mkdir(parents=True)
        (root / "operator").mkdir()
        (root / "blind" / "blind-submission-manifest.json").write_text(
            json.dumps(artifacts["manifest"]), encoding="utf-8"
        )
        (root / "blind" / "review-freeze-context.json").write_text(
            json.dumps(artifacts["context"]), encoding="utf-8"
        )
        (root / "operator" / "evidence-bundle.json").write_text(
            json.dumps(artifacts["bundle"]), encoding="utf-8"
        )
        return root

    def test_build_validate_and_score_projected_v3(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        self.assertEqual(
            validate_projected_corpus(
                corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
            ),
            [],
        )
        metrics = calculate_projected_metrics(
            artifacts["labels"], corpus, artifacts["manifest"], artifacts["bundle"],
            artifacts["context"], artifacts["ledger"], RECEIPT_HASH
        )
        self.assertEqual(metrics["schemaVersion"], PROJECTED_METRICS_SCHEMA)
        self.assertEqual(metrics["projectionReceiptHash"], RECEIPT_HASH)
        self.assertEqual(metrics["reviewCorpusHash"], corpus["corpusHash"])
        self.assertEqual(verify_projected_metrics(metrics, artifacts["labels"]), [])
        registry, schemas = self._schema_registry()
        Draft202012Validator(
            schemas["s0a-projected-metrics.schema.json"], registry=registry
        ).validate(metrics)

    def test_mixed_legacy_projected_protocol_is_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        mixed = copy.deepcopy(artifacts["bundle"])
        mixed["schemaVersion"] = "s0a-evidence-bundle/v2"
        with self.assertRaisesRegex(CalibrationValidationError, "Mixed or unsupported"):
            calculate_compatible_metrics(
                artifacts["labels"], corpus, artifacts["manifest"], mixed,
                artifacts["context"], artifacts["ledger"], projection_receipt_hash=RECEIPT_HASH
            )

    def test_assignment_evidence_profile_context_and_receipt_swaps_fail(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        for field in ("designRequirementIds", "evidenceHash", "captureProfileInstanceHash"):
            manifest = copy.deepcopy(artifacts["manifest"])
            if field == "designRequirementIds":
                manifest["samples"][0][field] = manifest["samples"][1][field]
            else:
                manifest["samples"][0][field] = _token(f"foreign:{field}")
            _rehash(manifest, "manifestHash")
            errors = validate_projected_protocol_bindings(manifest, artifacts["bundle"], artifacts["context"])
            self.assertTrue(errors, field)
        context = copy.deepcopy(artifacts["context"])
        context["captureProfilePolicyHash"] = _token("foreign-policy")
        _rehash(context, "contextHash")
        self.assertTrue(validate_projected_protocol_bindings(artifacts["manifest"], artifacts["bundle"], context))
        corpus = self._corpus(artifacts)
        self.assertTrue(
            validate_projected_corpus(
                corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], _token("foreign-receipt")
            )
        )

    def test_report_assignment_and_fixed_frame_leaks_fail(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        report = corpus["sessions"][0]["reviews"][0]["report"]
        report["perRequirement"][0]["designRequirementId"] = "baseline_control"
        report["perRequirement"][0]["frameNumbers"] = [42]
        _rehash(report, "sealedReportHash")
        _rehash(corpus["sessions"][0], "sessionHash")
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("assignment" in error for error in errors))
        self.assertTrue(any("frameNumbers" in error for error in errors))

    def test_sessions_must_be_isolated_and_distinct(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        corpus["sessions"][0]["isolated"] = False
        corpus["sessions"][1]["reviewerSessionId"] = corpus["sessions"][0]["reviewerSessionId"]
        for session in corpus["sessions"]:
            _rehash(session, "sessionHash")
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("isolated=true" in error for error in errors))
        self.assertTrue(any("repeats reviewerSessionId" in error for error in errors))

    def test_model_identity_mismatch_is_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        with self.assertRaisesRegex(CalibrationValidationError, "model_version_id"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
                "different-model-version", RECEIPT_HASH, artifacts["root"] / "wrong-model.json"
            )

    def test_report_context_and_evidence_replay_are_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        first = corpus["sessions"][0]["reviews"][0]["report"]
        first["contractHash"] = _token("foreign-contract")
        first["evidenceHashes"] = [
            corpus["sessions"][0]["reviews"][1]["report"]["evidenceHashes"][0]
        ]
        _rehash(first, "sealedReportHash")
        _rehash(corpus["sessions"][0], "sessionHash")
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("reviewContractHash" in error for error in errors))
        self.assertTrue(any("evidenceHashes" in error for error in errors))

    def test_diagnostic_matrix_and_metrics_source_identity_are_strict(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        bundle = copy.deepcopy(artifacts["bundle"])
        bundle["samples"][0]["diagnosticFrames"][1] = copy.deepcopy(
            bundle["samples"][0]["diagnosticFrames"][0]
        )
        _rehash(bundle, "bundleHash")
        self.assertTrue(
            any("diagnostic frame" in error for error in validate_projected_protocol_bindings(
                artifacts["manifest"], bundle, artifacts["context"]
            ))
        )
        corpus = self._corpus(artifacts)
        metrics = calculate_projected_metrics(
            artifacts["labels"], corpus, artifacts["manifest"], artifacts["bundle"],
            artifacts["context"], artifacts["ledger"], RECEIPT_HASH
        )
        metrics["scorerSourceHash"] = _token("drifted-scorer")
        _rehash(metrics, "reportHash")
        self.assertTrue(any("scorerSourceHash" in error for error in verify_projected_metrics(metrics)))

    def test_duplicate_session_replay_is_rejected_even_when_corpus_is_resealed(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        corpus["sessions"][2] = copy.deepcopy(corpus["sessions"][0])
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("repeats sessionId" in error for error in errors))
        self.assertTrue(any("repeats reviewerSessionId" in error for error in errors))
        self.assertTrue(any("repeats sessionHash" in error for error in errors))

    def test_single_report_replay_across_distinct_sessions_is_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        corpus["sessions"][1]["reviews"][0]["report"] = copy.deepcopy(
            corpus["sessions"][0]["reviews"][0]["report"]
        )
        _rehash(corpus["sessions"][1], "sessionHash")
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("replays sealed report" in error for error in errors))

    def test_reordered_session_reviews_are_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        corpus["sessions"][0]["reviews"][0], corpus["sessions"][0]["reviews"][1] = (
            corpus["sessions"][0]["reviews"][1], corpus["sessions"][0]["reviews"][0]
        )
        _rehash(corpus["sessions"][0], "sessionHash")
        _rehash(corpus, "corpusHash")
        errors = validate_projected_corpus(
            corpus, artifacts["manifest"], artifacts["bundle"], artifacts["context"], RECEIPT_HASH
        )
        self.assertTrue(any("review order" in error for error in errors))

    def test_write_once_and_session_file_replay_are_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        corpus = self._corpus(artifacts)
        with self.assertRaisesRegex(CalibrationValidationError, "overwrite"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"],
                [artifacts["root"] / f"session-{index}" for index in (1, 2, 3)],
                MODEL_ID, RECEIPT_HASH, artifacts["root"] / "corpus.json"
            )
        path = artifacts["root"] / "session-1" / "reports" / f"{corpus['sessions'][0]['reviews'][0]['sampleId']}.report.json"
        forged = copy.deepcopy(corpus["sessions"][0]["reviews"][0]["report"])
        forged["observation"] = "foreign"  # deliberately changes file shape/content
        path.write_text(json.dumps(forged), encoding="utf-8")
        output = artifacts["root"] / "corpus-second.json"
        with self.assertRaisesRegex(CalibrationValidationError, "embedded report differ"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"],
                [artifacts["root"] / f"session-{index}" for index in (1, 2, 3)],
                MODEL_ID, RECEIPT_HASH, output
            )

    def test_corpus_output_cannot_mutate_a_sealed_session_directory(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        with self.assertRaisesRegex(CalibrationValidationError, "protected input root"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
                MODEL_ID, RECEIPT_HASH, directories[0] / "injected-corpus.json"
            )

    def test_whole_session_review_suffix_is_accepted_but_mixing_is_not(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        for report in list((directories[1] / "reports").glob("*.report.json")):
            report.rename(report.with_name(report.name.replace(".report.json", ".review.json")))
        corpus = build_projected_review_corpus(
            artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
            MODEL_ID, RECEIPT_HASH, artifacts["root"] / "mixed-extension-corpus.json"
        )
        self.assertEqual(corpus["schemaVersion"], PROJECTED_CORPUS_SCHEMA)
        one = next((directories[2] / "reports").glob("*.report.json"))
        one.rename(one.with_name(one.name.replace(".report.json", ".review.json")))
        with self.assertRaisesRegex(CalibrationValidationError, "one deterministic suffix"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
                MODEL_ID, RECEIPT_HASH, artifacts["root"] / "forbidden-mixed-extension.json"
            )

    def test_rich_v2_reduced66_exact_tokens_pass_schema_and_scorer(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directory = self._write_session(artifacts, 1)
        self._convert_session_to_rich(artifacts, directory)
        session = json.loads((directory / "session.json").read_text(encoding="utf-8"))
        self._rich_schema().validate(session)
        self.assertEqual(self._rich_session_errors(artifacts, session), [])
        self.assertEqual(session["schemaVersion"], RICH_SESSION_SCHEMA_V2)
        self.assertEqual(session["validationChecks"], rich_validation_checks(66))
        with self.assertRaises(CalibrationValidationError):
            rich_validation_checks(66.0)

        legacy = copy.deepcopy(session)
        legacy["schemaVersion"] = RICH_SESSION_SCHEMA_V1
        _rehash(legacy, "sessionHash")
        self._rich_schema().validate(legacy)
        self.assertEqual(self._rich_session_errors(artifacts, legacy), [])

    def test_rich_v2_fresh_full110_exact_tokens_pass_schema_and_scorer(self) -> None:
        temp, artifacts = self._fixture("full", fresh_full=True)
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        for directory in directories:
            self._convert_session_to_rich(artifacts, directory)
        directory = directories[0]
        session = json.loads((directory / "session.json").read_text(encoding="utf-8"))
        self._rich_schema().validate(session)
        self.assertEqual(self._rich_session_errors(artifacts, session), [])
        self.assertEqual(session["schemaVersion"], RICH_SESSION_SCHEMA_V2)
        self.assertEqual(session["validationChecks"], rich_validation_checks(110))

        corpus = build_projected_review_corpus(
            artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
            MODEL_ID, RECEIPT_HASH, artifacts["root"] / "rich-full-corpus.json"
        )
        self.assertEqual(
            [item["schemaVersion"] for item in corpus["sessionProtocols"]],
            [RICH_SESSION_SCHEMA_V2] * 3,
        )
        self.assertTrue(all("reports" in item and "reviews" not in item for item in corpus["sessions"]))
        registry, schemas = self._schema_registry()
        Draft202012Validator(
            schemas["s0a-isolated-review-corpus.schema.json"], registry=registry
        ).validate(corpus)

        legacy = copy.deepcopy(session)
        legacy["schemaVersion"] = RICH_SESSION_SCHEMA_V1
        _rehash(legacy, "sessionHash")
        self.assertFalse(self._rich_schema().is_valid(legacy))
        self.assertTrue(
            any("reduced66-only" in error for error in self._rich_session_errors(artifacts, legacy))
        )

    def test_rich_reduced66_with_full110_tokens_is_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directory = self._write_session(artifacts, 1)
        self._convert_session_to_rich(artifacts, directory)
        session = json.loads((directory / "session.json").read_text(encoding="utf-8"))
        session["validationChecks"] = rich_validation_checks(110)
        _rehash(session, "sessionHash")
        self.assertFalse(self._rich_schema().is_valid(session))
        self.assertTrue(
            any("validationChecks" in error for error in self._rich_session_errors(artifacts, session))
        )

    def test_rich_fresh_full110_with_reduced66_tokens_is_rejected(self) -> None:
        temp, artifacts = self._fixture("full", fresh_full=True)
        self.addCleanup(temp.cleanup)
        directory = self._write_session(artifacts, 1)
        self._convert_session_to_rich(artifacts, directory)
        session = json.loads((directory / "session.json").read_text(encoding="utf-8"))
        session["validationChecks"] = rich_validation_checks(66)
        _rehash(session, "sessionHash")
        self.assertFalse(self._rich_schema().is_valid(session))
        self.assertTrue(
            any("validationChecks" in error for error in self._rich_session_errors(artifacts, session))
        )

    def test_rich_tokens_mixed_duplicate_missing_and_extra_are_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directory = self._write_session(artifacts, 1)
        self._convert_session_to_rich(artifacts, directory)
        baseline = json.loads((directory / "session.json").read_text(encoding="utf-8"))
        reduced = rich_validation_checks(66)
        full = rich_validation_checks(110)
        cases = {
            "mixed": [full[0], *reduced[1:]],
            "duplicate": [*reduced[:-1], reduced[0]],
            "missing": reduced[:-1],
            "extra": [*reduced, full[0]],
            "non-string": [{}, *reduced[1:]],
        }
        schema = self._rich_schema()
        for name, checks in cases.items():
            with self.subTest(name=name):
                session = copy.deepcopy(baseline)
                session["validationChecks"] = checks
                _rehash(session, "sessionHash")
                self.assertFalse(schema.is_valid(session))
                self.assertTrue(
                    any("validationChecks" in error for error in self._rich_session_errors(artifacts, session))
                )

    def test_mixed_simple_and_strict_rich_session_shapes_are_explicitly_bound(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        self._convert_session_to_rich(artifacts, directories[1])
        corpus = build_projected_review_corpus(
            artifacts["manifest"], artifacts["bundle"], artifacts["context"], directories,
            MODEL_ID, RECEIPT_HASH, artifacts["root"] / "rich-corpus.json"
        )
        self.assertEqual(
            [item["schemaVersion"] for item in corpus["sessionProtocols"]],
            ["s0a-isolated-review-session-directory/v1", RICH_SESSION_SCHEMA_V2, "s0a-isolated-review-session-directory/v1"],
        )
        registry, schemas = self._schema_registry()
        Draft202012Validator(
            schemas["s0a-isolated-review-corpus.schema.json"], registry=registry
        ).validate(corpus)
        rich_schema = Draft202012Validator(
            schemas["s0a-isolated-review-session.schema.json"], registry=registry
        )
        illegal_67 = copy.deepcopy(corpus["sessions"][1])
        illegal_67["reports"].append(copy.deepcopy(illegal_67["reports"][0]))
        self.assertFalse(rich_schema.is_valid(illegal_67))
        descriptive_checks = copy.deepcopy(corpus["sessions"][1])
        descriptive_checks["validationChecks"] = [
            f"descriptive validation statement {index}" for index in range(6)
        ]
        self.assertFalse(
            rich_schema.is_valid(descriptive_checks),
            "the rich session schema must reject prose in place of the six frozen check tokens",
        )
        report_schema = Draft202012Validator(
            schemas["s0a-projected-visual-review.schema.json"], registry=registry
        )
        illegal_two_requirements = copy.deepcopy(corpus["sessions"][0]["reviews"][0]["report"])
        illegal_two_requirements["perRequirement"].append(
            copy.deepcopy(illegal_two_requirements["perRequirement"][0])
        )
        self.assertFalse(report_schema.is_valid(illegal_two_requirements))
        illegal_uncertain_fail = copy.deepcopy(corpus["sessions"][0]["reviews"][0]["report"])
        illegal_uncertain_fail["topLevelRoute"] = "VISUAL_UNCERTAIN"
        illegal_uncertain_fail["perRequirement"][0].update(
            {"state": "fail", "countable": True, "reasonCode": "OBSERVED"}
        )
        self.assertFalse(report_schema.is_valid(illegal_uncertain_fail))

    def test_strict_json_rejects_duplicate_nonfinite_and_lone_surrogate(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            cases = {
                "duplicate.json": '{"x":1,"x":2}',
                "nan.json": '{"x":NaN}',
                "surrogate.json": '{"x":"\\ud800"}',
            }
            for name, payload in cases.items():
                path = root / name
                path.write_text(payload, encoding="utf-8")
                with self.subTest(name=name), self.assertRaises(CalibrationValidationError):
                    load_strict_json(path)

    def test_parent_traversal_is_rejected_before_json_read(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "target.json"
            target.write_text("{}", encoding="utf-8")
            traversing = root / "child" / ".." / "target.json"
            with self.assertRaisesRegex(CalibrationValidationError, "Parent traversal"):
                load_strict_json(traversing)

    def test_output_parent_traversal_and_publish_race_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaisesRegex(CalibrationValidationError, "Parent traversal"):
                _write_json_new(root / "child" / ".." / "out.json", {"ok": True})
            target = root / "raced.json"
            with mock.patch("vfx.s0a_projected_scorer.os.link", side_effect=FileExistsError):
                with self.assertRaisesRegex(CalibrationValidationError, "overwrite"):
                    _write_json_new(target, {"ok": True})
            self.assertFalse(target.exists())
            self.assertEqual(list(root.glob("*.publishing")), [])

    def test_verified_protocol_load_binds_receipt_and_detects_mid_load_drift(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        root = self._write_protocol_root(artifacts)
        receipt = {
            "blindManifestHash": artifacts["manifest"]["manifestHash"],
            "operatorEvidenceBundleHash": artifacts["bundle"]["bundleHash"],
            "reviewContextHash": artifacts["context"]["contextHash"],
            "receiptHash": RECEIPT_HASH,
        }
        with mock.patch("vfx.s0a_projection.verify_projection", side_effect=[receipt, receipt]):
            loaded = _load_verified_projected_protocol(root)
        self.assertEqual(loaded[0], receipt)
        mismatch = copy.deepcopy(receipt)
        mismatch["blindManifestHash"] = _token("replaced-manifest")
        with mock.patch("vfx.s0a_projection.verify_projection", return_value=mismatch):
            with self.assertRaisesRegex(CalibrationValidationError, "differ from the verified"):
                _load_verified_projected_protocol(root)
        drifted = copy.deepcopy(receipt)
        drifted["receiptHash"] = _token("receipt-drift")
        with mock.patch("vfx.s0a_projection.verify_projection", side_effect=[receipt, drifted]):
            with self.assertRaisesRegex(CalibrationValidationError, "changed during"):
                _load_verified_projected_protocol(root)

    def test_reparse_branch_rejects_input_before_json_read(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "input.json"
            path.write_text("{}", encoding="utf-8")
            with mock.patch("vfx.s0a_projected_scorer._is_reparse", return_value=True):
                with self.assertRaisesRegex(CalibrationValidationError, "reparse"):
                    load_strict_json(path)

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink unavailable")
    def test_symlink_session_root_is_rejected(self) -> None:
        temp, artifacts = self._fixture()
        self.addCleanup(temp.cleanup)
        directories = [self._write_session(artifacts, index) for index in (1, 2, 3)]
        link = artifacts["root"] / "linked-session"
        try:
            os.symlink(directories[0], link, target_is_directory=True)
        except OSError as exc:
            self.skipTest(f"symlink creation unavailable: {exc}")
        with self.assertRaisesRegex(CalibrationValidationError, "symlink|reparse|junction"):
            build_projected_review_corpus(
                artifacts["manifest"], artifacts["bundle"], artifacts["context"],
                [link, directories[1], directories[2]], MODEL_ID, RECEIPT_HASH,
                artifacts["root"] / "linked-corpus.json"
            )


if __name__ == "__main__":
    unittest.main()
