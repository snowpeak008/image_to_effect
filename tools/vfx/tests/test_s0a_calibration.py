from __future__ import annotations

import copy
import contextlib
import io
import json
import sys
import tempfile
import unittest
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from vfx.s0a_calibration import (  # noqa: E402
    COHORT_IDS,
    COHORTS,
    FREEZE_CONTEXT_HASH_FIELDS,
    CalibrationValidationError,
    build_samples,
    calculate_metrics,
    main,
    normalized_json,
    normalized_sha256,
    operator_mutation_vocabulary,
    validate_frozen_bindings,
    validate_label_schema_shape,
    validate_metrics_schema_shape,
    validate_reviews,
    verify_labels,
    verify_metrics,
    write_fixture_set,
)


ID_SALT = "test-only-operator-salt-0123456789"


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _rehash(document: dict, field: str) -> None:
    document[field] = normalized_sha256(document, (field,))


def _hash_token(name: str) -> str:
    return normalized_sha256({"testIdentity": name})


class S0aCalibrationTests(unittest.TestCase):
    def test_operator_mutation_vocabulary_is_the_twelve_target_unity_adapter_contract(self) -> None:
        vocabulary = operator_mutation_vocabulary()
        self.assertEqual(len(vocabulary), 12)
        self.assertEqual(vocabulary["Capture.frameManifestIntegrity"], ("missing_key_frame", "sha256_mismatch"))
        self.assertEqual(vocabulary["Light.enabled"], ("false", "intensity_0.02"))

    def _frozen_bundle(self, cohort: str, *, fresh_full: bool = False) -> tuple[tempfile.TemporaryDirectory, dict]:
        temp = tempfile.TemporaryDirectory()
        root = Path(temp.name)
        write_fixture_set(root, cohort, ID_SALT, fresh_full)
        blind = _load(root / "blind" / "blind-submission-manifest.json")
        context = _load(root / "blind" / "review-freeze-context.json")
        ledger = _load(root / "operator" / "generation-ledger.json")
        evidence = _load(root / "operator" / "evidence-bundle.json")

        context["freezeStatus"] = "FROZEN"
        for field in FREEZE_CONTEXT_HASH_FIELDS:
            context[field] = _hash_token(field)
        _rehash(context, "contextHash")

        blind.update(
            {
                "freezeStatus": "FROZEN",
                "contractHash": context["contractHash"],
                "captureProfileHash": context["captureProfileHash"],
                "reviewContextHash": context["contextHash"],
            }
        )
        for sample in blind["samples"]:
            sample["contractHash"] = blind["contractHash"]
            sample["captureProfileHash"] = blind["captureProfileHash"]
        _rehash(blind, "manifestHash")

        evidence.update(
            {
                "freezeStatus": "FROZEN",
                "blindManifestHash": blind["manifestHash"],
                "captureProfileHash": context["captureProfileHash"],
            }
        )
        for sample in evidence["samples"]:
            sample["evidenceHash"] = _hash_token(f"evidence:{sample['sampleId']}")
        _rehash(evidence, "bundleHash")
        evidence_by_id = {sample["sampleId"]: sample["evidenceHash"] for sample in evidence["samples"]}

        labels = {
            "schemaVersion": "calibration-labels/v2",
            "holdoutCohort": COHORT_IDS[cohort],
            "reviewStatus": "HUMAN_REVIEWED",
            "frozen": True,
            "reviewer": "human-reviewer-1",
            "frozenAt": "2026-08-25T12:00:00Z",
            "samples": [],
        }
        for sample in ledger["samples"]:
            blueprint = sample["labelBlueprint"]
            labels["samples"].append(
                {
                    "sampleId": sample["sampleId"],
                    "requirementId": blueprint["requirementId"],
                    "groundTruthRoute": blueprint["groundTruthRoute"],
                    "perRequirement": copy.deepcopy(blueprint["perRequirement"]),
                    "labelSource": "HUMAN_ADJUDICATION",
                    "reviewer": "human-reviewer-1",
                    "visuallyObservable": blueprint["visuallyObservable"],
                    "eligibleForVisualMetrics": blueprint["eligibleForVisualMetrics"],
                    "isBoundary": blueprint["isBoundary"],
                    "evidenceHash": evidence_by_id[sample["sampleId"]],
                    "verdictVersion": "human-v1",
                }
            )
        _rehash(labels, "manifestHash")
        self.assertEqual(verify_labels(labels), [])
        if context["holdoutIsolationMode"] != "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS":
            self.assertEqual(validate_frozen_bindings(labels, blind, evidence, context, ledger), [])
        return temp, {"root": root, "labels": labels, "blind": blind, "context": context, "ledger": ledger, "evidence": evidence}

    def _report(self, label: dict, context: dict, route: str | None = None) -> dict:
        route = route or label["groundTruthRoute"]
        if route == "VISUAL_PASS":
            state, countable, reason, image_read, evidence_status, contract_status = "pass", True, "OBSERVED", True, "valid", "unambiguous"
        elif route == "VISUAL_FAIL":
            state, countable, reason, image_read, evidence_status, contract_status = "fail", True, "OBSERVED", True, "valid", "unambiguous"
        elif route == "VISUAL_UNCERTAIN":
            state, countable, reason, image_read, evidence_status, contract_status = "uncertain", True, "VISUAL_UNCERTAIN", True, "valid", "unambiguous"
        elif route == "EVIDENCE_INVALID":
            state, countable, reason, image_read, evidence_status, contract_status = "uncertain", False, "EVIDENCE_INVALID", False, "invalid", "unambiguous"
        else:
            state, countable, reason, image_read, evidence_status, contract_status = "uncertain", False, "CONTRACT_AMBIGUOUS", True, "valid", "ambiguous"
        report = {
            "reviewVersion": "s0a-test-review/v1",
            "candidateId": "C0",
            "contractHash": context["contractHash"],
            "buildHash": context["buildHash"],
            "captureProfileHash": context["captureProfileHash"],
            "evidenceHashes": [label["evidenceHash"]],
            "imageInputRead": image_read,
            "evidenceStatus": evidence_status,
            "contractStatus": contract_status,
            "s0aTerminalStatus": None,
            "qaGateAuthority": "advisory-only",
            "topLevelRoute": route,
            "perRequirement": [
                {
                    "designRequirementId": item["requirementId"],
                    "state": state,
                    "countable": countable,
                    "reasonCode": reason,
                    "stateRef": "frame-0",
                    "frameNumbers": [0],
                    "imageRegion": "full-frame",
                    "observation": "synthetic test fixture record",
                }
                for item in label["perRequirement"]
            ],
        }
        _rehash(report, "sealedReportHash")
        return report

    def _reviews(self, artifacts: dict, overrides: dict[str, str] | None = None) -> dict:
        overrides = overrides or {}
        sessions = []
        for index in range(3):
            session = {
                "sessionId": f"session-{index}",
                "reviewerSessionId": f"independent-reviewer-run-{index}",
                "isolated": True,
                "startedAt": f"2026-08-25T12:0{index}:00Z",
                "completedAt": f"2026-08-25T12:1{index}:00Z",
                "reviews": [
                    {"sampleId": label["sampleId"], "report": self._report(label, artifacts["context"], overrides.get(label["sampleId"]))}
                    for label in artifacts["labels"]["samples"]
                ],
            }
            _rehash(session, "sessionHash")
            sessions.append(session)
        corpus = {
            "schemaVersion": "s0a-isolated-review-corpus/v2",
            "blindManifestHash": artifacts["blind"]["manifestHash"],
            "evidenceBundleHash": artifacts["evidence"]["bundleHash"],
            "reviewContextHash": artifacts["context"]["contextHash"],
            "sessions": sessions,
        }
        _rehash(corpus, "corpusHash")
        return corpus

    @staticmethod
    def _rehash_corpus(corpus: dict) -> None:
        for session in corpus.get("sessions", []):
            if isinstance(session, dict):
                for record in session.get("reviews", []):
                    if isinstance(record, dict) and isinstance(record.get("report"), dict):
                        _rehash(record["report"], "sealedReportHash")
                _rehash(session, "sessionHash")
        _rehash(corpus, "corpusHash")

    def _score(self, artifacts: dict, reviews: dict) -> dict:
        return calculate_metrics(artifacts["labels"], reviews, artifacts["blind"], artifacts["evidence"], artifacts["context"], artifacts["ledger"])

    def test_parameterized_generation_is_deterministic_and_fresh_full_isolated(self) -> None:
        reduced = build_samples("reduced", ID_SALT)
        self.assertEqual(reduced, build_samples("reduced", ID_SALT))
        self.assertEqual(Counter(sample["kind"] for sample in reduced), COHORTS["reduced"])
        expanded_full = build_samples("full", ID_SALT, "expanded")
        self.assertTrue({sample["sampleId"] for sample in reduced}.issubset({sample["sampleId"] for sample in expanded_full}))
        fresh_full = build_samples("full", ID_SALT, "fresh")
        self.assertTrue({sample["sampleId"] for sample in reduced}.isdisjoint({sample["sampleId"] for sample in fresh_full}))
        self.assertTrue({sample["fixedSeed"] for sample in reduced}.isdisjoint({sample["fixedSeed"] for sample in fresh_full}))
        for sample in reduced:
            self.assertNotIn(sample["injection"]["errorClass"], sample["sampleId"])
        strengths: dict[str, set[str]] = {}
        for sample in reduced:
            injection = sample["injection"]
            strengths.setdefault(injection["errorClass"], set()).add(injection["strength"])
        self.assertEqual(len(set(strengths) - {"baseline_control"}), 12)
        self.assertTrue(all(strengths[error] == {"obvious", "boundary"} for error in strengths if error != "baseline_control"))

    def test_blind_delivery_has_no_answer_leak_and_is_physically_separated(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            result = write_fixture_set(root, "reduced", ID_SALT)
            blind = _load(root / "blind" / "blind-submission-manifest.json")
            serialized = normalized_json(blind).lower()
            for forbidden in ("patch", "error", "property", "strength", "kind", "expected", "route", "targetkey", "mutationcommands"):
                self.assertNotIn(forbidden, serialized)
            self.assertEqual(result["renderedMutantsCreated"], 0)
            self.assertEqual(set(blind), {"schemaVersion", "holdoutCohort", "freezeStatus", "contractHash", "captureProfileHash", "reviewContextHash", "samples", "manifestHash"})
            for entry in blind["samples"]:
                self.assertEqual(set(entry), {"sampleId", "evidence", "contractHash", "captureProfileHash"})
            self.assertFalse((root / "blind" / "mutation-commands").exists())
            self.assertTrue((root / "operator" / "mutation-commands").exists())
            self.assertNotEqual([entry["sampleId"] for entry in blind["samples"]], [sample["sampleId"] for sample in build_samples("reduced", ID_SALT)])

    def test_operator_commands_are_not_recipe_patches(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_fixture_set(root, "reduced", ID_SALT)
            commands = [_load(path) for path in (root / "operator" / "mutation-commands").glob("*.json")]
            mutated = next(command for command in commands if command["mutationCommands"])
            self.assertEqual(mutated["schemaVersion"], "s0a-operator-mutation-command/v1")
            self.assertEqual(set(mutated["mutationCommands"][0]), {"operation", "targetKey", "value"})
            self.assertEqual(mutated["fixtureApplicationStatus"], "NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER")
            self.assertNotIn("patchOperations", mutated)
            self.assertEqual(mutated["commandHash"], normalized_sha256(mutated, ("commandHash",)))

    def test_capture_command_set_is_frozen_non_answer_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_fixture_set(root, "reduced", ID_SALT)
            command_set = _load(root / "operator" / "command-set.json")
            self.assertEqual(command_set["schemaVersion"], "s0a-operator-command-set/v1")
            self.assertEqual(command_set["freezeStatus"], "FROZEN_FOR_CAPTURE")
            self.assertEqual(command_set["commandSetHash"], normalized_sha256(command_set, ("commandSetHash",)))
            self.assertEqual(len(command_set["commands"]), 66)
            self.assertEqual(set(command_set["commands"][0]), {"sampleId", "commandHash"})
            serialized = normalized_json(command_set).lower()
            for forbidden in ("label", "route", "mutation", "targetkey", "value", "error", "strength", "kind"):
                self.assertNotIn(forbidden, serialized)

    def test_template_is_schema_aligned_and_never_claims_reviewed(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_fixture_set(root, "reduced", ID_SALT)
            labels = _load(root / "operator" / "calibration-labels.json")
            self.assertEqual(validate_label_schema_shape(labels), [])
            self.assertTrue(verify_labels(labels))
            self.assertEqual(labels["reviewStatus"], "TEMPLATE_NOT_REVIEWED")
            self.assertFalse(labels["frozen"])
            self.assertIsNone(labels["samples"][0]["groundTruthRoute"])
            self.assertIsNone(labels["samples"][0]["perRequirement"][0]["state"])

    def test_reduced_perfect_corpus_remains_advisory(self) -> None:
        temp, artifacts = self._frozen_bundle("reduced")
        try:
            result = self._score(artifacts, self._reviews(artifacts))
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")
            self.assertEqual(result["topRouteMetrics"]["evidenceInvalidRecall"], 1.0)
            self.assertEqual(validate_metrics_schema_shape(result), [])
            self.assertEqual(verify_metrics(result, artifacts["labels"]), [])
        finally:
            temp.cleanup()

    def test_only_complete_fresh_frozen_full_can_qualify(self) -> None:
        temp, artifacts = self._frozen_bundle("full", fresh_full=True)
        try:
            result = self._score(artifacts, self._reviews(artifacts))
            self.assertEqual(result["terminalStatus"], "S0A_GATE_QUALIFIED")
            self.assertEqual(result["perRequirementMetrics"]["qualificationPreconditions"]["labelCount"], 110)
        finally:
            temp.cleanup()

    def test_full_gate_rejects_replaced_ids_and_evidence_replay(self) -> None:
        temp, artifacts = self._frozen_bundle("full", fresh_full=True)
        try:
            changed = copy.deepcopy(artifacts["labels"])
            changed["samples"][0]["sampleId"] = "s0a-aaaaaaaaaaaaaaaaaaaa"
            _rehash(changed, "manifestHash")
            with self.assertRaises(CalibrationValidationError):
                calculate_metrics(changed, self._reviews(artifacts), artifacts["blind"], artifacts["evidence"], artifacts["context"], artifacts["ledger"])
            replayed = copy.deepcopy(artifacts["labels"])
            replayed["samples"][1]["evidenceHash"] = replayed["samples"][0]["evidenceHash"]
            _rehash(replayed, "manifestHash")
            self.assertTrue(any("must not be reused" in error for error in verify_labels(replayed)))
        finally:
            temp.cleanup()

    def test_full_gate_allows_unscored_contract_ambiguous_route_errors(self) -> None:
        temp, artifacts = self._frozen_bundle("full", fresh_full=True)
        try:
            uncertain = [sample for sample in artifacts["labels"]["samples"] if sample["groundTruthRoute"] == "VISUAL_UNCERTAIN"]
            self.assertGreaterEqual(len(uncertain), 2)
            self.assertTrue(all(sample["isBoundary"] for sample in uncertain[:2]))
            ambiguous_routes = self._reviews(artifacts, {sample["sampleId"]: "CONTRACT_AMBIGUOUS" for sample in uncertain[:2]})
            result = self._score(artifacts, ambiguous_routes)

            self.assertEqual(result["terminalStatus"], "S0A_GATE_QUALIFIED")
            self.assertEqual(result["topRouteMetrics"]["confusionMatrix"]["VISUAL_UNCERTAIN"]["CONTRACT_AMBIGUOUS"], 6)
            self.assertEqual(result["topRouteMetrics"]["evidenceInvalidRecall"], 1.0)
            self.assertEqual(result["perRequirementMetrics"]["falsePassCount"], 0)
            self.assertLessEqual(result["perRequirementMetrics"]["falseFailRate"], 0.10)
            self.assertLessEqual(result["perRequirementMetrics"]["nonBoundaryUncertainRate"], 0.15)
            self.assertGreaterEqual(result["stability"]["perRequirementAgreement"], 0.90)
            self.assertGreaterEqual(result["stability"]["topRouteAgreement"], 0.90)
            self.assertEqual(verify_metrics(result, artifacts["labels"]), [])
        finally:
            temp.cleanup()

    def test_authorized_gate_thresholds_still_fail_closed(self) -> None:
        temp, artifacts = self._frozen_bundle("full", fresh_full=True)
        try:
            labels = artifacts["labels"]["samples"]

            label = next(sample for sample in artifacts["labels"]["samples"] if sample["groundTruthRoute"] == "VISUAL_FAIL")
            false_pass = self._reviews(artifacts, {label["sampleId"]: "VISUAL_PASS"})
            result = self._score(artifacts, false_pass)
            self.assertEqual(result["perRequirementMetrics"]["falsePassCount"], 3)
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")

            pass_labels = [sample for sample in labels if sample["groundTruthRoute"] == "VISUAL_PASS"]
            false_fail = self._reviews(artifacts, {sample["sampleId"]: "VISUAL_FAIL" for sample in pass_labels[:3]})
            result = self._score(artifacts, false_fail)
            self.assertGreater(result["perRequirementMetrics"]["falseFailRate"], 0.10)
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")

            non_boundary = [sample for sample in labels if sample["eligibleForVisualMetrics"] and not sample["isBoundary"]]
            uncertain_count = int(0.15 * len(non_boundary)) + 1
            excessive_uncertain = self._reviews(artifacts, {sample["sampleId"]: "VISUAL_UNCERTAIN" for sample in non_boundary[:uncertain_count]})
            result = self._score(artifacts, excessive_uncertain)
            self.assertGreater(result["perRequirementMetrics"]["nonBoundaryUncertainRate"], 0.15)
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")

            invalid = next(sample for sample in labels if sample["groundTruthRoute"] == "EVIDENCE_INVALID")
            missed_invalid = self._reviews(artifacts, {invalid["sampleId"]: "CONTRACT_AMBIGUOUS"})
            result = self._score(artifacts, missed_invalid)
            self.assertLess(result["topRouteMetrics"]["evidenceInvalidRecall"], 1.0)
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")

            boundary = [sample for sample in labels if sample["groundTruthRoute"] == "VISUAL_UNCERTAIN"]
            unstable = self._reviews(artifacts)
            unstable_ids = {sample["sampleId"] for sample in boundary[:12]}
            for record in unstable["sessions"][0]["reviews"]:
                if record["sampleId"] in unstable_ids:
                    label_by_id = next(sample for sample in boundary if sample["sampleId"] == record["sampleId"])
                    record["report"] = self._report(label_by_id, artifacts["context"], "VISUAL_PASS")
            self._rehash_corpus(unstable)
            result = self._score(artifacts, unstable)
            self.assertLess(result["stability"]["perRequirementAgreement"], 0.90)
            self.assertLess(result["stability"]["topRouteAgreement"], 0.90)
            self.assertEqual(result["terminalStatus"], "S0A_ADVISORY_ONLY")
        finally:
            temp.cleanup()

    def test_scorer_rejects_bad_session_shapes_duplicates_and_illegal_reports(self) -> None:
        temp, artifacts = self._frozen_bundle("reduced")
        try:
            reviews = self._reviews(artifacts)
            reviews["sessions"] = reviews["sessions"][:2]
            _rehash(reviews, "corpusHash")
            self.assertIn("exactly three", " ".join(validate_reviews(artifacts["labels"], reviews, artifacts["context"])))

            reviews = self._reviews(artifacts)
            reviews["sessions"][0]["reviews"].append(copy.deepcopy(reviews["sessions"][0]["reviews"][0]))
            self._rehash_corpus(reviews)
            self.assertTrue(any("duplicate (sampleId, sessionId)" in error for error in validate_reviews(artifacts["labels"], reviews, artifacts["context"])))

            reviews = self._reviews(artifacts)
            reviews["sessions"][1]["reviewerSessionId"] = reviews["sessions"][0]["reviewerSessionId"]
            self._rehash_corpus(reviews)
            self.assertTrue(any("duplicate reviewerSessionId" in error for error in validate_reviews(artifacts["labels"], reviews, artifacts["context"])))

            reviews = self._reviews(artifacts)
            reviews["sessions"][0]["reviews"].pop()
            self._rehash_corpus(reviews)
            self.assertTrue(any("missing or extra samples" in error for error in validate_reviews(artifacts["labels"], reviews, artifacts["context"])))

            reviews = self._reviews(artifacts)
            requirement = reviews["sessions"][0]["reviews"][0]["report"]["perRequirement"][0]
            requirement["designRequirementId"] = "unknown.requirement"
            requirement["state"] = "invalid"
            self._rehash_corpus(reviews)
            errors = validate_reviews(artifacts["labels"], reviews, artifacts["context"])
            self.assertTrue(any("unknown designRequirementId" in error for error in errors))
            self.assertTrue(any("state/countable/reasonCode" in error for error in errors))
        finally:
            temp.cleanup()

    def test_expanded_full_requires_zero_false_pass_reduced_provenance(self) -> None:
        temp, artifacts = self._frozen_bundle("full", fresh_full=False)
        try:
            self.assertEqual(artifacts["context"]["holdoutIsolationMode"], "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS")
            with self.assertRaises(CalibrationValidationError) as raised:
                self._score(artifacts, self._reviews(artifacts))
            self.assertIn("reducedMetricsHash", str(raised.exception))
        finally:
            temp.cleanup()

    def test_template_and_parser_negative_cases_return_validation_errors(self) -> None:
        self.assertTrue(validate_label_schema_shape([]))
        self.assertTrue(validate_metrics_schema_shape([]))
        malformed = {"schemaVersion": "calibration-labels/v2", "holdoutCohort": [], "reviewStatus": {}, "frozen": False, "reviewer": None, "frozenAt": None, "samples": [], "manifestHash": []}
        self.assertTrue(validate_label_schema_shape(malformed))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_fixture_set(root, "reduced", ID_SALT)
            labels = _load(root / "operator" / "calibration-labels.json")
            labels["samples"][0]["perRequirement"][0]["state"] = "pass"
            _rehash(labels, "manifestHash")
            self.assertTrue(any("template perRequirement state" in error for error in validate_label_schema_shape(labels)))

    def test_metrics_self_hash_and_arithmetic_are_verified(self) -> None:
        temp, artifacts = self._frozen_bundle("reduced")
        try:
            result = self._score(artifacts, self._reviews(artifacts))
            forged = copy.deepcopy(result)
            forged["topRouteMetrics"]["evidenceInvalidRecall"] = 0.0
            _rehash(forged, "reportHash")
            self.assertTrue(any("recall arithmetic" in error for error in verify_metrics(forged, artifacts["labels"])))
            forged = copy.deepcopy(result)
            forged["topRouteMetrics"]["confusionMatrix"]["VISUAL_PASS"]["VISUAL_PASS"] = -1
            _rehash(forged, "reportHash")
            self.assertTrue(validate_metrics_schema_shape(forged))
            forged = copy.deepcopy(result)
            forged["perRequirementMetrics"]["falsePassCount"] = 99
            _rehash(forged, "reportHash")
            self.assertTrue(any("false-pass arithmetic" in error for error in verify_metrics(forged, artifacts["labels"])))
        finally:
            temp.cleanup()

    def test_regeneration_refuses_applied_or_frozen_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_fixture_set(root, "reduced", ID_SALT)
            command_path = next((root / "operator" / "mutation-commands").glob("*.json"))
            command = _load(command_path)
            command["fixtureApplicationStatus"] = "APPLIED"
            _rehash(command, "commandHash")
            command_path.write_text(json.dumps(command), encoding="utf-8")
            with self.assertRaises(CalibrationValidationError):
                write_fixture_set(root, "reduced", ID_SALT)

    def test_cli_score_output_is_write_once(self) -> None:
        temp, artifacts = self._frozen_bundle("reduced")
        try:
            root = artifacts["root"]
            reviews = self._reviews(artifacts)
            paths = {
                "labels": root / "labels.frozen.json",
                "reviews": root / "reviews.json",
                "blind": root / "blind.frozen.json",
                "bundle": root / "bundle.frozen.json",
                "context": root / "context.frozen.json",
                "ledger": root / "ledger.json",
                "output": root / "metrics.json",
            }
            for name, document in (
                ("labels", artifacts["labels"]), ("reviews", reviews), ("blind", artifacts["blind"]),
                ("bundle", artifacts["evidence"]), ("context", artifacts["context"]), ("ledger", artifacts["ledger"]),
            ):
                paths[name].write_text(json.dumps(document), encoding="utf-8")
            paths["output"].write_text("write-once", encoding="utf-8")
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                main([
                    "score", "--labels", str(paths["labels"]), "--reviews", str(paths["reviews"]),
                    "--blind-manifest", str(paths["blind"]), "--evidence-bundle", str(paths["bundle"]),
                    "--review-context", str(paths["context"]), "--operator-ledger", str(paths["ledger"]),
                    "--output", str(paths["output"]),
                ])
            self.assertEqual(paths["output"].read_text(encoding="utf-8"), "write-once")
        finally:
            temp.cleanup()


if __name__ == "__main__":
    unittest.main()
