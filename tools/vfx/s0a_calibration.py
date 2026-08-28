"""W24 S0a calibration fixture generator and frozen-corpus scorer.

This module produces lightweight *operator mutation commands*, not Unity
assets or production Recipe Patches. A Unity fixture adapter must apply each
command and capture evidence before a sample can be reviewed.

Hashes here are integrity identifiers, not signatures. If writers are mutually
untrusted, pin the frozen artifact hashes in immutable storage or sign them.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import secrets
import shutil
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable


GENERATOR_SCHEMA_VERSION = "s0a-calibration/v3"
LABEL_SCHEMA_VERSION = "calibration-labels/v2"
BLIND_SCHEMA_VERSION = "s0a-blind-evidence-index/v3"
CONTEXT_SCHEMA_VERSION = "s0a-review-freeze-context/v2"
BUNDLE_SCHEMA_VERSION = "s0a-evidence-bundle/v2"
REVIEW_CORPUS_SCHEMA_VERSION = "s0a-isolated-review-corpus/v2"
OPERATOR_COMMAND_SCHEMA_VERSION = "s0a-operator-mutation-command/v1"
OPERATOR_COMMAND_SET_SCHEMA_VERSION = "s0a-operator-command-set/v1"
OPERATOR_COMMAND_SET_CAPTURE_FROZEN = "FROZEN_FOR_CAPTURE"
OPERATOR_EFFECT_ID = "sustained_flame_3d"
EVIDENCE_TEMPLATE_SCHEMA_VERSION = "s0a-evidence-template/v2"
HASH_PREFIX = "sha256:"
HASH_PATTERN = re.compile(r"^sha256:[a-f0-9]{64}$")
RFC3339_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$")
SAMPLE_ID_PATTERN = re.compile(r"^s0a-[a-f0-9]{20}$")

REVIEW_ROUTES = frozenset({"VISUAL_PASS", "VISUAL_FAIL", "EVIDENCE_INVALID", "CONTRACT_AMBIGUOUS", "VISUAL_UNCERTAIN"})
TRI_STATES = frozenset({"pass", "fail", "uncertain"})
REASON_CODES = frozenset({"OBSERVED", "EVIDENCE_INVALID", "CONTRACT_AMBIGUOUS", "VISUAL_UNCERTAIN"})
HUMAN_REVIEWED = "HUMAN_REVIEWED"
TEMPLATE_NOT_REVIEWED = "TEMPLATE_NOT_REVIEWED"
FROZEN = "FROZEN"
TEMPLATE_NOT_FROZEN = "TEMPLATE_NOT_FROZEN"

# Eleven visual faults plus one deliberate evidence/capture fault.
ERROR_SPECS: tuple[dict[str, str], ...] = (
    {"id": "synchronous_rotation", "requirementId": "flame.fragments.independent_motion", "targetKey": "Fragments.sharedParentAngularVelocity", "obvious": "180deg_per_second", "boundary": "22deg_per_second"},
    {"id": "steady_state_drift", "requirementId": "flame.steady_state.distribution", "targetKey": "Flame.steadyStateLinearDrift", "obvious": "0.90_units_per_second", "boundary": "0.11_units_per_second"},
    {"id": "loop_seam", "requirementId": "flame.steady_state.continuity", "targetKey": "Flame.loopResetDiscontinuity", "obvious": "0.85_normalized_delta", "boundary": "0.14_normalized_delta"},
    {"id": "stop_particle_residue", "requirementId": "flame.cleanup.particles", "targetKey": "Particles.stopResidualSeconds", "obvious": "2.50_seconds", "boundary": "0.18_seconds"},
    {"id": "stop_light_residue", "requirementId": "flame.cleanup.light", "targetKey": "Light.stopResidualSeconds", "obvious": "2.50_seconds", "boundary": "0.18_seconds"},
    {"id": "smoke_occludes_subject", "requirementId": "flame.subject.readability", "targetKey": "Smoke.subjectOcclusionFraction", "obvious": "0.78", "boundary": "0.26"},
    {"id": "layering_inversion", "requirementId": "flame.layers.primary_before_smoke", "targetKey": "Renderer.primarySmokeSortingOrder", "obvious": "inverted", "boundary": "near_equal"},
    {"id": "missing_ignition", "requirementId": "flame.start.ignition", "targetKey": "StateMachine.ignitionEnabled", "obvious": "false", "boundary": "delay_0.42_seconds"},
    {"id": "hard_stop_cut", "requirementId": "flame.stop.continuity", "targetKey": "StateMachine.stopContinuityMode", "obvious": "clear_immediate", "boundary": "fade_0.04_seconds"},
    {"id": "additive_fake_light", "requirementId": "flame.lighting.receiver_response", "targetKey": "Light.enabled", "obvious": "false", "boundary": "intensity_0.02"},
    {"id": "camera_or_scale_error", "requirementId": "flame.capture.camera_and_scale", "targetKey": "Capture.cameraScaleOffset", "obvious": "scale_2.20", "boundary": "scale_1.12"},
    {"id": "evidence_metadata_mismatch", "requirementId": "flame.capture.metadata_integrity", "targetKey": "Capture.frameManifestIntegrity", "obvious": "missing_key_frame", "boundary": "sha256_mismatch"},
)
VISUAL_ERROR_SPECS = ERROR_SPECS[:-1]
INVALID_ERROR = ERROR_SPECS[-1]

# Public bridge for the Unity S0a fixture adapter.  This intentionally exposes only
# the generated operator vocabulary, never error classes, labels, or expected routes.
# Values are string tokens so the adapter must perform its own explicit typed parsing.
def operator_mutation_vocabulary() -> dict[str, tuple[str, ...]]:
    vocabulary: dict[str, set[str]] = {}
    for spec in ERROR_SPECS:
        vocabulary.setdefault(spec["targetKey"], set()).update((spec["obvious"], spec["boundary"]))
    return {target: tuple(sorted(values)) for target, values in sorted(vocabulary.items())}

COHORTS = {
    "reduced": {"fail": 36, "pass": 12, "uncertain": 12, "invalid": 6},
    "extension": {"fail": 24, "pass": 8, "uncertain": 8, "invalid": 4},
    "full": {"fail": 60, "pass": 20, "uncertain": 20, "invalid": 10},
}
COHORT_IDS = {"reduced": "reduced-36-12-12-6", "extension": "extension-24-8-8-4", "full": "full-60-20-20-10"}
COHORT_ID_VALUES = frozenset(COHORT_IDS.values())

LABEL_TOP_LEVEL_FIELDS = frozenset({"schemaVersion", "holdoutCohort", "reviewStatus", "frozen", "reviewer", "frozenAt", "samples", "manifestHash"})
LABEL_SAMPLE_REQUIRED_FIELDS = frozenset({"sampleId", "requirementId", "groundTruthRoute", "perRequirement", "labelSource", "reviewer", "visuallyObservable", "eligibleForVisualMetrics", "isBoundary", "evidenceHash", "verdictVersion"})
LABEL_SAMPLE_FIELDS = LABEL_SAMPLE_REQUIRED_FIELDS | {"adjudicationNotes"}
METRICS_TOP_LEVEL_FIELDS = frozenset({"holdoutCohort", "labelManifestHash", "perRequirementMetrics", "topRouteMetrics", "stability", "terminalStatus", "reportHash"})
PER_REQUIREMENT_METRIC_FIELDS = frozenset({"falsePassCount", "knownVisualFailCount", "falseFailRate", "nonBoundaryUncertainRate", "confusionMatricesByRequirementType", "qualificationPreconditions"})
TOP_ROUTE_METRIC_FIELDS = frozenset({"evidenceInvalidRecall", "evidenceInvalidDetected", "evidenceInvalidExpected", "confusionMatrix"})
STABILITY_FIELDS = frozenset({"isolatedSessionCount", "perRequirementAgreement", "topRouteAgreement"})
FREEZE_CONTEXT_HASH_FIELDS = (
    "qaPromptHash", "modelVersionHash", "imageInputStrategyHash", "threeStateRulesHash", "aggregationRulesHash", "visualReviewSchemaHash",
    "contractHash", "buildHash", "captureProfileHash", "frameTableHash", "sceneHash", "prefabManifestHash", "captureToolHash",
)
CONTEXT_FIELDS = frozenset({"schemaVersion", "holdoutCohort", "freezeStatus", "holdoutIsolationMode", "operatorLedgerHash", "reducedMetricsHash", "contextHash", *FREEZE_CONTEXT_HASH_FIELDS})
BLIND_FIELDS = frozenset({"schemaVersion", "holdoutCohort", "freezeStatus", "contractHash", "captureProfileHash", "reviewContextHash", "samples", "manifestHash"})
BUNDLE_FIELDS = frozenset({"schemaVersion", "holdoutCohort", "freezeStatus", "operatorLedgerHash", "blindManifestHash", "captureProfileHash", "samples", "bundleHash"})
LEDGER_FIELDS = frozenset({"schemaVersion", "holdoutCohort", "holdoutMode", "purpose", "sampleIdSalt", "samples", "ledgerHash"})
REVIEW_CORPUS_FIELDS = frozenset({"schemaVersion", "blindManifestHash", "evidenceBundleHash", "reviewContextHash", "sessions", "corpusHash"})
REVIEW_SESSION_FIELDS = frozenset({"sessionId", "reviewerSessionId", "isolated", "startedAt", "completedAt", "reviews", "sessionHash"})
VFX_REPORT_REQUIRED_FIELDS = frozenset({"reviewVersion", "candidateId", "contractHash", "buildHash", "captureProfileHash", "evidenceHashes", "imageInputRead", "evidenceStatus", "contractStatus", "s0aTerminalStatus", "qaGateAuthority", "topLevelRoute", "perRequirement", "sealedReportHash"})
VFX_REPORT_FIELDS = VFX_REPORT_REQUIRED_FIELDS | {"conflicts"}
VFX_REQUIREMENT_FIELDS = frozenset({"designRequirementId", "state", "countable", "reasonCode", "stateRef", "frameNumbers", "imageRegion", "observation"})


class CalibrationValidationError(ValueError):
    """Raised for malformed, unfrozen, unbound, or unsafe S0a artifacts."""


def normalized_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def normalized_sha256(value: dict[str, Any], omit: Iterable[str] = ()) -> str:
    if not isinstance(value, dict):
        raise CalibrationValidationError("hashable calibration document must be an object")
    material = copy.deepcopy(value)
    for key in omit:
        material.pop(key, None)
    return HASH_PREFIX + hashlib.sha256(normalized_json(material).encode("utf-8")).hexdigest()


def _with_hash(document: dict[str, Any], field: str) -> dict[str, Any]:
    if not isinstance(document, dict):
        raise CalibrationValidationError("document must be an object before hashing")
    result = copy.deepcopy(document)
    result.pop(field, None)
    result[field] = normalized_sha256(result, (field,))
    return result


def _is_hash(value: Any) -> bool:
    return isinstance(value, str) and bool(HASH_PATTERN.fullmatch(value))


def _is_nonempty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _is_route(value: Any) -> bool:
    return isinstance(value, str) and value in REVIEW_ROUTES


def _is_state(value: Any) -> bool:
    return isinstance(value, str) and value in TRI_STATES


def _is_one_of(value: Any, allowed: Iterable[str]) -> bool:
    """Safe enum check for untrusted JSON values (including arrays/objects)."""
    return isinstance(value, str) and value in allowed


def _is_nonnegative_count(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _is_rate(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and 0 <= value <= 1


def _valid_datetime(value: Any) -> bool:
    if not isinstance(value, str) or not RFC3339_PATTERN.fullmatch(value):
        return False
    try:
        datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return False
    return True


def _hash_matches(document: Any, field: str) -> bool:
    return isinstance(document, dict) and _is_hash(document.get(field)) and document[field] == normalized_sha256(document, (field,))


def _exact_fields(document: Any, fields: frozenset[str], name: str, errors: list[str]) -> bool:
    if not isinstance(document, dict):
        errors.append(f"{name} must be an object")
        return False
    if set(document) != fields:
        errors.append(f"{name} fields are invalid")
        return False
    return True


def _require_sample_id_salt(sample_id_salt: str | None) -> str:
    if not isinstance(sample_id_salt, str) or len(sample_id_salt) < 16:
        raise CalibrationValidationError("sample_id_salt must be an operator-only string of at least 16 characters")
    return sample_id_salt


def _sample_id(source_namespace: str, ordinal: int, salt: str) -> str:
    return "s0a-" + hashlib.sha256(f"W24-S0A|anonymous-id|{salt}|{source_namespace}|{ordinal:03d}".encode("utf-8")).hexdigest()[:20]


def _fixed_seed(source_namespace: str, ordinal: int) -> int:
    return int.from_bytes(hashlib.sha256(f"W24-S0A-seed|{source_namespace}|{ordinal}".encode("utf-8")).digest()[:4], "big")


def _error_for(kind: str, ordinal: int) -> dict[str, str] | None:
    if kind == "pass":
        return None
    return INVALID_ERROR if kind == "invalid" else VISUAL_ERROR_SPECS[ordinal % len(VISUAL_ERROR_SPECS)]


def _strength_for(kind: str, ordinal: int) -> str:
    if kind == "pass":
        return "control"
    if kind == "uncertain":
        return "boundary"
    return "boundary" if ordinal % 3 == 2 else "obvious"


def _expected(kind: str) -> tuple[str, str, bool, bool]:
    if kind == "fail":
        return "VISUAL_FAIL", "fail", True, False
    if kind == "pass":
        return "VISUAL_PASS", "pass", True, False
    if kind == "uncertain":
        return "VISUAL_UNCERTAIN", "uncertain", True, True
    return "EVIDENCE_INVALID", "uncertain", False, False


def _build_sample(source: str, kind: str, ordinal: int, source_ordinal: int, salt: str) -> dict[str, Any]:
    error = _error_for(kind, ordinal)
    route, state, observable, boundary = _expected(kind)
    strength = _strength_for(kind, ordinal)
    commands = [] if error is None else [{"operation": "set", "targetKey": error["targetKey"], "value": error[strength]}]
    requirement_id = error["requirementId"] if error else "flame.baseline.required_visuals"
    return {
        "sampleId": _sample_id(source, source_ordinal, salt), "sourceNamespace": source, "fixedSeed": _fixed_seed(source, source_ordinal), "kind": kind,
        "injection": {"errorClass": error["id"] if error else "baseline_control", "strength": strength, "mutationCommands": commands},
        "labelBlueprint": {"groundTruthRoute": route, "perRequirement": [{"requirementId": requirement_id, "state": state}], "requirementId": requirement_id, "visuallyObservable": observable, "eligibleForVisualMetrics": observable, "isBoundary": boundary},
    }


def _sample_plan(cohort: str, full_mode: str) -> list[tuple[str, dict[str, int]]]:
    if cohort not in COHORTS:
        raise CalibrationValidationError(f"Unknown cohort {cohort!r}; expected one of {sorted(COHORTS)}")
    if cohort != "full":
        return [(cohort, COHORTS[cohort])]
    if full_mode == "expanded":
        return [("reduced", COHORTS["reduced"]), ("extension", COHORTS["extension"])]
    if full_mode == "fresh":
        return [("fresh-full-v1", COHORTS["full"])]
    raise CalibrationValidationError("full_mode must be 'expanded' or 'fresh'")


def build_samples(cohort: str = "reduced", sample_id_salt: str | None = None, full_mode: str = "expanded") -> list[dict[str, Any]]:
    """Build reproducible operator samples; callers must provide an operator salt."""
    salt = _require_sample_id_salt(sample_id_salt)
    samples: list[dict[str, Any]] = []
    for source, counts in _sample_plan(cohort, full_mode):
        source_ordinal = 0
        for kind in ("fail", "pass", "uncertain", "invalid"):
            for ordinal in range(counts[kind]):
                samples.append(_build_sample(source, kind, ordinal, source_ordinal, salt))
                source_ordinal += 1
    return samples


def _blind_order(samples: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return sorted(samples, key=lambda sample: hashlib.sha256(f"W24-S0a-blind-order|{sample['sampleId']}".encode("utf-8")).hexdigest())


def _operator_command_document(sample: dict[str, Any]) -> dict[str, Any]:
    return _with_hash({
        "schemaVersion": OPERATOR_COMMAND_SCHEMA_VERSION, "sampleId": sample["sampleId"], "effectId": OPERATOR_EFFECT_ID, "fixedSeed": sample["fixedSeed"],
        "mutationCommands": sample["injection"]["mutationCommands"], "fixtureApplicationStatus": "NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER",
        "operatorInstruction": "A later Unity fixture adapter must execute this command and capture evidence.",
    }, "commandHash")


def _operator_command_set_document(samples: list[dict[str, Any]], cohort: str) -> dict[str, Any]:
    """Non-answer anchor consumed by Unity before a formal capture.

    It deliberately contains only anonymous sample IDs and individual command hashes: no
    mutation target, failure class, route, strength, label, blind ordering, or ledger data.
    This lets the capture harness reject a replaced command without learning the answer key.
    """
    commands = [_operator_command_document(sample) for sample in samples]
    return _with_hash({
        "schemaVersion": OPERATOR_COMMAND_SET_SCHEMA_VERSION,
        "holdoutCohort": COHORT_IDS[cohort],
        "freezeStatus": OPERATOR_COMMAND_SET_CAPTURE_FROZEN,
        "commands": [
            {"sampleId": command["sampleId"], "commandHash": command["commandHash"]}
            for command in sorted(commands, key=lambda item: item["sampleId"])
        ],
    }, "commandSetHash")


def _evidence_template(sample_id: str) -> dict[str, Any]:
    return {"schemaVersion": EVIDENCE_TEMPLATE_SCHEMA_VERSION, "sampleId": sample_id, "contractHash": None, "captureProfileHash": None, "captureStatus": "NOT_CAPTURED", "frames": [], "evidenceHash": None, "note": "Generated fixture placeholder only; no rendered evidence exists yet."}


def _mode_for_template(cohort: str, full_mode: str) -> str:
    if cohort == "reduced":
        return "INITIAL_REDUCED"
    if cohort == "extension":
        return "EXTENSION_ONLY"
    return "FRESH_INDEPENDENT_FULL" if full_mode == "fresh" else "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS"


def _review_freeze_context_template(cohort: str, ledger_hash: str, full_mode: str) -> dict[str, Any]:
    return _with_hash({"schemaVersion": CONTEXT_SCHEMA_VERSION, "holdoutCohort": COHORT_IDS[cohort], "freezeStatus": TEMPLATE_NOT_FROZEN, "holdoutIsolationMode": _mode_for_template(cohort, full_mode), "operatorLedgerHash": ledger_hash, "reducedMetricsHash": None, **{field: None for field in FREEZE_CONTEXT_HASH_FIELDS}}, "contextHash")


def _blind_template(samples: list[dict[str, Any]], cohort: str, ledger_hash: str) -> dict[str, Any]:
    return _with_hash({
        "schemaVersion": BLIND_SCHEMA_VERSION, "holdoutCohort": COHORT_IDS[cohort], "freezeStatus": TEMPLATE_NOT_FROZEN,
        "contractHash": None, "captureProfileHash": None, "reviewContextHash": None,
        "samples": [{"sampleId": sample["sampleId"], "evidence": {"evidenceManifest": f"evidence/{sample['sampleId']}.evidence.json", "evidenceIdentity": sample["sampleId"]}, "contractHash": None, "captureProfileHash": None} for sample in _blind_order(samples)],
    }, "manifestHash")


def _evidence_bundle_template(samples: list[dict[str, Any]], cohort: str, ledger_hash: str) -> dict[str, Any]:
    return _with_hash({"schemaVersion": BUNDLE_SCHEMA_VERSION, "holdoutCohort": COHORT_IDS[cohort], "freezeStatus": TEMPLATE_NOT_FROZEN, "operatorLedgerHash": ledger_hash, "blindManifestHash": None, "captureProfileHash": None, "samples": [{"sampleId": sample["sampleId"], "evidenceHash": None} for sample in samples]}, "bundleHash")


def _label_template(sample: dict[str, Any]) -> dict[str, Any]:
    blueprint = sample["labelBlueprint"]
    return {"sampleId": sample["sampleId"], "requirementId": blueprint["requirementId"], "groundTruthRoute": None, "perRequirement": [{"requirementId": blueprint["requirementId"], "state": None}], "labelSource": "PENDING_HUMAN_ADJUDICATION", "reviewer": None, "visuallyObservable": None, "eligibleForVisualMetrics": None, "isBoundary": blueprint["isBoundary"], "evidenceHash": None, "verdictVersion": None, "adjudicationNotes": ""}


def _ledger_document(samples: list[dict[str, Any]], cohort: str, salt: str, full_mode: str) -> dict[str, Any]:
    return _with_hash({
        "schemaVersion": GENERATOR_SCHEMA_VERSION, "holdoutCohort": COHORT_IDS[cohort], "holdoutMode": full_mode if cohort == "full" else "standalone",
        "purpose": "operator-only generation trace; never a label authority or blind-review input", "sampleIdSalt": salt,
        "samples": [{"sampleId": sample["sampleId"], "sourceNamespace": sample["sourceNamespace"], "fixedSeed": sample["fixedSeed"], "kind": sample["kind"], "injection": sample["injection"], "labelBlueprint": sample["labelBlueprint"]} for sample in samples],
    }, "ledgerHash")


def _read_json_object(path: Path) -> dict[str, Any]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise CalibrationValidationError(f"Cannot read JSON artifact {path}: {exc}") from exc
    if not isinstance(document, dict):
        raise CalibrationValidationError(f"JSON artifact {path} must be an object")
    return document


def _is_unreviewed_generator_template(labels: Any) -> bool:
    if not isinstance(labels, dict) or labels.get("reviewStatus") != TEMPLATE_NOT_REVIEWED or labels.get("frozen") is not False or labels.get("reviewer") is not None or labels.get("frozenAt") is not None or not _hash_matches(labels, "manifestHash"):
        return False
    samples = labels.get("samples")
    if not isinstance(samples, list) or not samples:
        return False
    for sample in samples:
        if not isinstance(sample, dict) or sample.get("labelSource") != "PENDING_HUMAN_ADJUDICATION":
            return False
        if any(sample.get(field) is not None for field in ("groundTruthRoute", "reviewer", "visuallyObservable", "eligibleForVisualMetrics", "evidenceHash", "verdictVersion")):
            return False
        per = sample.get("perRequirement")
        if not isinstance(per, list) or not per or any(not isinstance(item, dict) or item.get("state") is not None for item in per):
            return False
    return True


def _is_disposable_generator_document(document: Any) -> bool:
    if not isinstance(document, dict):
        return False
    schema = document.get("schemaVersion")
    if schema == OPERATOR_COMMAND_SCHEMA_VERSION:
        return document.get("fixtureApplicationStatus") == "NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER" and _hash_matches(document, "commandHash")
    if schema == OPERATOR_COMMAND_SET_SCHEMA_VERSION:
        return document.get("freezeStatus") == OPERATOR_COMMAND_SET_CAPTURE_FROZEN and _hash_matches(document, "commandSetHash")
    if schema == EVIDENCE_TEMPLATE_SCHEMA_VERSION:
        return document.get("captureStatus") == "NOT_CAPTURED" and document.get("evidenceHash") is None
    if schema == LABEL_SCHEMA_VERSION:
        return _is_unreviewed_generator_template(document)
    if schema == BLIND_SCHEMA_VERSION:
        return document.get("freezeStatus") == TEMPLATE_NOT_FROZEN and _hash_matches(document, "manifestHash")
    if schema == CONTEXT_SCHEMA_VERSION:
        return document.get("freezeStatus") == TEMPLATE_NOT_FROZEN and _hash_matches(document, "contextHash")
    if schema == BUNDLE_SCHEMA_VERSION:
        return document.get("freezeStatus") == TEMPLATE_NOT_FROZEN and _hash_matches(document, "bundleHash")
    if schema == GENERATOR_SCHEMA_VERSION:
        return _hash_matches(document, "ledgerHash")
    # One-way migration from this generator's own pre-split v2 templates.
    # They are only removable when their canonical content hash still matches.
    if schema == "s0a-blind-evidence-index/v2":
        return set(document) == {"schemaVersion", "holdoutCohort", "contractHash", "captureProfileHash", "samples", "manifestHash"} and _hash_matches(document, "manifestHash")
    if schema == "s0a-calibration/v2":
        return set(document) == {"schemaVersion", "holdoutCohort", "purpose", "samples", "ledgerHash"} and _hash_matches(document, "ledgerHash")
    return False


def _safe_remove_generated_directory(path: Path) -> None:
    if not path.exists():
        return
    if not path.is_dir():
        raise CalibrationValidationError(f"Expected generated directory at {path}")
    allowed_directories = {Path(".")}
    if path.name == "blind":
        allowed_directories.add(Path("evidence"))
    elif path.name == "operator":
        allowed_directories.add(Path("mutation-commands"))
    for artifact in path.rglob("*"):
        if artifact.is_dir():
            if artifact.relative_to(path) not in allowed_directories:
                raise CalibrationValidationError(f"Refusing to remove unexpected generator subdirectory {artifact}")
            continue
        if artifact.suffix != ".json":
            raise CalibrationValidationError(f"Refusing to remove non-JSON artifact {artifact}")
        if not _is_disposable_generator_document(_read_json_object(artifact)):
            raise CalibrationValidationError(f"Refusing to remove frozen, captured, applied, or foreign artifact {artifact}")
    try:
        shutil.rmtree(path)
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot remove disposable generator directory {path}: {exc}") from exc


def _prepare_fixture_output(output_dir: Path) -> None:
    try:
        output_dir.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot prepare fixture output directory {output_dir}: {exc}") from exc
    for directory in (output_dir / "blind", output_dir / "operator"):
        _safe_remove_generated_directory(directory)
    # Remove only exact legacy generator-owned paths after the same checks.
    for name in ("blind-submission-manifest.json", "generation-ledger.json", "calibration-labels.json", "review-freeze-context.json", "evidence-bundle.json"):
        legacy = output_dir / name
        if legacy.exists():
            if not _is_disposable_generator_document(_read_json_object(legacy)):
                raise CalibrationValidationError(f"Refusing to replace non-template artifact {legacy}")
            try:
                legacy.unlink()
            except OSError as exc:
                raise CalibrationValidationError(f"Cannot remove obsolete generator fixture {legacy}: {exc}") from exc
    for directory in (output_dir / "patches", output_dir / "mutation-commands", output_dir / "evidence"):
        _safe_remove_generated_directory(directory)


def _resolve_sample_id_salt(output_dir: Path, supplied_salt: str | None) -> str:
    if supplied_salt is not None:
        return _require_sample_id_salt(supplied_salt)
    for ledger_path in (output_dir / "operator" / "generation-ledger.json", output_dir / "generation-ledger.json"):
        if ledger_path.exists():
            existing = _read_json_object(ledger_path).get("sampleIdSalt")
            if existing is not None:
                return _require_sample_id_salt(existing)
    return secrets.token_hex(32)


def _write_new_json(path: Path, document: dict[str, Any]) -> None:
    if path.exists():
        raise CalibrationValidationError(f"Refusing to overwrite existing write-once artifact {path}")
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot write artifact {path}: {exc}") from exc


def write_fixture_set(output_dir: Path, cohort: str = "reduced", sample_id_salt: str | None = None, fresh_full_holdout: bool = False) -> dict[str, Any]:
    """Write fixture contracts only; this never creates a rendered mutant."""
    full_mode = "fresh" if fresh_full_holdout else "expanded"
    salt = _resolve_sample_id_salt(output_dir, sample_id_salt)
    samples = build_samples(cohort, salt, full_mode)
    _prepare_fixture_output(output_dir)
    blind_dir, operator_dir = output_dir / "blind", output_dir / "operator"
    evidence_dir, commands_dir = blind_dir / "evidence", operator_dir / "mutation-commands"
    try:
        evidence_dir.mkdir(parents=True, exist_ok=True)
        commands_dir.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot create generator-owned fixture directories under {output_dir}: {exc}") from exc
    ledger = _ledger_document(samples, cohort, salt, full_mode)
    _write_new_json(operator_dir / "generation-ledger.json", ledger)
    for sample in samples:
        _write_new_json(commands_dir / f"{sample['sampleId']}.mutation-command.json", _operator_command_document(sample))
        _write_new_json(evidence_dir / f"{sample['sampleId']}.evidence.json", _evidence_template(sample["sampleId"]))
    _write_new_json(blind_dir / "blind-submission-manifest.json", _blind_template(samples, cohort, ledger["ledgerHash"]))
    _write_new_json(blind_dir / "review-freeze-context.json", _review_freeze_context_template(cohort, ledger["ledgerHash"], full_mode))
    _write_new_json(operator_dir / "evidence-bundle.json", _evidence_bundle_template(samples, cohort, ledger["ledgerHash"]))
    _write_new_json(operator_dir / "command-set.json", _operator_command_set_document(samples, cohort))
    labels = _with_hash({"schemaVersion": LABEL_SCHEMA_VERSION, "holdoutCohort": COHORT_IDS[cohort], "reviewStatus": TEMPLATE_NOT_REVIEWED, "frozen": False, "reviewer": None, "frozenAt": None, "samples": [_label_template(sample) for sample in samples]}, "manifestHash")
    _write_new_json(operator_dir / "calibration-labels.json", labels)
    return {"outputDir": str(output_dir), "blindDeliveryDir": str(blind_dir), "operatorOnlyDir": str(operator_dir), "holdoutCohort": COHORT_IDS[cohort], "counts": dict(Counter(sample["kind"] for sample in samples)), "renderedMutantsCreated": 0}


def validate_label_schema_shape(labels: Any) -> list[str]:
    """Dependency-free mirror of calibration-labels.schema.json shape rules."""
    errors: list[str] = []
    if not _exact_fields(labels, LABEL_TOP_LEVEL_FIELDS, "label manifest", errors):
        return errors
    if labels.get("schemaVersion") != LABEL_SCHEMA_VERSION:
        errors.append("schemaVersion must be calibration-labels/v2")
    cohort = labels.get("holdoutCohort")
    if not isinstance(cohort, str) or cohort not in COHORT_ID_VALUES:
        errors.append("holdoutCohort is invalid")
    status = labels.get("reviewStatus")
    if not isinstance(status, str) or status not in {TEMPLATE_NOT_REVIEWED, HUMAN_REVIEWED}:
        errors.append("reviewStatus is invalid")
    if not isinstance(labels.get("frozen"), bool):
        errors.append("frozen must be boolean")
    if not _is_hash(labels.get("manifestHash")):
        errors.append("manifestHash must be sha256:<64 lowercase hex>")
    samples = labels.get("samples")
    if not isinstance(samples, list) or not samples:
        return [*errors, "samples must be a non-empty array"]
    for index, sample in enumerate(samples):
        prefix = f"sample[{index}]"
        if not isinstance(sample, dict):
            errors.append(f"{prefix} must be an object")
            continue
        if not LABEL_SAMPLE_REQUIRED_FIELDS.issubset(sample) or not set(sample).issubset(LABEL_SAMPLE_FIELDS):
            errors.append(f"{prefix} fields do not match calibration-labels/v2")
            continue
        sample_id = sample.get("sampleId")
        name = sample_id if _is_nonempty_string(sample_id) else prefix
        if not _is_nonempty_string(sample_id):
            errors.append(f"{name}: sampleId is required")
        if not _is_nonempty_string(sample.get("requirementId")):
            errors.append(f"{name}: requirementId is required")
        per = sample.get("perRequirement")
        if not isinstance(per, list) or not per:
            errors.append(f"{name}: perRequirement must be non-empty")
        else:
            for item in per:
                if not isinstance(item, dict) or set(item) != {"requirementId", "state"}:
                    errors.append(f"{name}: invalid perRequirement item")
                    continue
                if not _is_nonempty_string(item.get("requirementId")):
                    errors.append(f"{name}: perRequirement requirementId is required")
                state = item.get("state")
                if status == TEMPLATE_NOT_REVIEWED and state is not None:
                    errors.append(f"{name}: template perRequirement state must be null")
                elif status == HUMAN_REVIEWED and not _is_state(state):
                    errors.append(f"{name}: invalid perRequirement state")
        if not isinstance(sample.get("isBoundary"), bool):
            errors.append(f"{name}: isBoundary must be boolean")
        if "adjudicationNotes" in sample and not isinstance(sample.get("adjudicationNotes"), str):
            errors.append(f"{name}: adjudicationNotes must be a string")
        if status == TEMPLATE_NOT_REVIEWED:
            if sample.get("labelSource") != "PENDING_HUMAN_ADJUDICATION" or any(sample.get(field) is not None for field in ("groundTruthRoute", "reviewer", "visuallyObservable", "eligibleForVisualMetrics", "evidenceHash", "verdictVersion")):
                errors.append(f"{name}: template sample contains an adjudicated field")
        elif status == HUMAN_REVIEWED:
            if not _is_route(sample.get("groundTruthRoute")):
                errors.append(f"{name}: invalid groundTruthRoute")
            if not _is_nonempty_string(sample.get("labelSource")) or sample.get("labelSource") == "PENDING_HUMAN_ADJUDICATION":
                errors.append(f"{name}: labelSource is required")
            if not _is_nonempty_string(sample.get("reviewer")):
                errors.append(f"{name}: reviewer is required")
            for field in ("visuallyObservable", "eligibleForVisualMetrics"):
                if not isinstance(sample.get(field), bool):
                    errors.append(f"{name}: {field} must be boolean")
            if not _is_hash(sample.get("evidenceHash")):
                errors.append(f"{name}: evidenceHash must be sha256:<64 lowercase hex>")
            if not _is_nonempty_string(sample.get("verdictVersion")):
                errors.append(f"{name}: verdictVersion is required")
    if status == TEMPLATE_NOT_REVIEWED:
        if labels.get("frozen") is not False or labels.get("reviewer") is not None or labels.get("frozenAt") is not None:
            errors.append("template header must remain unreviewed and unfrozen")
    elif status == HUMAN_REVIEWED:
        if labels.get("frozen") is not True:
            errors.append("human-reviewed manifest must be frozen")
        if not _is_nonempty_string(labels.get("reviewer")):
            errors.append("top-level reviewer is required")
        if not _valid_datetime(labels.get("frozenAt")):
            errors.append("frozenAt must be an RFC3339 timestamp with timezone")
    return errors


def _route_state_coherent(route: Any, per: Any, observable: Any, eligible: Any) -> bool:
    if not _is_route(route) or not isinstance(per, list) or not per:
        return False
    states = [item.get("state") for item in per if isinstance(item, dict)]
    if len(states) != len(per) or any(not _is_state(state) for state in states):
        return False
    if route == "VISUAL_PASS":
        return observable is True and eligible is True and all(state == "pass" for state in states)
    if route == "VISUAL_FAIL":
        return observable is True and eligible is True and any(state == "fail" for state in states)
    if route == "VISUAL_UNCERTAIN":
        return observable is True and eligible is True and any(state == "uncertain" for state in states) and all(state != "fail" for state in states)
    return eligible is False and all(state == "uncertain" for state in states)


def verify_labels(labels: Any) -> list[str]:
    """Validate final human labels, canonical hash, and semantic coherence."""
    errors = validate_label_schema_shape(labels)
    if not isinstance(labels, dict):
        return errors
    if labels.get("reviewStatus") != HUMAN_REVIEWED:
        errors.append("reviewStatus is not HUMAN_REVIEWED")
    if labels.get("frozen") is not True:
        errors.append("manifest is not frozen")
    if not _hash_matches(labels, "manifestHash"):
        errors.append("manifestHash does not match canonical content")
    samples = labels.get("samples")
    if not isinstance(samples, list) or not samples:
        return [*errors, "samples must be a non-empty array"]
    seen_ids: set[str] = set()
    seen_evidence: set[str] = set()
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict):
            errors.append(f"sample[{index}] must be an object")
            continue
        sample_id = sample.get("sampleId")
        name = sample_id if _is_nonempty_string(sample_id) else f"sample[{index}]"
        if not _is_nonempty_string(sample_id):
            errors.append(f"invalid sampleId: {sample_id!r}")
        elif sample_id in seen_ids:
            errors.append(f"invalid or duplicate sampleId: {sample_id!r}")
        else:
            seen_ids.add(sample_id)
        if not _is_nonempty_string(sample.get("requirementId")):
            errors.append(f"{name}: requirementId is required")
        per = sample.get("perRequirement")
        if not isinstance(per, list) or not per:
            errors.append(f"{name}: perRequirement must be non-empty")
        else:
            seen_requirements: set[str] = set()
            for item in per:
                if not isinstance(item, dict):
                    errors.append(f"{name}: perRequirement item must be an object")
                    continue
                requirement_id = item.get("requirementId")
                if not _is_nonempty_string(requirement_id):
                    errors.append(f"{name}: invalid perRequirement requirementId")
                elif requirement_id in seen_requirements:
                    errors.append(f"{name}: duplicate perRequirement requirementId")
                else:
                    seen_requirements.add(requirement_id)
                if requirement_id != sample.get("requirementId"):
                    errors.append(f"{name}: perRequirement requirementId must match sample requirementId")
                if not _is_state(item.get("state")):
                    errors.append(f"{name}: invalid perRequirement state")
        for field in ("visuallyObservable", "eligibleForVisualMetrics", "isBoundary"):
            if not isinstance(sample.get(field), bool):
                errors.append(f"{name}: {field} must be boolean")
        if sample.get("eligibleForVisualMetrics") is True and sample.get("visuallyObservable") is False:
            errors.append(f"{name}: invisible samples cannot be eligible for visual metrics")
        evidence_hash = sample.get("evidenceHash")
        if not _is_hash(evidence_hash):
            errors.append(f"{name}: evidenceHash must be sha256:<64 lowercase hex>")
        elif evidence_hash in seen_evidence:
            errors.append(f"{name}: evidenceHash must not be reused by another sample")
        else:
            seen_evidence.add(evidence_hash)
        if not _route_state_coherent(sample.get("groundTruthRoute"), per, sample.get("visuallyObservable"), sample.get("eligibleForVisualMetrics")):
            errors.append(f"{name}: groundTruthRoute, visibility, eligibility, and states are incoherent")
    return errors


def freeze_labels(labels: Any, reviewer: str, frozen_at: str) -> dict[str, Any]:
    if not isinstance(labels, dict):
        raise CalibrationValidationError("Cannot freeze labels: label manifest must be an object")
    frozen = copy.deepcopy(labels)
    frozen.update({"reviewStatus": HUMAN_REVIEWED, "frozen": True, "reviewer": reviewer, "frozenAt": frozen_at})
    frozen = _with_hash(frozen, "manifestHash")
    errors = verify_labels(frozen)
    if errors:
        raise CalibrationValidationError("Cannot freeze labels: " + "; ".join(errors))
    return frozen


def validate_operator_ledger(ledger: Any) -> list[str]:
    errors: list[str] = []
    if not _exact_fields(ledger, LEDGER_FIELDS, "operator ledger", errors):
        return errors
    if ledger.get("schemaVersion") != GENERATOR_SCHEMA_VERSION:
        errors.append("operator ledger schemaVersion is invalid")
    if not isinstance(ledger.get("holdoutCohort"), str) or ledger.get("holdoutCohort") not in COHORT_ID_VALUES:
        errors.append("operator ledger holdoutCohort is invalid")
    if not _is_one_of(ledger.get("holdoutMode"), {"standalone", "expanded", "fresh"}):
        errors.append("operator ledger holdoutMode is invalid")
    if not _is_nonempty_string(ledger.get("purpose")) or "operator-only" not in ledger.get("purpose", ""):
        errors.append("operator ledger purpose is invalid")
    if not isinstance(ledger.get("sampleIdSalt"), str) or len(ledger["sampleIdSalt"]) < 16:
        errors.append("operator ledger sampleIdSalt is invalid")
    if not _hash_matches(ledger, "ledgerHash"):
        errors.append("operator ledger hash does not match canonical content")
    samples = ledger.get("samples")
    if not isinstance(samples, list) or not samples:
        return [*errors, "operator ledger samples must be non-empty"]
    seen: set[str] = set()
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict) or set(sample) != {"sampleId", "sourceNamespace", "fixedSeed", "kind", "injection", "labelBlueprint"}:
            errors.append(f"operator ledger sample[{index}] fields are invalid")
            continue
        sample_id = sample.get("sampleId")
        if not isinstance(sample_id, str) or not SAMPLE_ID_PATTERN.fullmatch(sample_id):
            errors.append(f"operator ledger sample[{index}] has invalid sampleId")
        elif sample_id in seen:
            errors.append(f"operator ledger has duplicate sampleId {sample_id}")
        else:
            seen.add(sample_id)
        if not _is_nonempty_string(sample.get("sourceNamespace")) or not _is_nonnegative_count(sample.get("fixedSeed")):
            errors.append(f"operator ledger sample[{index}] source/seed is invalid")
        if not _is_one_of(sample.get("kind"), {"fail", "pass", "uncertain", "invalid"}):
            errors.append(f"operator ledger sample[{index}] kind is invalid")
        injection, blueprint = sample.get("injection"), sample.get("labelBlueprint")
        if not isinstance(injection, dict) or set(injection) != {"errorClass", "strength", "mutationCommands"}:
            errors.append(f"operator ledger sample[{index}] injection is invalid")
        if not isinstance(blueprint, dict) or set(blueprint) != {"groundTruthRoute", "perRequirement", "requirementId", "visuallyObservable", "eligibleForVisualMetrics", "isBoundary"}:
            errors.append(f"operator ledger sample[{index}] labelBlueprint is invalid")
    cohort_name = next((name for name, cohort_id in COHORT_IDS.items() if cohort_id == ledger.get("holdoutCohort")), None)
    if cohort_name and _ledger_counts(ledger) != COHORTS[cohort_name]:
        errors.append("operator ledger counts do not match its holdout cohort")
    source_names = {sample.get("sourceNamespace") for sample in samples if isinstance(sample, dict) and isinstance(sample.get("sourceNamespace"), str)}
    if cohort_name == "full":
        expected_sources = {"fresh-full-v1"} if ledger.get("holdoutMode") == "fresh" else {"reduced", "extension"}
        if source_names != expected_sources:
            errors.append("operator ledger source namespace does not match full holdout mode")
    return errors


def validate_review_context(context: Any, require_frozen: bool = False) -> list[str]:
    errors: list[str] = []
    if not _exact_fields(context, CONTEXT_FIELDS, "review freeze context", errors):
        return errors
    if context.get("schemaVersion") != CONTEXT_SCHEMA_VERSION:
        errors.append("review freeze context schemaVersion is invalid")
    if not isinstance(context.get("holdoutCohort"), str) or context.get("holdoutCohort") not in COHORT_ID_VALUES:
        errors.append("review freeze context holdoutCohort is invalid")
    status, mode = context.get("freezeStatus"), context.get("holdoutIsolationMode")
    if not _is_one_of(status, {TEMPLATE_NOT_FROZEN, FROZEN}):
        errors.append("review freeze context freezeStatus is invalid")
    if not _is_one_of(mode, {"INITIAL_REDUCED", "EXTENSION_ONLY", "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS", "FRESH_INDEPENDENT_FULL"}):
        errors.append("review freeze context holdoutIsolationMode is invalid")
    if not _is_hash(context.get("operatorLedgerHash")) or not _hash_matches(context, "contextHash"):
        errors.append("review freeze context hash binding is invalid")
    if status == TEMPLATE_NOT_FROZEN:
        if any(context.get(field) is not None for field in (*FREEZE_CONTEXT_HASH_FIELDS, "reducedMetricsHash")):
            errors.append("template review context contains frozen values")
    elif status == FROZEN:
        for field in FREEZE_CONTEXT_HASH_FIELDS:
            if not _is_hash(context.get(field)):
                errors.append(f"review freeze context {field} is invalid")
        if mode == "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS" and not _is_hash(context.get("reducedMetricsHash")):
            errors.append("expanded full context requires reducedMetricsHash")
        if mode != "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS" and context.get("reducedMetricsHash") is not None:
            errors.append("fresh/initial context must not claim a reducedMetricsHash")
    if require_frozen and status != FROZEN:
        errors.append("review freeze context is not frozen")
    return errors


def validate_blind_manifest(manifest: Any, require_frozen: bool = False) -> list[str]:
    errors: list[str] = []
    if not _exact_fields(manifest, BLIND_FIELDS, "blind manifest", errors):
        return errors
    if manifest.get("schemaVersion") != BLIND_SCHEMA_VERSION:
        errors.append("blind manifest schemaVersion is invalid")
    if not isinstance(manifest.get("holdoutCohort"), str) or manifest.get("holdoutCohort") not in COHORT_ID_VALUES:
        errors.append("blind manifest holdoutCohort is invalid")
    status = manifest.get("freezeStatus")
    if not _is_one_of(status, {TEMPLATE_NOT_FROZEN, FROZEN}):
        errors.append("blind manifest freezeStatus is invalid")
    if not _hash_matches(manifest, "manifestHash"):
        errors.append("blind manifest hash binding is invalid")
    if status == TEMPLATE_NOT_FROZEN:
        if any(manifest.get(field) is not None for field in ("contractHash", "captureProfileHash", "reviewContextHash")):
            errors.append("template blind manifest contains frozen values")
    elif status == FROZEN:
        for field in ("contractHash", "captureProfileHash", "reviewContextHash"):
            if not _is_hash(manifest.get(field)):
                errors.append(f"blind manifest {field} is invalid")
    samples = manifest.get("samples")
    if not isinstance(samples, list) or not samples:
        return [*errors, "blind manifest samples must be non-empty"]
    seen: set[str] = set()
    for index, entry in enumerate(samples):
        if not isinstance(entry, dict) or set(entry) != {"sampleId", "evidence", "contractHash", "captureProfileHash"}:
            errors.append(f"blind manifest sample[{index}] fields are invalid")
            continue
        sample_id = entry.get("sampleId")
        if not isinstance(sample_id, str) or not SAMPLE_ID_PATTERN.fullmatch(sample_id):
            errors.append(f"blind manifest sample[{index}] has invalid sampleId")
        elif sample_id in seen:
            errors.append(f"blind manifest has duplicate sampleId {sample_id}")
        else:
            seen.add(sample_id)
        evidence = entry.get("evidence")
        expected_path = f"evidence/{sample_id}.evidence.json" if isinstance(sample_id, str) else None
        if not isinstance(evidence, dict) or set(evidence) != {"evidenceManifest", "evidenceIdentity"} or evidence.get("evidenceIdentity") != sample_id or evidence.get("evidenceManifest") != expected_path:
            errors.append(f"blind manifest sample[{index}] evidence identity is invalid")
        if entry.get("contractHash") != manifest.get("contractHash") or entry.get("captureProfileHash") != manifest.get("captureProfileHash"):
            errors.append(f"blind manifest sample[{index}] identity hashes do not match header")
    if require_frozen and status != FROZEN:
        errors.append("blind manifest is not frozen")
    return errors


def validate_evidence_bundle(bundle: Any, require_frozen: bool = False) -> list[str]:
    errors: list[str] = []
    if not _exact_fields(bundle, BUNDLE_FIELDS, "evidence bundle", errors):
        return errors
    if bundle.get("schemaVersion") != BUNDLE_SCHEMA_VERSION:
        errors.append("evidence bundle schemaVersion is invalid")
    if not isinstance(bundle.get("holdoutCohort"), str) or bundle.get("holdoutCohort") not in COHORT_ID_VALUES:
        errors.append("evidence bundle holdoutCohort is invalid")
    status = bundle.get("freezeStatus")
    if not _is_one_of(status, {TEMPLATE_NOT_FROZEN, FROZEN}):
        errors.append("evidence bundle freezeStatus is invalid")
    if not _is_hash(bundle.get("operatorLedgerHash")) or not _hash_matches(bundle, "bundleHash"):
        errors.append("evidence bundle hash binding is invalid")
    if status == TEMPLATE_NOT_FROZEN:
        if bundle.get("blindManifestHash") is not None or bundle.get("captureProfileHash") is not None:
            errors.append("template evidence bundle contains frozen values")
    elif status == FROZEN:
        for field in ("blindManifestHash", "captureProfileHash"):
            if not _is_hash(bundle.get(field)):
                errors.append(f"evidence bundle {field} is invalid")
    samples = bundle.get("samples")
    if not isinstance(samples, list) or not samples:
        return [*errors, "evidence bundle samples must be non-empty"]
    seen_ids: set[str] = set()
    seen_hashes: set[str] = set()
    for index, entry in enumerate(samples):
        if not isinstance(entry, dict) or set(entry) != {"sampleId", "evidenceHash"}:
            errors.append(f"evidence bundle sample[{index}] fields are invalid")
            continue
        sample_id, evidence_hash = entry.get("sampleId"), entry.get("evidenceHash")
        if not isinstance(sample_id, str) or not SAMPLE_ID_PATTERN.fullmatch(sample_id):
            errors.append(f"evidence bundle sample[{index}] has invalid sampleId")
        elif sample_id in seen_ids:
            errors.append(f"evidence bundle has duplicate sampleId {sample_id}")
        else:
            seen_ids.add(sample_id)
        if status == TEMPLATE_NOT_FROZEN:
            if evidence_hash is not None:
                errors.append(f"evidence bundle sample[{index}] template evidenceHash must be null")
        elif not _is_hash(evidence_hash):
            errors.append(f"evidence bundle sample[{index}] evidenceHash is invalid")
        elif evidence_hash in seen_hashes:
            errors.append(f"evidence bundle reuses evidenceHash {evidence_hash}")
        else:
            seen_hashes.add(evidence_hash)
    if require_frozen and status != FROZEN:
        errors.append("evidence bundle is not frozen")
    return errors


def _sample_id_set(document: Any) -> set[str]:
    return {item["sampleId"] for item in document.get("samples", []) if isinstance(item, dict) and isinstance(item.get("sampleId"), str)} if isinstance(document, dict) else set()


def _bundle_hashes(bundle: dict[str, Any]) -> dict[str, str]:
    return {entry["sampleId"]: entry["evidenceHash"] for entry in bundle["samples"]}


def _ledger_counts(ledger: dict[str, Any]) -> dict[str, int]:
    counts: Counter[str] = Counter({"fail": 0, "pass": 0, "uncertain": 0, "invalid": 0})
    for sample in ledger["samples"]:
        kind = sample.get("kind") if isinstance(sample, dict) else None
        if isinstance(kind, str) and kind in counts:
            counts[kind] += 1
    return dict(counts)


def validate_frozen_bindings(labels: Any, blind_manifest: Any, evidence_bundle: Any, review_context: Any, operator_ledger: Any, reduced_metrics: Any | None = None) -> list[str]:
    """Bind labels to frozen evidence/contract/context/operator identities."""
    errors: list[str] = []
    errors.extend(verify_labels(labels))
    errors.extend(validate_blind_manifest(blind_manifest, True))
    errors.extend(validate_evidence_bundle(evidence_bundle, True))
    errors.extend(validate_review_context(review_context, True))
    errors.extend(validate_operator_ledger(operator_ledger))
    # The validators above deliberately accept arbitrary parsed JSON and
    # return errors. Do not dereference malformed nested fields afterward.
    if errors:
        return errors
    if not all(isinstance(item, dict) for item in (labels, blind_manifest, evidence_bundle, review_context, operator_ledger)):
        return errors
    cohort = labels.get("holdoutCohort")
    if any(item.get("holdoutCohort") != cohort for item in (blind_manifest, evidence_bundle, review_context, operator_ledger)):
        errors.append("frozen artifacts do not share one holdoutCohort")
    ledger_hash = operator_ledger.get("ledgerHash")
    if any(item.get("operatorLedgerHash") != ledger_hash for item in (evidence_bundle, review_context)):
        errors.append("frozen artifacts do not bind the supplied operator ledger")
    if cohort == COHORT_IDS["full"]:
        expected_mode = "FRESH_INDEPENDENT_FULL" if operator_ledger.get("holdoutMode") == "fresh" else "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS"
        if review_context.get("holdoutIsolationMode") != expected_mode:
            errors.append("review context holdoutIsolationMode does not match operator ledger mode")
    elif cohort == COHORT_IDS["reduced"] and review_context.get("holdoutIsolationMode") != "INITIAL_REDUCED":
        errors.append("reduced review context must use INITIAL_REDUCED isolation mode")
    if blind_manifest.get("contractHash") != review_context.get("contractHash") or blind_manifest.get("captureProfileHash") != review_context.get("captureProfileHash") or blind_manifest.get("reviewContextHash") != review_context.get("contextHash"):
        errors.append("blind manifest does not bind frozen contract/capture/review context")
    if evidence_bundle.get("blindManifestHash") != blind_manifest.get("manifestHash") or evidence_bundle.get("captureProfileHash") != review_context.get("captureProfileHash"):
        errors.append("evidence bundle does not bind blind manifest/capture identity")
    sets = [_sample_id_set(item) for item in (labels, blind_manifest, evidence_bundle, operator_ledger)]
    if not sets[0] or any(sample_ids != sets[0] for sample_ids in sets[1:]):
        errors.append("labels, blind manifest, evidence bundle, and ledger must contain exactly the same sampleIds")
    if isinstance(labels.get("samples"), list) and isinstance(evidence_bundle.get("samples"), list):
        bundle_hashes = _bundle_hashes(evidence_bundle)
        for sample in labels["samples"]:
            if isinstance(sample, dict) and sample.get("evidenceHash") != bundle_hashes.get(sample.get("sampleId")):
                errors.append(f"{sample.get('sampleId')!r}: label evidenceHash does not match frozen evidence bundle")
    mode = review_context.get("holdoutIsolationMode")
    if cohort == COHORT_IDS["full"] and mode == "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS":
        if not isinstance(reduced_metrics, dict):
            errors.append("expanded full holdout requires its frozen reduced metrics input")
        else:
            errors.extend(verify_metrics(reduced_metrics))
            if reduced_metrics.get("reportHash") != review_context.get("reducedMetricsHash"):
                errors.append("expanded full context does not bind supplied reduced metrics")
            reduced_per = reduced_metrics.get("perRequirementMetrics")
            if reduced_metrics.get("holdoutCohort") != COHORT_IDS["reduced"] or not isinstance(reduced_per, dict) or reduced_per.get("falsePassCount") != 0:
                errors.append("expanded full holdout requires reduced falsePassCount=0")
    elif reduced_metrics is not None:
        errors.append("reduced metrics may only accompany an expanded full holdout")
    return errors


def _expected_requirement_states(label: dict[str, Any]) -> dict[str, str]:
    return {item["requirementId"]: item["state"] for item in label["perRequirement"]}


def _validate_vfx_report(report: Any, label: dict[str, Any], context: dict[str, Any]) -> list[str]:
    """Mirror vfx-visual-review.schema.json and bind report to frozen evidence."""
    errors: list[str] = []
    if not isinstance(report, dict):
        errors.append("VFX review report must be an object")
        return errors
    if not VFX_REPORT_REQUIRED_FIELDS.issubset(report) or not set(report).issubset(VFX_REPORT_FIELDS):
        errors.append("VFX review report fields are invalid")
        return errors
    if not _is_nonempty_string(report.get("reviewVersion")):
        errors.append("VFX review report reviewVersion is required")
    if not _is_one_of(report.get("candidateId"), {"C0", "C1", "C2"}):
        errors.append("VFX review report candidateId is invalid")
    for field in ("contractHash", "buildHash", "captureProfileHash", "sealedReportHash"):
        if not _is_hash(report.get(field)):
            errors.append(f"VFX review report {field} is invalid")
    if not _hash_matches(report, "sealedReportHash"):
        errors.append("VFX review report sealedReportHash does not match canonical content")
    if any(report.get(field) != context.get(field) for field in ("contractHash", "buildHash", "captureProfileHash")):
        errors.append("VFX review report does not bind frozen contract/build/capture identity")
    evidence_hashes = report.get("evidenceHashes")
    if not isinstance(evidence_hashes, list) or not evidence_hashes or any(not _is_hash(value) for value in evidence_hashes) or len(set(evidence_hashes)) != len(evidence_hashes):
        errors.append("VFX review report evidenceHashes are invalid")
    elif label.get("evidenceHash") not in evidence_hashes:
        errors.append("VFX review report does not include sample frozen evidenceHash")
    if not isinstance(report.get("imageInputRead"), bool) or not _is_one_of(report.get("evidenceStatus"), {"valid", "invalid"}) or not _is_one_of(report.get("contractStatus"), {"unambiguous", "ambiguous"}):
        errors.append("VFX review report status fields are invalid")
    if report.get("s0aTerminalStatus") is not None and not _is_one_of(report.get("s0aTerminalStatus"), {"S0A_ADVISORY_ONLY"}) or report.get("qaGateAuthority") != "advisory-only":
        errors.append("S0a calibration reports must remain advisory-only before scoring")
    route = report.get("topLevelRoute")
    if not _is_route(route):
        errors.append("VFX review report topLevelRoute is invalid")
    if "conflicts" in report and (not isinstance(report["conflicts"], list) or any(not _is_nonempty_string(value) for value in report["conflicts"])):
        errors.append("VFX review report conflicts are invalid")
    expected = _expected_requirement_states(label)
    requirements = report.get("perRequirement")
    if not isinstance(requirements, list) or not requirements:
        return [*errors, "VFX review report perRequirement must be non-empty"]
    seen: set[str] = set()
    for item in requirements:
        if not _exact_fields(item, VFX_REQUIREMENT_FIELDS, "VFX requirement review", errors):
            continue
        requirement_id = item.get("designRequirementId")
        if not _is_nonempty_string(requirement_id) or requirement_id not in expected:
            errors.append("VFX requirement review has unknown designRequirementId")
        elif requirement_id in seen:
            errors.append("VFX requirement review repeats designRequirementId")
        else:
            seen.add(requirement_id)
        if not _is_state(item.get("state")) or not isinstance(item.get("countable"), bool) or not _is_one_of(item.get("reasonCode"), REASON_CODES):
            errors.append("VFX requirement review state/countable/reasonCode is invalid")
        if not _is_nonempty_string(item.get("stateRef")) or not _is_nonempty_string(item.get("imageRegion")) or not _is_nonempty_string(item.get("observation")):
            errors.append("VFX requirement review observation fields are invalid")
        frames = item.get("frameNumbers")
        if not isinstance(frames, list) or not frames or any(not _is_nonnegative_count(frame) for frame in frames):
            errors.append("VFX requirement review frameNumbers are invalid")
    if seen != set(expected):
        errors.append("VFX review report has missing or extra requirements")
    items = [item for item in requirements if isinstance(item, dict)]
    if route == "VISUAL_PASS":
        if not (report.get("imageInputRead") is True and report.get("evidenceStatus") == "valid" and report.get("contractStatus") == "unambiguous") or any(item.get("state") != "pass" or item.get("countable") is not True or item.get("reasonCode") != "OBSERVED" for item in items):
            errors.append("VISUAL_PASS report states are invalid")
    elif route == "VISUAL_FAIL":
        if not (report.get("imageInputRead") is True and report.get("evidenceStatus") == "valid" and report.get("contractStatus") == "unambiguous") or not any(item.get("state") == "fail" and item.get("countable") is True and item.get("reasonCode") == "OBSERVED" for item in items):
            errors.append("VISUAL_FAIL report needs an observed countable fail")
    elif route == "VISUAL_UNCERTAIN":
        if not (report.get("imageInputRead") is True and report.get("evidenceStatus") == "valid" and report.get("contractStatus") == "unambiguous") or not any(item.get("state") == "uncertain" and item.get("countable") is True and item.get("reasonCode") == "VISUAL_UNCERTAIN" for item in items) or any(item.get("state") == "fail" for item in items):
            errors.append("VISUAL_UNCERTAIN report states are invalid")
    elif route == "EVIDENCE_INVALID":
        if report.get("evidenceStatus") != "invalid" or any(item.get("state") != "uncertain" or item.get("countable") is not False or item.get("reasonCode") != "EVIDENCE_INVALID" for item in items):
            errors.append("EVIDENCE_INVALID report states are invalid")
    elif route == "CONTRACT_AMBIGUOUS":
        if report.get("evidenceStatus") != "valid" or report.get("contractStatus") != "ambiguous" or any(item.get("state") != "uncertain" or item.get("countable") is not False or item.get("reasonCode") != "CONTRACT_AMBIGUOUS" for item in items):
            errors.append("CONTRACT_AMBIGUOUS report states are invalid")
    return errors


def validate_reviews(labels_manifest: Any, reviews_manifest: Any, review_context: Any | None = None) -> list[str]:
    """Require exactly three complete, independently identified sealed sessions."""
    errors: list[str] = []
    if not isinstance(labels_manifest, dict) or not isinstance(reviews_manifest, dict) or not isinstance(review_context, dict):
        return ["labels, review corpus, and review context must be objects"]
    if not _exact_fields(reviews_manifest, REVIEW_CORPUS_FIELDS, "review corpus", errors):
        return errors
    if reviews_manifest.get("schemaVersion") != REVIEW_CORPUS_SCHEMA_VERSION or not _hash_matches(reviews_manifest, "corpusHash"):
        errors.append("review corpus schema/hash is invalid")
    if reviews_manifest.get("reviewContextHash") != review_context.get("contextHash"):
        errors.append("review corpus does not bind review context")
    labels = labels_manifest.get("samples")
    if not isinstance(labels, list):
        return [*errors, "labels samples must be an array"]
    labels_by_id = {sample.get("sampleId"): sample for sample in labels if isinstance(sample, dict) and isinstance(sample.get("sampleId"), str)}
    expected_ids = set(labels_by_id)
    sessions = reviews_manifest.get("sessions")
    if not isinstance(sessions, list) or len(sessions) != 3:
        return [*errors, "reviews must contain exactly three isolated sessions"]
    seen_sessions: set[str] = set()
    seen_reviewer_sessions: set[str] = set()
    for index, session in enumerate(sessions):
        prefix = f"session[{index}]"
        if not _exact_fields(session, REVIEW_SESSION_FIELDS, prefix, errors):
            continue
        session_id, reviewer_session = session.get("sessionId"), session.get("reviewerSessionId")
        if not _is_nonempty_string(session_id):
            errors.append(f"{prefix}: sessionId is required")
        elif session_id in seen_sessions:
            errors.append(f"duplicate sessionId: {session_id}")
        else:
            seen_sessions.add(session_id)
        if not _is_nonempty_string(reviewer_session):
            errors.append(f"{prefix}: reviewerSessionId is required")
        elif reviewer_session in seen_reviewer_sessions:
            errors.append(f"duplicate reviewerSessionId: {reviewer_session}")
        else:
            seen_reviewer_sessions.add(reviewer_session)
        if session.get("isolated") is not True:
            errors.append(f"{prefix}: session must explicitly attest isolated=true")
        if not _valid_datetime(session.get("startedAt")) or not _valid_datetime(session.get("completedAt")):
            errors.append(f"{prefix}: session timestamps must be RFC3339 with timezone")
        if not _hash_matches(session, "sessionHash"):
            errors.append(f"{prefix}: sessionHash does not match canonical content")
        records = session.get("reviews")
        if not isinstance(records, list):
            errors.append(f"{prefix}: reviews must be an array")
            continue
        seen_samples: set[str] = set()
        for record in records:
            if not isinstance(record, dict) or set(record) != {"sampleId", "report"}:
                errors.append(f"{prefix}: review record fields are invalid")
                continue
            sample_id = record.get("sampleId")
            if not isinstance(sample_id, str):
                errors.append(f"{prefix}: sampleId must be a string")
                continue
            if sample_id in seen_samples:
                errors.append(f"duplicate (sampleId, sessionId): {(sample_id, session_id)!r}")
                continue
            seen_samples.add(sample_id)
            label = labels_by_id.get(sample_id)
            if label is None:
                errors.append(f"{prefix}: unknown sampleId {sample_id!r}")
                continue
            errors.extend(f"{prefix}/{sample_id}: {message}" for message in _validate_vfx_report(record.get("report"), label, review_context))
        if seen_samples != expected_ids:
            errors.append(f"{prefix}: missing or extra samples")
    return errors


def _count_cohort(labels: list[dict[str, Any]]) -> tuple[dict[str, int], list[str]]:
    counts: Counter[str] = Counter({"fail": 0, "pass": 0, "uncertain": 0, "invalid": 0})
    uncounted: list[str] = []
    for sample in labels:
        route, visible, eligible = sample["groundTruthRoute"], sample["visuallyObservable"], sample["eligibleForVisualMetrics"]
        if route == "VISUAL_FAIL" and visible and eligible:
            counts["fail"] += 1
        elif route == "VISUAL_PASS" and visible and eligible:
            counts["pass"] += 1
        elif route == "VISUAL_UNCERTAIN" and visible and eligible:
            counts["uncertain"] += 1
        elif route == "EVIDENCE_INVALID" and not eligible:
            counts["invalid"] += 1
        else:
            uncounted.append(sample["sampleId"])
    return dict(counts), uncounted


def _flatten_reviews(corpus: dict[str, Any]) -> list[dict[str, Any]]:
    return [{"sampleId": record["sampleId"], "sessionId": session["sessionId"], "report": copy.deepcopy(record["report"])} for session in corpus["sessions"] for record in session["reviews"]]


def _agreement_groups(flat: list[dict[str, Any]], labels: list[dict[str, Any]]) -> tuple[float, float]:
    sample_ids = {sample["sampleId"] for sample in labels}
    route_groups: dict[str, list[str]] = defaultdict(list)
    requirement_groups: dict[tuple[str, str], list[str]] = defaultdict(list)
    for review in flat:
        report = review["report"]
        route_groups[review["sampleId"]].append(report["topLevelRoute"])
        for result in report["perRequirement"]:
            requirement_groups[(review["sampleId"], result["designRequirementId"])].append(result["state"])
    routes = sum(len(route_groups[sample_id]) == 3 and len(set(route_groups[sample_id])) == 1 for sample_id in sample_ids)
    requirements = [(sample["sampleId"], item["requirementId"]) for sample in labels for item in sample["perRequirement"]]
    states = sum(len(requirement_groups[key]) == 3 and len(set(requirement_groups[key])) == 1 for key in requirements)
    return states / len(requirements), routes / len(sample_ids)


def calculate_metrics(labels_manifest: Any, reviews_manifest: Any, blind_manifest: Any, evidence_bundle: Any, review_context: Any, operator_ledger: Any, reduced_metrics: Any | None = None) -> dict[str, Any]:
    """Score a fully frozen corpus; incomplete inputs cannot claim a gate."""
    binding_errors = validate_frozen_bindings(labels_manifest, blind_manifest, evidence_bundle, review_context, operator_ledger, reduced_metrics)
    if binding_errors:
        raise CalibrationValidationError("Invalid frozen S0a bindings: " + "; ".join(binding_errors))
    if not isinstance(reviews_manifest, dict) or reviews_manifest.get("blindManifestHash") != blind_manifest["manifestHash"] or reviews_manifest.get("evidenceBundleHash") != evidence_bundle["bundleHash"]:
        raise CalibrationValidationError("Invalid review corpus: review corpus does not bind frozen blind manifest/evidence bundle")
    review_errors = validate_reviews(labels_manifest, reviews_manifest, review_context)
    if review_errors:
        raise CalibrationValidationError("Invalid review corpus: " + "; ".join(review_errors))
    labels = labels_manifest["samples"]
    if labels_manifest["holdoutCohort"] not in {COHORT_IDS["reduced"], COHORT_IDS["full"]}:
        raise CalibrationValidationError("Only reduced or full holdouts may be scored")
    flat = _flatten_reviews(reviews_manifest)
    labels_by_id = {sample["sampleId"]: sample for sample in labels}
    by_type: dict[str, dict[str, Counter[str]]] = defaultdict(lambda: defaultdict(Counter))
    top: dict[str, Counter[str]] = {route: Counter() for route in sorted(REVIEW_ROUTES)}
    known_fail = false_pass = known_pass = false_fail = non_boundary = non_boundary_uncertain = expected_invalid = detected_invalid = 0
    for review in flat:
        label, report = labels_by_id[review["sampleId"]], review["report"]
        expected_route, predicted_route = label["groundTruthRoute"], report["topLevelRoute"]
        top[expected_route][predicted_route] += 1
        if expected_route == "EVIDENCE_INVALID":
            expected_invalid += 1
            detected_invalid += int(predicted_route == "EVIDENCE_INVALID")
        kind = "visual" if label["eligibleForVisualMetrics"] else "behavioral_or_structural"
        expected_states = _expected_requirement_states(label)
        for result in report["perRequirement"]:
            expected_state, predicted_state = expected_states[result["designRequirementId"]], result["state"]
            by_type[kind][result["designRequirementId"]][f"{expected_state}->{predicted_state}"] += 1
            if not label["eligibleForVisualMetrics"]:
                continue
            if expected_state == "fail":
                known_fail += 1
                false_pass += int(predicted_state == "pass")
            if expected_state == "pass":
                known_pass += 1
                false_fail += int(predicted_state == "fail")
            if not label["isBoundary"]:
                non_boundary += 1
                non_boundary_uncertain += int(predicted_state == "uncertain")
    false_fail_rate = false_fail / known_pass if known_pass else 0.0
    uncertain_rate = non_boundary_uncertain / non_boundary if non_boundary else 0.0
    invalid_recall = detected_invalid / expected_invalid if expected_invalid else 0.0
    requirement_agreement, route_agreement = _agreement_groups(flat, labels)
    counts, uncounted = _count_cohort(labels)
    ledger_counts = _ledger_counts(operator_ledger)
    exact_full = labels_manifest["holdoutCohort"] == COHORT_IDS["full"] and len(labels) == 110 and counts == COHORTS["full"] and ledger_counts == COHORTS["full"] and not uncounted and all(sample["groundTruthRoute"] != "CONTRACT_AMBIGUOUS" for sample in labels)
    thresholds = false_pass == 0 and false_fail_rate <= 0.10 and uncertain_rate <= 0.15 and invalid_recall == 1.0 and requirement_agreement >= 0.90 and route_agreement >= 0.90
    report = {
        "holdoutCohort": labels_manifest["holdoutCohort"], "labelManifestHash": labels_manifest["manifestHash"],
        "perRequirementMetrics": {"falsePassCount": false_pass, "knownVisualFailCount": known_fail, "falseFailRate": false_fail_rate, "nonBoundaryUncertainRate": uncertain_rate, "confusionMatricesByRequirementType": {kind: {requirement: dict(matrix) for requirement, matrix in requirements.items()} for kind, requirements in by_type.items()}, "qualificationPreconditions": {"exactFullHoldout": exact_full, "labelCount": len(labels), "cohortCounts": counts, "uncountedSamples": uncounted}},
        "topRouteMetrics": {"evidenceInvalidRecall": invalid_recall, "evidenceInvalidDetected": detected_invalid, "evidenceInvalidExpected": expected_invalid, "confusionMatrix": {route: dict(top[route]) for route in sorted(REVIEW_ROUTES)}},
        "stability": {"isolatedSessionCount": 3, "perRequirementAgreement": requirement_agreement, "topRouteAgreement": route_agreement},
        "terminalStatus": "S0A_GATE_QUALIFIED" if exact_full and thresholds else "S0A_ADVISORY_ONLY",
    }
    sealed = _with_hash(report, "reportHash")
    errors = verify_metrics(sealed, labels_manifest)
    if errors:
        raise CalibrationValidationError("Internal metric report validation failed: " + "; ".join(errors))
    return sealed


def _matrix_is_valid(matrix: Any) -> bool:
    return isinstance(matrix, dict) and all(isinstance(key, str) and _is_nonnegative_count(value) for key, value in matrix.items())


def validate_metrics_schema_shape(report: Any) -> list[str]:
    errors: list[str] = []
    if not _exact_fields(report, METRICS_TOP_LEVEL_FIELDS, "metrics report", errors):
        return errors
    if not _is_one_of(report.get("holdoutCohort"), {COHORT_IDS["reduced"], COHORT_IDS["full"]}):
        errors.append("metrics holdoutCohort is invalid")
    if not _is_hash(report.get("labelManifestHash")) or not _is_hash(report.get("reportHash")):
        errors.append("metrics hashes must be sha256:<64 lowercase hex>")
    per = report.get("perRequirementMetrics")
    if not _exact_fields(per, PER_REQUIREMENT_METRIC_FIELDS, "perRequirementMetrics", errors):
        per = None
    if isinstance(per, dict):
        for field in ("falsePassCount", "knownVisualFailCount"):
            if not _is_nonnegative_count(per.get(field)):
                errors.append(f"{field} must be a non-negative integer")
        for field in ("falseFailRate", "nonBoundaryUncertainRate"):
            if not _is_rate(per.get(field)):
                errors.append(f"{field} must be a rate")
        matrices = per.get("confusionMatricesByRequirementType")
        if not isinstance(matrices, dict):
            errors.append("confusionMatricesByRequirementType must be an object")
        else:
            for kind, requirements in matrices.items():
                if not isinstance(kind, str) or not isinstance(requirements, dict):
                    errors.append("confusionMatricesByRequirementType nesting is invalid")
                    continue
                for requirement, matrix in requirements.items():
                    if not isinstance(requirement, str) or not _matrix_is_valid(matrix):
                        errors.append("per-requirement confusion matrix is invalid")
        pre = per.get("qualificationPreconditions")
        if not isinstance(pre, dict) or set(pre) != {"exactFullHoldout", "labelCount", "cohortCounts", "uncountedSamples"}:
            errors.append("qualificationPreconditions fields are invalid")
        else:
            if not isinstance(pre.get("exactFullHoldout"), bool) or not _is_nonnegative_count(pre.get("labelCount")):
                errors.append("qualificationPreconditions types are invalid")
            counts = pre.get("cohortCounts")
            if not isinstance(counts, dict) or set(counts) != set(COHORTS["full"]) or not all(_is_nonnegative_count(value) for value in counts.values()):
                errors.append("cohortCounts values are invalid")
            uncounted = pre.get("uncountedSamples")
            if not isinstance(uncounted, list) or any(not _is_nonempty_string(value) for value in uncounted) or len(set(uncounted)) != len(uncounted):
                errors.append("uncountedSamples must be a unique non-empty-string array")
    top = report.get("topRouteMetrics")
    if not _exact_fields(top, TOP_ROUTE_METRIC_FIELDS, "topRouteMetrics", errors):
        top = None
    if isinstance(top, dict):
        if not _is_rate(top.get("evidenceInvalidRecall")):
            errors.append("evidenceInvalidRecall must be a rate")
        for field in ("evidenceInvalidDetected", "evidenceInvalidExpected"):
            if not _is_nonnegative_count(top.get(field)):
                errors.append(f"{field} must be a non-negative integer")
        matrix = top.get("confusionMatrix")
        if not isinstance(matrix, dict) or set(matrix) != REVIEW_ROUTES or any(not _matrix_is_valid(row) for row in matrix.values()):
            errors.append("top-level confusion matrix is invalid")
    stability = report.get("stability")
    if not _exact_fields(stability, STABILITY_FIELDS, "stability", errors):
        stability = None
    if isinstance(stability, dict) and (stability.get("isolatedSessionCount") != 3 or not _is_rate(stability.get("perRequirementAgreement")) or not _is_rate(stability.get("topRouteAgreement"))):
        errors.append("stability values are invalid")
    terminal = report.get("terminalStatus")
    if not _is_one_of(terminal, {"S0A_GATE_QUALIFIED", "S0A_ADVISORY_ONLY"}):
        errors.append("terminalStatus is invalid")
    elif terminal == "S0A_GATE_QUALIFIED" and isinstance(per, dict) and isinstance(top, dict) and isinstance(stability, dict):
        pre = per.get("qualificationPreconditions", {})
        if not (report.get("holdoutCohort") == COHORT_IDS["full"] and per.get("falsePassCount") == 0 and _is_rate(per.get("falseFailRate")) and per["falseFailRate"] <= .10 and _is_rate(per.get("nonBoundaryUncertainRate")) and per["nonBoundaryUncertainRate"] <= .15 and pre.get("exactFullHoldout") is True and pre.get("labelCount") == 110 and pre.get("cohortCounts") == COHORTS["full"] and pre.get("uncountedSamples") == [] and top.get("evidenceInvalidRecall") == 1 and stability.get("perRequirementAgreement", 0) >= .90 and stability.get("topRouteAgreement", 0) >= .90):
            errors.append("GATE_QUALIFIED report misses schema qualification constraints")
    return errors


def verify_metrics(report: Any, labels_manifest: Any | None = None) -> list[str]:
    errors = validate_metrics_schema_shape(report)
    if not isinstance(report, dict):
        return errors
    if not _hash_matches(report, "reportHash"):
        errors.append("reportHash does not match canonical content")
    if isinstance(labels_manifest, dict):
        label_errors = verify_labels(labels_manifest)
        if label_errors:
            errors.extend(f"labels: {error}" for error in label_errors)
        elif report.get("labelManifestHash") != labels_manifest.get("manifestHash"):
            errors.append("labelManifestHash does not match supplied labels")
    top = report.get("topRouteMetrics")
    if isinstance(top, dict) and isinstance(top.get("confusionMatrix"), dict):
        row = top["confusionMatrix"].get("EVIDENCE_INVALID", {})
        if _matrix_is_valid(row):
            expected, detected = sum(row.values()), row.get("EVIDENCE_INVALID", 0)
            if top.get("evidenceInvalidExpected") != expected or top.get("evidenceInvalidDetected") != detected or top.get("evidenceInvalidRecall") != (detected / expected if expected else 0.0):
                errors.append("evidence-invalid recall arithmetic is inconsistent")
    if isinstance(labels_manifest, dict) and not verify_labels(labels_manifest):
        labels = labels_manifest["samples"]
        top_metrics = report.get("topRouteMetrics")
        matrix = top_metrics.get("confusionMatrix", {}) if isinstance(top_metrics, dict) else {}
        if isinstance(matrix, dict):
            for route in REVIEW_ROUTES:
                row = matrix.get(route, {})
                if _matrix_is_valid(row) and sum(row.values()) != 3 * sum(sample["groundTruthRoute"] == route for sample in labels):
                    errors.append(f"top-level confusion matrix row {route} has the wrong total")
        per = report.get("perRequirementMetrics", {})
        if isinstance(per, dict):
            known_fails = sum(3 for sample in labels if sample["eligibleForVisualMetrics"] for item in sample["perRequirement"] if item["state"] == "fail")
            if per.get("knownVisualFailCount") != known_fails:
                errors.append("knownVisualFailCount does not match labels")
            matrices = per.get("confusionMatricesByRequirementType")
            if isinstance(matrices, dict):
                visual = matrices.get("visual", {})
                if isinstance(visual, dict):
                    false_passes = known_fail_pairs = known_pass_pairs = false_fails = 0
                    for requirement_matrix in visual.values():
                        if not _matrix_is_valid(requirement_matrix):
                            continue
                        for pair, count in requirement_matrix.items():
                            if pair == "fail->pass":
                                false_passes += count
                            if pair.startswith("fail->"):
                                known_fail_pairs += count
                            if pair.startswith("pass->"):
                                known_pass_pairs += count
                            if pair == "pass->fail":
                                false_fails += count
                    if per.get("falsePassCount") != false_passes or known_fails != known_fail_pairs:
                        errors.append("visual false-pass arithmetic is inconsistent")
                    expected_false_fail_rate = false_fails / known_pass_pairs if known_pass_pairs else 0.0
                    if per.get("falseFailRate") != expected_false_fail_rate:
                        errors.append("visual false-fail arithmetic is inconsistent")
            counts, uncounted = _count_cohort(labels)
            pre = per.get("qualificationPreconditions", {})
            if isinstance(pre, dict) and (pre.get("labelCount") != len(labels) or pre.get("cohortCounts") != counts or pre.get("uncountedSamples") != uncounted):
                errors.append("qualification preconditions do not match labels")
    return errors


def _load_json(path: Path) -> dict[str, Any]:
    return _read_json_object(path)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    generate = commands.add_parser("generate", help="write lightweight S0a fixture contracts")
    generate.add_argument("--cohort", choices=sorted(COHORTS), default="reduced")
    generate.add_argument("--output", type=Path, required=True)
    generate.add_argument("--sample-id-salt", help="operator-only anonymous ID salt; omitted creates/reuses an operator-only salt")
    generate.add_argument("--fresh-full-holdout", action="store_true", help="for --cohort full, use fresh source namespace/seeds instead of no-tuning expansion")
    freeze = commands.add_parser("freeze-labels", help="freeze completed human-adjudication labels")
    freeze.add_argument("--labels", type=Path, required=True)
    freeze.add_argument("--reviewer", required=True)
    freeze.add_argument("--frozen-at", required=True, help="RFC3339 timestamp from the human adjudication record")
    freeze.add_argument("--output", type=Path, required=True)
    score = commands.add_parser("score", help="score exactly three sealed isolated review sessions")
    score.add_argument("--labels", type=Path, required=True)
    score.add_argument("--reviews", type=Path, required=True)
    score.add_argument("--blind-manifest", type=Path, required=True)
    score.add_argument("--evidence-bundle", type=Path, required=True)
    score.add_argument("--review-context", type=Path, required=True)
    score.add_argument("--operator-ledger", type=Path, required=True)
    score.add_argument("--reduced-metrics", type=Path)
    score.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "generate":
            print(json.dumps(write_fixture_set(args.output, args.cohort, args.sample_id_salt, args.fresh_full_holdout), indent=2))
            return 0
        if args.command == "freeze-labels":
            _write_new_json(args.output, freeze_labels(_load_json(args.labels), args.reviewer, args.frozen_at))
            return 0
        report = calculate_metrics(_load_json(args.labels), _load_json(args.reviews), _load_json(args.blind_manifest), _load_json(args.evidence_bundle), _load_json(args.review_context), _load_json(args.operator_ledger), _load_json(args.reduced_metrics) if args.reduced_metrics else None)
        if args.output:
            _write_new_json(args.output, report)
        else:
            print(json.dumps(report, indent=2))
        return 0
    except CalibrationValidationError as exc:
        parser.error(str(exc))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
