"""Strict W24 S0a projected-evidence review corpus builder and scorer.

This module is the compatibility boundary between the write-once projected
artifacts and the existing label/metrics authority.  It never creates or reads
labels while building a review corpus.  At score time it supports either the
legacy frozen v2/v3 triplet or the projected v1 triplet, but rejects a mixture
of those protocol families.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import re
import stat
import tempfile
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable

try:  # package import in tests and direct-script compatibility
    from .s0a_calibration import (
        BLIND_SCHEMA_VERSION,
        BUNDLE_SCHEMA_VERSION,
        CONTEXT_SCHEMA_VERSION,
        COHORTS,
        COHORT_IDS,
        REVIEW_ROUTES,
        REVIEW_CORPUS_SCHEMA_VERSION,
        CalibrationValidationError,
        calculate_metrics as calculate_legacy_metrics,
        normalized_sha256,
        validate_operator_ledger,
        verify_labels,
        verify_metrics,
    )
except ImportError:  # pragma: no cover
    from s0a_calibration import (
        BLIND_SCHEMA_VERSION,
        BUNDLE_SCHEMA_VERSION,
        CONTEXT_SCHEMA_VERSION,
        COHORTS,
        COHORT_IDS,
        REVIEW_ROUTES,
        REVIEW_CORPUS_SCHEMA_VERSION,
        CalibrationValidationError,
        calculate_metrics as calculate_legacy_metrics,
        normalized_sha256,
        validate_operator_ledger,
        verify_labels,
        verify_metrics,
    )


PROJECTED_BLIND_SCHEMA = "s0a-projected-blind-manifest/v1"
PROJECTED_CONTEXT_SCHEMA = "s0a-projected-review-freeze-context/v1"
PROJECTED_BUNDLE_SCHEMA = "s0a-projected-operator-evidence-bundle/v1"
PROJECTED_CORPUS_SCHEMA = "s0a-isolated-review-corpus/v3"
PROJECTED_REVIEW_VERSION = "s0a-visual-qa/1.0"
PROJECTED_FREEZE_STATUS = "FROZEN_FOR_BLIND_REVIEW"
SESSION_DIRECTORY_SCHEMA = "s0a-isolated-review-session-directory/v1"
RICH_SESSION_SCHEMA_V1 = "s0a-visual-qa-session/1.0"
RICH_SESSION_SCHEMA_V2 = "s0a-visual-qa-session/2.0"
RICH_SESSION_SCHEMAS = frozenset({RICH_SESSION_SCHEMA_V1, RICH_SESSION_SCHEMA_V2})
PROJECTED_METRICS_SCHEMA = "s0a-metrics/v3"
PROJECTED_SCORER_VERSION = "w24-s0a-projected-scorer/1.1.0"
FIXED_FRAME_NUMBERS = frozenset({1, 21, 60, 120, 180, 240, 300, 360})
REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECTED_SCORER_SOURCE_FILES = (
    "tools/vfx/s0a_projected_scorer.py",
    "tools/vfx/s0a_calibration.py",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-visual-review.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-isolated-review-session.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-isolated-review-corpus.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-projected-metrics.schema.json",
    "docs/skills/unity-vfx-visual-qa/calibration/s0a-metrics.schema.json",
)

HASH_RE = re.compile(r"^sha256:[a-f0-9]{64}$")
SAMPLE_RE = re.compile(r"^s0a-[a-f0-9]{20}$")
REQUIREMENT_RE = re.compile(r"^s0a\.visual\.req\.[a-f0-9]{20}$")
RFC3339_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$")

PROJECTED_CONTEXT_FIELDS = frozenset(
    {
        "schemaVersion",
        "holdoutCohort",
        "freezeStatus",
        "holdoutIsolationMode",
        "operatorLedgerHash",
        "reducedMetricsHash",
        "qaPromptHash",
        "imageInputStrategyHash",
        "threeStateRulesHash",
        "aggregationRulesHash",
        "visualReviewSchemaHash",
        "modelVersionHash",
        "reviewContractHash",
        "buildHash",
        "captureProfilePolicyHash",
        "frameTableHash",
        "sceneHash",
        "prefabManifestHash",
        "captureToolHash",
        "contextHash",
    }
)
PROJECTED_BLIND_FIELDS = frozenset(
    {
        "schemaVersion",
        "holdoutCohort",
        "freezeStatus",
        "reviewContractHash",
        "captureProfilePolicyHash",
        "reviewContextHash",
        "samples",
        "manifestHash",
    }
)
PROJECTED_BLIND_SAMPLE_FIELDS = frozenset(
    {
        "sampleId",
        "designRequirementIds",
        "captureProfileInstanceHash",
        "evidenceManifest",
        "evidenceHash",
    }
)
PROJECTED_BUNDLE_FIELDS = frozenset(
    {
        "schemaVersion",
        "holdoutCohort",
        "freezeStatus",
        "operatorLedgerHash",
        "commandSetHash",
        "blindManifestHash",
        "reviewContextHash",
        "captureProfilePolicyHash",
        "samples",
        "bundleHash",
    }
)
PROJECTED_BUNDLE_SAMPLE_FIELDS = frozenset(
    {
        "sampleId",
        "commandHash",
        "completionHash",
        "rawCaptureSealFileHash",
        "selectedEvidenceDerivationHash",
        "ledgerTailHash",
        "captureProfileInstanceHash",
        "blindEvidenceHash",
        "blindEvidenceManifest",
        "selectedCaptureMetadata",
        "semanticTelemetry",
        "diagnosticFrames",
    }
)
REPORT_REQUIRED_FIELDS = frozenset(
    {
        "reviewVersion",
        "candidateId",
        "contractHash",
        "buildHash",
        "captureProfileHash",
        "evidenceHashes",
        "imageInputRead",
        "evidenceStatus",
        "contractStatus",
        "s0aTerminalStatus",
        "qaGateAuthority",
        "topLevelRoute",
        "perRequirement",
        "sealedReportHash",
    }
)
REPORT_FIELDS = REPORT_REQUIRED_FIELDS | {"conflicts"}
REQUIREMENT_FIELDS = frozenset(
    {
        "designRequirementId",
        "state",
        "countable",
        "reasonCode",
        "stateRef",
        "frameNumbers",
        "imageRegion",
        "observation",
    }
)
SESSION_FIELDS = frozenset(
    {"sessionId", "reviewerSessionId", "isolated", "startedAt", "completedAt", "reviews", "sessionHash"}
)
RICH_SESSION_FIELDS = frozenset(
    {
        "schemaVersion",
        "reviewVersion",
        "sessionId",
        "reviewerSessionId",
        "candidateId",
        "contractHash",
        "buildHash",
        "captureProfileHash",
        "isolated",
        "qaGateAuthority",
        "s0aTerminalStatus",
        "sampleCount",
        "routeCounts",
        "validationChecks",
        "reports",
        "sessionHash",
    }
)
RICH_REVIEW_FIELDS = frozenset(
    {
        "sampleId",
        "reportFile",
        "evidenceHash",
        "imageReviewComplete",
        "reviewedSeedOrdinals",
        "reviewedFrameNumbers",
        "expectedBeautyFrameCount",
        "presentBeautyFrameCount",
        "reviewedBeautySlotCount",
        "reviewedBeautySlots",
        "report",
    }
)
REVIEWED_SLOT_FIELDS = frozenset({"seedOrdinal", "frameNumber", "availability"})
CORPUS_FIELDS = frozenset(
    {
        "schemaVersion",
        "projectionReceiptHash",
        "blindManifestHash",
        "evidenceBundleHash",
        "reviewContextHash",
        "reviewContractHash",
        "captureProfilePolicyHash",
        "modelVersionHash",
        "sessionProtocols",
        "sessions",
        "corpusHash",
    }
)
REVIEW_ROUTES_LOCAL = frozenset(REVIEW_ROUTES)
TRI_STATES = frozenset({"pass", "fail", "uncertain"})
REASON_CODES = frozenset({"OBSERVED", "EVIDENCE_INVALID", "CONTRACT_AMBIGUOUS", "VISUAL_UNCERTAIN"})
PROJECTED_METRICS_FIELDS = frozenset(
    {
        "schemaVersion",
        "scorerVersion",
        "scorerSourceHash",
        "projectionReceiptHash",
        "reviewCorpusHash",
        "holdoutCohort",
        "labelManifestHash",
        "perRequirementMetrics",
        "topRouteMetrics",
        "stability",
        "terminalStatus",
        "reportHash",
    }
)


def _is_hash(value: Any) -> bool:
    return isinstance(value, str) and bool(HASH_RE.fullmatch(value))


def _is_nonempty(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _is_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _hash_matches(document: Any, field: str) -> bool:
    return (
        isinstance(document, dict)
        and _is_hash(document.get(field))
        and document[field] == normalized_sha256(document, (field,))
    )


def _exact(document: Any, fields: frozenset[str], label: str, errors: list[str]) -> bool:
    if not isinstance(document, dict):
        errors.append(f"{label} must be an object")
        return False
    if set(document) != fields:
        errors.append(f"{label} fields are invalid")
        return False
    return True


def _reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON member: {key}")
        result[key] = value
    return result


def _reject_nonfinite(token: str) -> None:
    raise ValueError(f"non-finite JSON number: {token}")


def _check_scalars(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            key.encode("utf-8", errors="strict")
            _check_scalars(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _check_scalars(child, f"{path}[{index}]")
    elif isinstance(value, str):
        value.encode("utf-8", errors="strict")
    elif isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"non-finite JSON number at {path}")


def load_strict_json(path: Path, label: str = "JSON artifact") -> dict[str, Any]:
    original = Path(path)
    if ".." in original.parts:
        raise CalibrationValidationError(f"Parent traversal is forbidden for {label}: {path}")
    absolute = Path(os.path.abspath(os.fspath(original)))
    for component in reversed([absolute, *absolute.parents]):
        if component.exists() or component.is_symlink():
            if _is_reparse(component):
                raise CalibrationValidationError(
                    f"{label} traverses a symlink/junction/reparse point: {component}"
                )
    if not absolute.is_file():
        raise CalibrationValidationError(f"{label} is not an existing regular file: {absolute}")
    try:
        raw = absolute.read_text(encoding="utf-8")
        document = json.loads(
            raw,
            object_pairs_hook=_reject_duplicate_pairs,
            parse_constant=_reject_nonfinite,
        )
        _check_scalars(document)
    except (OSError, UnicodeError, ValueError, json.JSONDecodeError) as exc:
        raise CalibrationValidationError(f"Cannot read strict {label} {absolute}: {exc}") from exc
    if not isinstance(document, dict):
        raise CalibrationValidationError(f"{label} must be an object: {absolute}")
    return document


def _write_json_new(path: Path, document: dict[str, Any]) -> None:
    original = Path(path)
    if ".." in original.parts:
        raise CalibrationValidationError(f"Parent traversal is forbidden for write-once output: {path}")
    path = Path(os.path.abspath(os.fspath(original)))
    if path.exists() or path.is_symlink():
        raise CalibrationValidationError(f"Refusing to overwrite write-once artifact {path}")
    try:
        _check_scalars(document)
        encoded = json.dumps(document, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
        parent = Path(os.path.abspath(os.fspath(path.parent)))
        for component in reversed([parent, *parent.parents]):
            if component.exists() or component.is_symlink():
                if _is_reparse(component):
                    raise CalibrationValidationError(
                        f"Write-once output traverses a symlink/junction/reparse point: {component}"
                    )
        path.parent.mkdir(parents=True, exist_ok=True)
        for component in reversed([path.parent, *path.parent.parents]):
            if component.exists() or component.is_symlink():
                if _is_reparse(component):
                    raise CalibrationValidationError(
                        f"Write-once output ancestry became a reparse point: {component}"
                    )
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{path.name}.", suffix=".publishing", dir=path.parent
        )
        temporary = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(encoded.encode("utf-8"))
                stream.flush()
                os.fsync(stream.fileno())
            os.link(temporary, path, follow_symlinks=False)
        except FileExistsError as exc:
            raise CalibrationValidationError(
                f"Refusing to overwrite write-once artifact {path}"
            ) from exc
        finally:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass
    except (OSError, UnicodeError, ValueError) as exc:
        raise CalibrationValidationError(f"Cannot write strict artifact {path}: {exc}") from exc


def _is_reparse(path: Path) -> bool:
    try:
        info = path.lstat()
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot inspect path {path}: {exc}") from exc
    junction_probe = getattr(path, "is_junction", None)
    try:
        is_junction = bool(junction_probe()) if callable(junction_probe) else False
    except OSError as exc:
        raise CalibrationValidationError(f"Cannot inspect junction {path}: {exc}") from exc
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return (
        path.is_symlink()
        or is_junction
        or stat.S_ISLNK(info.st_mode)
        or bool(getattr(info, "st_file_attributes", 0) & reparse_flag)
    )


def _secure_directory(path: Path, label: str) -> Path:
    absolute = Path(os.path.abspath(os.fspath(path)))
    if ".." in path.parts:
        raise CalibrationValidationError(f"Parent traversal is forbidden for {label}")
    for component in reversed([absolute, *absolute.parents]):
        if component.exists() or component.is_symlink():
            if _is_reparse(component):
                raise CalibrationValidationError(f"{label} traverses a symlink/junction/reparse point: {component}")
    if not absolute.is_dir():
        raise CalibrationValidationError(f"{label} is not an existing directory: {absolute}")
    for parent, directories, files in os.walk(absolute, followlinks=False):
        for name in [*directories, *files]:
            entry = Path(parent) / name
            if _is_reparse(entry):
                raise CalibrationValidationError(f"{label} contains a symlink/junction/reparse point: {entry}")
    return absolute


def _reject_output_within(output: Path, protected_roots: Iterable[Path]) -> None:
    output_absolute = Path(os.path.abspath(os.fspath(output)))
    for protected in protected_roots:
        root = Path(os.path.abspath(os.fspath(protected)))
        try:
            common = Path(os.path.commonpath((os.fspath(output_absolute), os.fspath(root))))
        except ValueError:
            continue
        if common == root:
            raise CalibrationValidationError(
                f"Output must not be written inside sealed/protected input root: {root}"
            )


def _valid_time(value: Any) -> bool:
    if not isinstance(value, str) or not RFC3339_RE.fullmatch(value):
        return False
    try:
        datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return False
    return True


def _time_value(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def _model_hash(model_version_id: str) -> str:
    if not _is_nonempty(model_version_id):
        raise CalibrationValidationError("model_version_id must be a non-empty frozen identifier")
    return "sha256:" + hashlib.sha256(
        ("W24-S0a-model-version|" + model_version_id.strip()).encode("utf-8")
    ).hexdigest()


def _projected_scorer_source_hash() -> str:
    entries: list[str] = []
    for relative in PROJECTED_SCORER_SOURCE_FILES:
        path = REPO_ROOT / relative
        if not path.is_file() or _is_reparse(path):
            raise CalibrationValidationError(f"Projected scorer source is missing or unsafe: {relative}")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        entries.append(f"{relative}|sha256:{digest}")
    return "sha256:" + hashlib.sha256("\n".join(entries).encode("utf-8")).hexdigest()


def _cohort_count(cohort_id: Any) -> int | None:
    if cohort_id == COHORT_IDS["reduced"]:
        return sum(COHORTS["reduced"].values())
    if cohort_id == COHORT_IDS["full"]:
        return sum(COHORTS["full"].values())
    return None


def rich_validation_checks(cohort_count: int) -> list[str]:
    """Return the one exact authored rich-session check set for a formal cohort."""
    if type(cohort_count) is not int or cohort_count not in {66, 110}:
        raise CalibrationValidationError(
            "Rich session validation checks support only exact 66- or 110-sample cohorts"
        )
    return [
        f"schema-valid-{cohort_count}",
        f"sealed-report-hash-valid-{cohort_count}",
        "session-hash-valid",
        f"unique-sample-ids-{cohort_count}",
        f"embedded-report-file-json-equal-{cohort_count}",
        "reviewed-seeds-3-and-frame-slots-8-per-seed",
    ]


def validate_projected_context(context: Any) -> list[str]:
    errors: list[str] = []
    if not _exact(context, PROJECTED_CONTEXT_FIELDS, "projected review context", errors):
        return errors
    if context.get("schemaVersion") != PROJECTED_CONTEXT_SCHEMA:
        errors.append("projected review context schemaVersion is invalid")
    if context.get("freezeStatus") != PROJECTED_FREEZE_STATUS:
        errors.append("projected review context is not frozen for blind review")
    if _cohort_count(context.get("holdoutCohort")) is None:
        errors.append("projected review context holdoutCohort is invalid")
    mode = context.get("holdoutIsolationMode")
    if mode not in {"INITIAL_REDUCED", "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS", "FRESH_INDEPENDENT_FULL"}:
        errors.append("projected review context isolation mode is invalid")
    elif (
        (context.get("holdoutCohort") == COHORT_IDS["reduced"] and mode != "INITIAL_REDUCED")
        or (context.get("holdoutCohort") == COHORT_IDS["full"] and mode == "INITIAL_REDUCED")
    ):
        errors.append("projected review context cohort and isolation mode are incompatible")
    for field in PROJECTED_CONTEXT_FIELDS - {"schemaVersion", "holdoutCohort", "freezeStatus", "holdoutIsolationMode", "reducedMetricsHash"}:
        if field != "contextHash" and not _is_hash(context.get(field)):
            errors.append(f"projected review context {field} is invalid")
    if mode == "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS":
        if not _is_hash(context.get("reducedMetricsHash")):
            errors.append("expanded projected context requires reducedMetricsHash")
    elif context.get("reducedMetricsHash") is not None:
        errors.append("initial/fresh projected context must not contain reducedMetricsHash")
    if not _hash_matches(context, "contextHash"):
        errors.append("projected review context hash binding is invalid")
    return errors


def validate_projected_manifest(manifest: Any) -> list[str]:
    errors: list[str] = []
    if not _exact(manifest, PROJECTED_BLIND_FIELDS, "projected blind manifest", errors):
        return errors
    if manifest.get("schemaVersion") != PROJECTED_BLIND_SCHEMA:
        errors.append("projected blind manifest schemaVersion is invalid")
    if manifest.get("freezeStatus") != PROJECTED_FREEZE_STATUS:
        errors.append("projected blind manifest is not frozen for blind review")
    expected_count = _cohort_count(manifest.get("holdoutCohort"))
    if expected_count is None:
        errors.append("projected blind manifest holdoutCohort is invalid")
    for field in ("reviewContractHash", "captureProfilePolicyHash", "reviewContextHash"):
        if not _is_hash(manifest.get(field)):
            errors.append(f"projected blind manifest {field} is invalid")
    if not _hash_matches(manifest, "manifestHash"):
        errors.append("projected blind manifest hash binding is invalid")
    samples = manifest.get("samples")
    if not isinstance(samples, list) or expected_count is None or len(samples) != expected_count:
        return [*errors, "projected blind manifest does not contain the exact cohort"]
    ids: set[str] = set()
    requirements: set[str] = set()
    evidence_hashes: set[str] = set()
    profile_hashes: set[str] = set()
    for index, sample in enumerate(samples):
        prefix = f"projected blind sample[{index}]"
        if not _exact(sample, PROJECTED_BLIND_SAMPLE_FIELDS, prefix, errors):
            continue
        sample_id = sample.get("sampleId")
        if not isinstance(sample_id, str) or not SAMPLE_RE.fullmatch(sample_id) or sample_id in ids:
            errors.append(f"{prefix} sampleId is invalid or duplicate")
        else:
            ids.add(sample_id)
        assigned = sample.get("designRequirementIds")
        if (
            not isinstance(assigned, list)
            or len(assigned) != 1
            or not isinstance(assigned[0], str)
            or not REQUIREMENT_RE.fullmatch(assigned[0])
            or assigned[0] in requirements
        ):
            errors.append(f"{prefix} designRequirementIds are invalid, non-opaque, or reused")
        else:
            requirements.add(assigned[0])
        evidence_hash = sample.get("evidenceHash")
        profile_hash = sample.get("captureProfileInstanceHash")
        if not _is_hash(evidence_hash) or evidence_hash in evidence_hashes:
            errors.append(f"{prefix} evidenceHash is invalid or reused")
        else:
            evidence_hashes.add(evidence_hash)
        if not _is_hash(profile_hash) or profile_hash in profile_hashes:
            errors.append(f"{prefix} captureProfileInstanceHash is invalid or reused")
        else:
            profile_hashes.add(profile_hash)
        if sample.get("evidenceManifest") != f"evidence/{sample_id}.evidence.json":
            errors.append(f"{prefix} evidenceManifest does not bind sampleId")
    return errors


def _valid_integrity_pair(value: Any) -> bool:
    return (
        isinstance(value, dict)
        and set(value) == {"file", "declaredHash", "actualHash"}
        and _is_nonempty(value.get("file"))
        and _is_hash(value.get("declaredHash"))
        and _is_hash(value.get("actualHash"))
    )


def _valid_hashed_file(value: Any) -> bool:
    return (
        isinstance(value, dict)
        and set(value) == {"file", "sha256"}
        and _is_nonempty(value.get("file"))
        and _is_hash(value.get("sha256"))
    )


def validate_projected_bundle(bundle: Any) -> list[str]:
    errors: list[str] = []
    if not _exact(bundle, PROJECTED_BUNDLE_FIELDS, "projected evidence bundle", errors):
        return errors
    if bundle.get("schemaVersion") != PROJECTED_BUNDLE_SCHEMA:
        errors.append("projected evidence bundle schemaVersion is invalid")
    if bundle.get("freezeStatus") != PROJECTED_FREEZE_STATUS:
        errors.append("projected evidence bundle is not frozen for blind review")
    expected_count = _cohort_count(bundle.get("holdoutCohort"))
    for field in (
        "operatorLedgerHash",
        "commandSetHash",
        "blindManifestHash",
        "reviewContextHash",
        "captureProfilePolicyHash",
    ):
        if not _is_hash(bundle.get(field)):
            errors.append(f"projected evidence bundle {field} is invalid")
    if not _hash_matches(bundle, "bundleHash"):
        errors.append("projected evidence bundle hash binding is invalid")
    samples = bundle.get("samples")
    if not isinstance(samples, list) or expected_count is None or len(samples) != expected_count:
        return [*errors, "projected evidence bundle does not contain the exact cohort"]
    ids: set[str] = set()
    for index, sample in enumerate(samples):
        prefix = f"projected bundle sample[{index}]"
        if not _exact(sample, PROJECTED_BUNDLE_SAMPLE_FIELDS, prefix, errors):
            continue
        sample_id = sample.get("sampleId")
        if not isinstance(sample_id, str) or not SAMPLE_RE.fullmatch(sample_id) or sample_id in ids:
            errors.append(f"{prefix} sampleId is invalid or duplicate")
        else:
            ids.add(sample_id)
        for field in (
            "commandHash",
            "completionHash",
            "rawCaptureSealFileHash",
            "ledgerTailHash",
            "captureProfileInstanceHash",
            "blindEvidenceHash",
        ):
            if not _is_hash(sample.get(field)):
                errors.append(f"{prefix} {field} is invalid")
        if sample.get("selectedEvidenceDerivationHash") is not None and not _is_hash(sample.get("selectedEvidenceDerivationHash")):
            errors.append(f"{prefix} selectedEvidenceDerivationHash is invalid")
        if sample.get("blindEvidenceManifest") != f"blind/evidence/{sample_id}.evidence.json":
            errors.append(f"{prefix} blindEvidenceManifest does not bind sampleId")
        if not _valid_integrity_pair(sample.get("selectedCaptureMetadata")):
            errors.append(f"{prefix} selectedCaptureMetadata is invalid")
        if not _valid_hashed_file(sample.get("semanticTelemetry")):
            errors.append(f"{prefix} semanticTelemetry is invalid")
        diagnostics = sample.get("diagnosticFrames")
        if not isinstance(diagnostics, list) or len(diagnostics) != 24:
            errors.append(f"{prefix} diagnosticFrames must contain 24 entries")
        else:
            expected_pairs = {(seed, frame) for seed in range(3) for frame in FIXED_FRAME_NUMBERS}
            actual_pairs: set[tuple[int, int]] = set()
            for diagnostic in diagnostics:
                if not isinstance(diagnostic, dict) or set(diagnostic) != {
                    "seedOrdinal", "frameIndex", "file", "declaredHash", "actualHash"
                }:
                    errors.append(f"{prefix} diagnostic frame fields are invalid")
                    continue
                pair = (diagnostic.get("seedOrdinal"), diagnostic.get("frameIndex"))
                if pair not in expected_pairs or pair in actual_pairs:
                    errors.append(f"{prefix} diagnostic frame seed/frame pair is invalid or duplicate")
                else:
                    actual_pairs.add(pair)
                if not _is_nonempty(diagnostic.get("file")):
                    errors.append(f"{prefix} diagnostic frame file is invalid")
                if not _is_hash(diagnostic.get("declaredHash")) or not _is_hash(diagnostic.get("actualHash")):
                    errors.append(f"{prefix} diagnostic frame hashes are invalid")
            if actual_pairs != expected_pairs:
                errors.append(f"{prefix} diagnosticFrames do not contain the fixed 3x8 matrix")
    return errors


def _manifest_assignments(manifest: dict[str, Any]) -> dict[str, tuple[list[str], str, str]]:
    return {
        sample["sampleId"]: (
            list(sample["designRequirementIds"]),
            sample["evidenceHash"],
            sample["captureProfileInstanceHash"],
        )
        for sample in manifest["samples"]
    }


def _bundle_samples(bundle: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {sample["sampleId"]: sample for sample in bundle["samples"]}


def validate_projected_protocol_bindings(
    manifest: Any,
    bundle: Any,
    context: Any,
) -> list[str]:
    errors = [
        *validate_projected_manifest(manifest),
        *validate_projected_bundle(bundle),
        *validate_projected_context(context),
    ]
    if errors or not all(isinstance(item, dict) for item in (manifest, bundle, context)):
        return errors
    if len({manifest["holdoutCohort"], bundle["holdoutCohort"], context["holdoutCohort"]}) != 1:
        errors.append("projected manifest, bundle, and context cohorts differ")
    if (
        manifest["reviewContractHash"] != context["reviewContractHash"]
        or manifest["captureProfilePolicyHash"] != context["captureProfilePolicyHash"]
        or manifest["reviewContextHash"] != context["contextHash"]
    ):
        errors.append("projected manifest does not bind review contract/profile/context")
    if (
        bundle["blindManifestHash"] != manifest["manifestHash"]
        or bundle["reviewContextHash"] != context["contextHash"]
        or bundle["captureProfilePolicyHash"] != context["captureProfilePolicyHash"]
        or bundle["operatorLedgerHash"] != context["operatorLedgerHash"]
    ):
        errors.append("projected bundle does not bind manifest/profile/context/ledger")
    assignments = _manifest_assignments(manifest)
    bundled = _bundle_samples(bundle)
    if set(assignments) != set(bundled):
        errors.append("projected manifest and bundle sample sets differ")
    else:
        for sample_id, (_requirements, evidence_hash, profile_hash) in assignments.items():
            record = bundled[sample_id]
            if record["blindEvidenceHash"] != evidence_hash:
                errors.append(f"{sample_id}: projected bundle evidenceHash differs from blind manifest")
            if record["captureProfileInstanceHash"] != profile_hash:
                errors.append(f"{sample_id}: projected bundle profile instance differs from blind manifest")
    return errors


def _context_report_identities(context: dict[str, Any]) -> dict[str, str]:
    return {
        "contractHash": context["reviewContractHash"],
        "buildHash": context["buildHash"],
        "captureProfileHash": context["captureProfilePolicyHash"],
    }


def validate_projected_report(
    report: Any,
    requirement_ids: list[str],
    evidence_hash: str,
    context: dict[str, Any],
) -> list[str]:
    errors: list[str] = []
    if not isinstance(report, dict):
        return ["projected VFX review report must be an object"]
    if not REPORT_REQUIRED_FIELDS.issubset(report) or not set(report).issubset(REPORT_FIELDS):
        return ["projected VFX review report fields are invalid"]
    if report.get("reviewVersion") != PROJECTED_REVIEW_VERSION:
        errors.append("projected VFX report reviewVersion is invalid")
    if report.get("candidateId") != "C0":
        errors.append("projected S0a report candidateId must be C0")
    if not _hash_matches(report, "sealedReportHash"):
        errors.append("projected VFX report sealedReportHash is invalid")
    identities = _context_report_identities(context)
    if any(report.get(field) != expected for field, expected in identities.items()):
        errors.append("projected VFX report does not bind reviewContractHash/buildHash/captureProfilePolicyHash")
    if report.get("evidenceHashes") != [evidence_hash]:
        errors.append("projected VFX report evidenceHashes must be exactly its projected evidenceHash")
    if report.get("s0aTerminalStatus") is not None or report.get("qaGateAuthority") != "advisory-only":
        errors.append("projected pre-score VFX reports cannot claim an S0a terminal status or gate authority")
    if not isinstance(report.get("imageInputRead"), bool):
        errors.append("projected VFX report imageInputRead must be boolean")
    if report.get("evidenceStatus") not in {"valid", "invalid"}:
        errors.append("projected VFX report evidenceStatus is invalid")
    if report.get("contractStatus") not in {"unambiguous", "ambiguous"}:
        errors.append("projected VFX report contractStatus is invalid")
    route = report.get("topLevelRoute")
    if route not in REVIEW_ROUTES_LOCAL:
        errors.append("projected VFX report topLevelRoute is invalid")
    if "conflicts" in report and (
        not isinstance(report["conflicts"], list)
        or any(not _is_nonempty(value) for value in report["conflicts"])
    ):
        errors.append("projected VFX report conflicts are invalid")
    requirements = report.get("perRequirement")
    if not isinstance(requirements, list) or len(requirements) != len(requirement_ids):
        return [*errors, "projected VFX report requirement count differs from blind assignment"]
    seen: set[str] = set()
    valid_items: list[dict[str, Any]] = []
    for item in requirements:
        if not _exact(item, REQUIREMENT_FIELDS, "projected requirement review", errors):
            continue
        requirement_id = item.get("designRequirementId")
        if requirement_id not in requirement_ids or requirement_id in seen:
            errors.append("projected requirement review assignment is foreign or duplicate")
        else:
            seen.add(requirement_id)
        if item.get("state") not in TRI_STATES or not isinstance(item.get("countable"), bool) or item.get("reasonCode") not in REASON_CODES:
            errors.append("projected requirement review state/countable/reasonCode is invalid")
        if not all(_is_nonempty(item.get(field)) for field in ("stateRef", "imageRegion", "observation")):
            errors.append("projected requirement review observation fields are invalid")
        frames = item.get("frameNumbers")
        if (
            not isinstance(frames, list)
            or not frames
            or len(set(frames)) != len(frames)
            or any(not _is_int(frame) or frame not in FIXED_FRAME_NUMBERS for frame in frames)
        ):
            errors.append("projected requirement review frameNumbers are invalid")
        valid_items.append(item)
    if seen != set(requirement_ids):
        errors.append("projected VFX report does not contain the exact blind assignment")
    if route == "VISUAL_PASS":
        if not (
            report.get("imageInputRead") is True
            and report.get("evidenceStatus") == "valid"
            and report.get("contractStatus") == "unambiguous"
            and all(item.get("state") == "pass" and item.get("countable") is True and item.get("reasonCode") == "OBSERVED" for item in valid_items)
        ):
            errors.append("projected VISUAL_PASS report states are invalid")
    elif route == "VISUAL_FAIL":
        if not (
            report.get("imageInputRead") is True
            and report.get("evidenceStatus") == "valid"
            and report.get("contractStatus") == "unambiguous"
            and any(item.get("state") == "fail" and item.get("countable") is True and item.get("reasonCode") == "OBSERVED" for item in valid_items)
        ):
            errors.append("projected VISUAL_FAIL report needs an observed countable fail")
    elif route == "VISUAL_UNCERTAIN":
        if not (
            report.get("imageInputRead") is True
            and report.get("evidenceStatus") == "valid"
            and report.get("contractStatus") == "unambiguous"
            and any(item.get("state") == "uncertain" and item.get("countable") is True and item.get("reasonCode") == "VISUAL_UNCERTAIN" for item in valid_items)
            and all(item.get("state") != "fail" for item in valid_items)
        ):
            errors.append("projected VISUAL_UNCERTAIN report states are invalid")
    elif route == "EVIDENCE_INVALID":
        if not (
            report.get("evidenceStatus") == "invalid"
            and all(item.get("state") == "uncertain" and item.get("countable") is False and item.get("reasonCode") == "EVIDENCE_INVALID" for item in valid_items)
        ):
            errors.append("projected EVIDENCE_INVALID report states are invalid")
    elif route == "CONTRACT_AMBIGUOUS":
        if not (
            report.get("evidenceStatus") == "valid"
            and report.get("contractStatus") == "ambiguous"
            and all(item.get("state") == "uncertain" and item.get("countable") is False and item.get("reasonCode") == "CONTRACT_AMBIGUOUS" for item in valid_items)
        ):
            errors.append("projected CONTRACT_AMBIGUOUS report states are invalid")
    return errors


def _session_records(session: dict[str, Any]) -> list[dict[str, Any]] | None:
    if set(session) == SESSION_FIELDS and isinstance(session.get("reviews"), list):
        return session["reviews"]
    if set(session) == RICH_SESSION_FIELDS and isinstance(session.get("reports"), list):
        return [
            {"sampleId": record.get("sampleId"), "report": record.get("report")}
            if isinstance(record, dict)
            else {"sampleId": None, "report": None}
            for record in session["reports"]
        ]
    return None


def _session_schema_version(session: dict[str, Any]) -> str | None:
    if set(session) == SESSION_FIELDS:
        return SESSION_DIRECTORY_SCHEMA
    if set(session) == RICH_SESSION_FIELDS:
        version = session.get("schemaVersion")
        return version if isinstance(version, str) and version in RICH_SESSION_SCHEMAS else None
    return None


def _validate_session(
    session: Any,
    assignments: dict[str, tuple[list[str], str, str]],
    context: dict[str, Any],
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(session, dict):
        return [f"{label} must be an object"]
    is_simple = set(session) == SESSION_FIELDS
    is_rich = set(session) == RICH_SESSION_FIELDS
    if not is_simple and not is_rich:
        return [f"{label} fields are invalid"]
    if not _is_nonempty(session.get("sessionId")) or not _is_nonempty(session.get("reviewerSessionId")):
        errors.append(f"{label} session identities are required")
    if session.get("isolated") is not True:
        errors.append(f"{label} must explicitly attest isolated=true")
    if is_simple:
        if not _valid_time(session.get("startedAt")) or not _valid_time(session.get("completedAt")):
            errors.append(f"{label} timestamps must be RFC3339 with timezone")
        elif _time_value(session["completedAt"]) <= _time_value(session["startedAt"]):
            errors.append(f"{label} completedAt must be after startedAt")
    else:
        rich_schema_version = session.get("schemaVersion")
        if (
            not isinstance(rich_schema_version, str)
            or rich_schema_version not in RICH_SESSION_SCHEMAS
            or session.get("reviewVersion") != PROJECTED_REVIEW_VERSION
            or session.get("candidateId") != "C0"
            or session.get("qaGateAuthority") != "advisory-only"
            or session.get("s0aTerminalStatus") is not None
        ):
            errors.append(f"{label} rich session protocol/authority fields are invalid")
        cohort_count = len(assignments)
        if rich_schema_version == RICH_SESSION_SCHEMA_V1 and cohort_count != 66:
            errors.append(
                f"{label} legacy rich session 1.0 is reduced66-only; full110 requires rich session 2.0"
            )
        if any(
            session.get(field) != expected
            for field, expected in _context_report_identities(context).items()
        ):
            errors.append(f"{label} rich session does not bind frozen context identities")
        checks = session.get("validationChecks")
        expected_checks = set(rich_validation_checks(cohort_count))
        if (
            not isinstance(checks, list)
            or any(not isinstance(check, str) for check in checks)
            or len(checks) != len(set(checks))
            or set(checks) != expected_checks
        ):
            errors.append(f"{label} rich validationChecks are invalid")
    if not _hash_matches(session, "sessionHash"):
        errors.append(f"{label} sessionHash is invalid")
    reviews = _session_records(session)
    if not isinstance(reviews, list) or len(reviews) != len(assignments):
        return [*errors, f"{label} does not contain the exact cohort review count"]
    if is_rich and session.get("sampleCount") != len(assignments):
        errors.append(f"{label} rich sampleCount differs from the exact cohort")
    review_order = [
        record.get("sampleId") if isinstance(record, dict) else None for record in reviews
    ]
    if is_simple and review_order != list(assignments):
        errors.append(f"{label} review order differs from the frozen blind manifest")
    seen: set[str] = set()
    for index, record in enumerate(reviews):
        prefix = f"{label}/review[{index}]"
        if not isinstance(record, dict) or set(record) != {"sampleId", "report"}:
            errors.append(f"{prefix} fields are invalid")
            continue
        sample_id = record.get("sampleId")
        if sample_id not in assignments or sample_id in seen:
            errors.append(f"{prefix} sampleId is foreign or duplicate")
            continue
        seen.add(sample_id)
        requirement_ids, evidence_hash, _profile_hash = assignments[sample_id]
        if is_rich:
            rich = session["reports"][index]
            if not _exact(rich, RICH_REVIEW_FIELDS, f"{prefix}/rich", errors):
                continue
            if rich.get("evidenceHash") != evidence_hash:
                errors.append(f"{prefix} rich evidenceHash differs from blind assignment")
            if rich.get("reportFile") not in {
                f"reports/{sample_id}.report.json",
                f"reports/{sample_id}.review.json",
            }:
                errors.append(f"{prefix} rich reportFile does not bind sampleId")
            if (
                rich.get("imageReviewComplete") is not True
                or rich.get("reviewedSeedOrdinals") != [0, 1, 2]
                or rich.get("reviewedFrameNumbers") != sorted(FIXED_FRAME_NUMBERS)
                or rich.get("expectedBeautyFrameCount") != 24
                or rich.get("reviewedBeautySlotCount") != 24
            ):
                errors.append(f"{prefix} rich fixed 3x8 review attestation is invalid")
            slots = rich.get("reviewedBeautySlots")
            expected_pairs = {(seed, frame) for seed in range(3) for frame in FIXED_FRAME_NUMBERS}
            actual_pairs: set[tuple[int, int]] = set()
            present_count = 0
            if not isinstance(slots, list) or len(slots) != 24:
                errors.append(f"{prefix} rich reviewedBeautySlots are invalid")
            else:
                for slot in slots:
                    if not isinstance(slot, dict) or set(slot) != REVIEWED_SLOT_FIELDS:
                        errors.append(f"{prefix} rich reviewed slot fields are invalid")
                        continue
                    pair = (slot.get("seedOrdinal"), slot.get("frameNumber"))
                    if pair not in expected_pairs or pair in actual_pairs:
                        errors.append(f"{prefix} rich reviewed slot is foreign or duplicate")
                    else:
                        actual_pairs.add(pair)
                    if slot.get("availability") not in {"present", "missing"}:
                        errors.append(f"{prefix} rich reviewed slot availability is invalid")
                    present_count += int(slot.get("availability") == "present")
                if actual_pairs != expected_pairs:
                    errors.append(f"{prefix} rich reviewed slots do not cover fixed 3x8")
            if rich.get("presentBeautyFrameCount") != present_count:
                errors.append(f"{prefix} rich presentBeautyFrameCount is inconsistent")
        errors.extend(
            f"{prefix}: {message}"
            for message in validate_projected_report(record.get("report"), requirement_ids, evidence_hash, context)
        )
    if seen != set(assignments):
        errors.append(f"{label} has missing or extra sample assignments")
    if is_rich:
        route_counts = session.get("routeCounts")
        observed_counts = Counter(
            record["report"].get("topLevelRoute")
            for record in reviews
            if isinstance(record.get("report"), dict)
        )
        expected_route_counts = {route: observed_counts.get(route, 0) for route in sorted(REVIEW_ROUTES_LOCAL)}
        if route_counts != expected_route_counts:
            errors.append(f"{label} rich routeCounts are inconsistent")
    return errors


def validate_projected_corpus(
    corpus: Any,
    manifest: Any,
    bundle: Any,
    context: Any,
    projection_receipt_hash: str,
) -> list[str]:
    errors = validate_projected_protocol_bindings(manifest, bundle, context)
    if errors or not all(isinstance(item, dict) for item in (corpus, manifest, bundle, context)):
        if not isinstance(corpus, dict):
            errors.append("projected review corpus must be an object")
        return errors
    if not _exact(corpus, CORPUS_FIELDS, "projected review corpus", errors):
        return errors
    if corpus.get("schemaVersion") != PROJECTED_CORPUS_SCHEMA or not _hash_matches(corpus, "corpusHash"):
        errors.append("projected review corpus schema/hash is invalid")
    expected_headers = {
        "projectionReceiptHash": projection_receipt_hash,
        "blindManifestHash": manifest["manifestHash"],
        "evidenceBundleHash": bundle["bundleHash"],
        "reviewContextHash": context["contextHash"],
        "reviewContractHash": context["reviewContractHash"],
        "captureProfilePolicyHash": context["captureProfilePolicyHash"],
        "modelVersionHash": context["modelVersionHash"],
    }
    if any(corpus.get(field) != value for field, value in expected_headers.items()):
        errors.append("projected review corpus does not bind manifest/bundle/context/contract/profile/model")
    sessions = corpus.get("sessions")
    if not isinstance(sessions, list) or len(sessions) != 3:
        return [*errors, "projected review corpus must contain exactly three isolated sessions"]
    assignments = _manifest_assignments(manifest)
    expected_protocols = [
        {
            "sessionId": session.get("sessionId"),
            "schemaVersion": _session_schema_version(session),
            "sessionHash": session.get("sessionHash"),
        }
        if isinstance(session, dict)
        else {"sessionId": None, "schemaVersion": None, "sessionHash": None}
        for session in sessions
    ]
    if corpus.get("sessionProtocols") != expected_protocols:
        errors.append("projected review corpus sessionProtocols do not bind exact session shapes/hashes")
    session_ids: set[str] = set()
    reviewer_ids: set[str] = set()
    session_hashes: set[str] = set()
    per_sample_report_hashes: dict[str, set[str]] = defaultdict(set)
    for index, session in enumerate(sessions):
        errors.extend(_validate_session(session, assignments, context, f"session[{index}]"))
        if isinstance(session, dict):
            for field, seen in (
                ("sessionId", session_ids),
                ("reviewerSessionId", reviewer_ids),
                ("sessionHash", session_hashes),
            ):
                value = session.get(field)
                if isinstance(value, str):
                    if value in seen:
                        errors.append(f"projected review corpus repeats {field}: {value}")
                    seen.add(value)
            for record in _session_records(session) or []:
                if not isinstance(record, dict) or not isinstance(record.get("report"), dict):
                    continue
                sample_id = record.get("sampleId")
                report_hash = record["report"].get("sealedReportHash")
                if isinstance(sample_id, str) and isinstance(report_hash, str):
                    if report_hash in per_sample_report_hashes[sample_id]:
                        errors.append(
                            f"projected review corpus replays sealed report for {sample_id} across sessions"
                        )
                    per_sample_report_hashes[sample_id].add(report_hash)
    return errors


def _load_session_directory(
    session_dir: Path,
    assignments: dict[str, tuple[list[str], str, str]],
    context: dict[str, Any],
) -> dict[str, Any]:
    root = _secure_directory(session_dir, "isolated review session directory")
    actual_files = {entry.name for entry in root.iterdir() if entry.is_file()}
    actual_dirs = {entry.name for entry in root.iterdir() if entry.is_dir()}
    if actual_files != {"session.json"} or actual_dirs != {"reports"}:
        raise CalibrationValidationError(
            "Each session directory must contain exactly session.json and reports/"
        )
    reports_root = root / "reports"
    actual_report_files = {entry.name for entry in reports_root.iterdir() if entry.is_file()}
    report_names = {f"{sample_id}.report.json" for sample_id in assignments}
    review_names = {f"{sample_id}.review.json" for sample_id in assignments}
    if actual_report_files == report_names:
        report_suffix = ".report.json"
    elif actual_report_files == review_names:
        report_suffix = ".review.json"
    else:
        raise CalibrationValidationError(
            "Session reports/ must use one deterministic suffix and contain exactly one report per sample"
        )
    if any(entry.is_dir() for entry in reports_root.iterdir()):
        raise CalibrationValidationError("Session reports/ cannot contain subdirectories")
    session = load_strict_json(root / "session.json", "isolated session manifest")
    errors = _validate_session(session, assignments, context, f"session {root.name}")
    if errors:
        raise CalibrationValidationError("Invalid isolated session: " + "; ".join(errors))
    normalized_records = _session_records(session)
    if normalized_records is None:
        raise CalibrationValidationError(f"Unsupported isolated session shape: {root}")
    embedded = {record["sampleId"]: record["report"] for record in normalized_records}
    rich_by_id = {
        record.get("sampleId"): record
        for record in session.get("reports", [])
        if isinstance(record, dict)
    }
    for sample_id in assignments:
        if set(session) == RICH_SESSION_FIELDS:
            expected_relative = f"reports/{sample_id}{report_suffix}"
            if rich_by_id.get(sample_id, {}).get("reportFile") != expected_relative:
                raise CalibrationValidationError(
                    f"Rich session reportFile differs from the actual uniform suffix: {sample_id}"
                )
        report = load_strict_json(
            reports_root / f"{sample_id}{report_suffix}",
            "sealed visual QA report",
        )
        if report != embedded[sample_id]:
            raise CalibrationValidationError(
                f"Session report file and session.json embedded report differ: {sample_id}"
            )
    return session


def build_projected_review_corpus(
    manifest: dict[str, Any],
    bundle: dict[str, Any],
    context: dict[str, Any],
    session_directories: Iterable[Path],
    model_version_id: str,
    projection_receipt_hash: str,
    output: Path,
) -> dict[str, Any]:
    """Build one write-once v3 corpus without reading labels or answer data."""
    protocol_errors = validate_projected_protocol_bindings(manifest, bundle, context)
    if protocol_errors:
        raise CalibrationValidationError("Invalid projected protocol: " + "; ".join(protocol_errors))
    if _model_hash(model_version_id) != context["modelVersionHash"]:
        raise CalibrationValidationError("model_version_id does not match frozen review context")
    if not _is_hash(projection_receipt_hash):
        raise CalibrationValidationError("projection_receipt_hash must be a canonical sha256 identity")
    directories = list(session_directories)
    if len(directories) != 3:
        raise CalibrationValidationError("Exactly three isolated session directories are required")
    lexical = [Path(os.path.abspath(os.fspath(path))) for path in directories]
    if len(set(lexical)) != 3:
        raise CalibrationValidationError("Session directories must be distinct")
    _reject_output_within(output, lexical)
    assignments = _manifest_assignments(manifest)
    sessions = [_load_session_directory(path, assignments, context) for path in lexical]
    session_ids = [session["sessionId"] for session in sessions]
    reviewer_ids = [session["reviewerSessionId"] for session in sessions]
    session_hashes = [session["sessionHash"] for session in sessions]
    if len(set(session_ids)) != 3 or len(set(reviewer_ids)) != 3 or len(set(session_hashes)) != 3:
        raise CalibrationValidationError("Sessions must have distinct session/reviewer/hash identities")
    corpus = copy.deepcopy(
        {
            "schemaVersion": PROJECTED_CORPUS_SCHEMA,
            "projectionReceiptHash": projection_receipt_hash,
            "blindManifestHash": manifest["manifestHash"],
            "evidenceBundleHash": bundle["bundleHash"],
            "reviewContextHash": context["contextHash"],
            "reviewContractHash": context["reviewContractHash"],
            "captureProfilePolicyHash": context["captureProfilePolicyHash"],
            "modelVersionHash": context["modelVersionHash"],
            "sessionProtocols": [
                {
                    "sessionId": session["sessionId"],
                    "schemaVersion": _session_schema_version(session),
                    "sessionHash": session["sessionHash"],
                }
                for session in sessions
            ],
            "sessions": sessions,
        }
    )
    corpus["corpusHash"] = normalized_sha256(corpus, ("corpusHash",))
    errors = validate_projected_corpus(corpus, manifest, bundle, context, projection_receipt_hash)
    if errors:
        raise CalibrationValidationError("Internal projected corpus validation failed: " + "; ".join(errors))
    _write_json_new(output, corpus)
    return corpus


def build_projected_review_corpus_from_artifacts(
    artifacts_root: Path,
    session_directories: Iterable[Path],
    model_version_id: str,
    output: Path,
) -> dict[str, Any]:
    root = _secure_directory(artifacts_root, "projected artifacts root")
    directories = list(session_directories)
    _reject_output_within(output, (root, *directories))
    receipt, manifest, bundle, context = _load_verified_projected_protocol(root)
    return build_projected_review_corpus(
        manifest,
        bundle,
        context,
        directories,
        model_version_id,
        receipt["receiptHash"],
        output,
    )


def _load_verified_projected_protocol(
    artifacts_root: Path,
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any], dict[str, Any]]:
    """Load only the canonical projected documents after full receipt verification."""
    try:
        from .s0a_projection import verify_projection
    except ImportError:  # pragma: no cover
        from s0a_projection import verify_projection

    root = _secure_directory(artifacts_root, "projected artifacts root")
    receipt = verify_projection(root)
    manifest = load_strict_json(
        root / "blind" / "blind-submission-manifest.json", "projected blind manifest"
    )
    bundle = load_strict_json(
        root / "operator" / "evidence-bundle.json", "projected evidence bundle"
    )
    context = load_strict_json(
        root / "blind" / "review-freeze-context.json", "projected review context"
    )
    expected_receipt_bindings = {
        "blindManifestHash": manifest.get("manifestHash"),
        "operatorEvidenceBundleHash": bundle.get("bundleHash"),
        "reviewContextHash": context.get("contextHash"),
    }
    if any(receipt.get(field) != value for field, value in expected_receipt_bindings.items()):
        raise CalibrationValidationError(
            "Loaded projected protocol documents differ from the verified projection receipt"
        )
    protocol_errors = validate_projected_protocol_bindings(manifest, bundle, context)
    if protocol_errors:
        raise CalibrationValidationError(
            "Verified projection contains an invalid projected protocol: "
            + "; ".join(protocol_errors)
        )
    final_receipt = verify_projection(root)
    if final_receipt != receipt:
        raise CalibrationValidationError(
            "Projection receipt/artifact identity changed during verified protocol load"
        )
    return receipt, manifest, bundle, context


def validate_projected_frozen_bindings(
    labels: Any,
    manifest: Any,
    bundle: Any,
    context: Any,
    operator_ledger: Any,
    reduced_metrics: Any | None = None,
) -> list[str]:
    errors = [
        *verify_labels(labels),
        *validate_projected_protocol_bindings(manifest, bundle, context),
        *validate_operator_ledger(operator_ledger),
    ]
    if errors or not all(isinstance(item, dict) for item in (labels, manifest, bundle, context, operator_ledger)):
        return errors
    cohort = labels.get("holdoutCohort")
    if any(item.get("holdoutCohort") != cohort for item in (manifest, bundle, context, operator_ledger)):
        errors.append("projected scoring inputs do not share one holdout cohort")
    if context.get("operatorLedgerHash") != operator_ledger.get("ledgerHash"):
        errors.append("projected context does not bind supplied operator ledger")
    assignments = _manifest_assignments(manifest)
    labels_by_id = {
        sample["sampleId"]: sample
        for sample in labels.get("samples", [])
        if isinstance(sample, dict) and isinstance(sample.get("sampleId"), str)
    }
    ledger_ids = {
        sample["sampleId"]
        for sample in operator_ledger.get("samples", [])
        if isinstance(sample, dict) and isinstance(sample.get("sampleId"), str)
    }
    if set(assignments) != set(labels_by_id) or set(assignments) != ledger_ids:
        errors.append("labels, projected evidence, and ledger sample sets differ")
    else:
        for sample_id, (requirement_ids, evidence_hash, _profile_hash) in assignments.items():
            label = labels_by_id[sample_id]
            label_requirements = [item.get("requirementId") for item in label.get("perRequirement", []) if isinstance(item, dict)]
            if label.get("requirementId") not in requirement_ids or label_requirements != requirement_ids:
                errors.append(f"{sample_id}: labels do not match projected blind requirement assignment")
            if label.get("evidenceHash") != evidence_hash:
                errors.append(f"{sample_id}: labels do not bind projected evidenceHash")
            derivation = _bundle_samples(bundle)[sample_id].get("selectedEvidenceDerivationHash")
            is_invalid = label.get("groundTruthRoute") == "EVIDENCE_INVALID"
            if (derivation is not None) != is_invalid:
                errors.append(
                    f"{sample_id}: selected evidence derivation and EVIDENCE_INVALID ground truth differ"
                )
    mode = context.get("holdoutIsolationMode")
    if cohort == COHORT_IDS["full"] and mode == "EXPANDED_AFTER_REDUCED_ZERO_FALSE_PASS":
        if not isinstance(reduced_metrics, dict):
            errors.append("expanded projected full holdout requires reduced metrics")
        else:
            reduced_errors = verify_projected_metrics(reduced_metrics)
            errors.extend(f"reduced metrics: {error}" for error in reduced_errors)
            per = reduced_metrics.get("perRequirementMetrics")
            if (
                reduced_metrics.get("reportHash") != context.get("reducedMetricsHash")
                or reduced_metrics.get("holdoutCohort") != COHORT_IDS["reduced"]
                or not isinstance(per, dict)
                or per.get("falsePassCount") != 0
            ):
                errors.append("expanded projected full holdout lacks zero-false-pass reduced provenance")
    elif reduced_metrics is not None:
        errors.append("reduced metrics may accompany only expanded projected full scoring")
    return errors


def _flatten_reviews(corpus: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        {"sampleId": record["sampleId"], "sessionId": session["sessionId"], "report": record["report"]}
        for session in corpus["sessions"]
        for record in (_session_records(session) or [])
    ]


def _count_cohort(labels: list[dict[str, Any]]) -> tuple[dict[str, int], list[str]]:
    counts: Counter[str] = Counter({"fail": 0, "pass": 0, "uncertain": 0, "invalid": 0})
    uncounted: list[str] = []
    for sample in labels:
        route = sample["groundTruthRoute"]
        visible = sample["visuallyObservable"]
        eligible = sample["eligibleForVisualMetrics"]
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


def _ledger_counts(ledger: dict[str, Any]) -> dict[str, int]:
    counts: Counter[str] = Counter({"fail": 0, "pass": 0, "uncertain": 0, "invalid": 0})
    for sample in ledger["samples"]:
        if sample.get("kind") in counts:
            counts[sample["kind"]] += 1
    return dict(counts)


def _agreement(flat: list[dict[str, Any]], labels: list[dict[str, Any]]) -> tuple[float, float]:
    route_groups: dict[str, list[str]] = defaultdict(list)
    requirement_groups: dict[tuple[str, str], list[str]] = defaultdict(list)
    for review in flat:
        route_groups[review["sampleId"]].append(review["report"]["topLevelRoute"])
        for result in review["report"]["perRequirement"]:
            requirement_groups[(review["sampleId"], result["designRequirementId"])].append(result["state"])
    sample_ids = {sample["sampleId"] for sample in labels}
    requirements = [
        (sample["sampleId"], item["requirementId"])
        for sample in labels
        for item in sample["perRequirement"]
    ]
    route_agreement = sum(len(values) == 3 and len(set(values)) == 1 for values in route_groups.values()) / len(sample_ids)
    requirement_agreement = sum(
        len(requirement_groups[key]) == 3 and len(set(requirement_groups[key])) == 1
        for key in requirements
    ) / len(requirements)
    return requirement_agreement, route_agreement


def calculate_projected_metrics(
    labels: Any,
    corpus: Any,
    manifest: Any,
    bundle: Any,
    context: Any,
    operator_ledger: Any,
    projection_receipt_hash: str,
    reduced_metrics: Any | None = None,
) -> dict[str, Any]:
    binding_errors = validate_projected_frozen_bindings(
        labels,
        manifest,
        bundle,
        context,
        operator_ledger,
        reduced_metrics,
    )
    review_errors = validate_projected_corpus(
        corpus, manifest, bundle, context, projection_receipt_hash
    )
    if binding_errors or review_errors:
        raise CalibrationValidationError(
            "Invalid projected S0a score inputs: " + "; ".join([*binding_errors, *review_errors])
        )
    label_samples = labels["samples"]
    labels_by_id = {sample["sampleId"]: sample for sample in label_samples}
    flat = _flatten_reviews(corpus)
    by_type: dict[str, dict[str, Counter[str]]] = defaultdict(lambda: defaultdict(Counter))
    top: dict[str, Counter[str]] = {route: Counter() for route in sorted(REVIEW_ROUTES_LOCAL)}
    known_fail = false_pass = known_pass = false_fail = 0
    non_boundary = non_boundary_uncertain = expected_invalid = detected_invalid = 0
    for review in flat:
        label = labels_by_id[review["sampleId"]]
        report = review["report"]
        expected_route = label["groundTruthRoute"]
        predicted_route = report["topLevelRoute"]
        top[expected_route][predicted_route] += 1
        if expected_route == "EVIDENCE_INVALID":
            expected_invalid += 1
            detected_invalid += int(predicted_route == "EVIDENCE_INVALID")
        kind = "visual" if label["eligibleForVisualMetrics"] else "behavioral_or_structural"
        expected_states = {item["requirementId"]: item["state"] for item in label["perRequirement"]}
        for result in report["perRequirement"]:
            expected_state = expected_states[result["designRequirementId"]]
            predicted_state = result["state"]
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
    requirement_agreement, route_agreement = _agreement(flat, label_samples)
    counts, uncounted = _count_cohort(label_samples)
    ledger_counts = _ledger_counts(operator_ledger)
    exact_full = (
        labels["holdoutCohort"] == COHORT_IDS["full"]
        and len(label_samples) == 110
        and counts == COHORTS["full"]
        and ledger_counts == COHORTS["full"]
        and not uncounted
        and all(sample["groundTruthRoute"] != "CONTRACT_AMBIGUOUS" for sample in label_samples)
    )
    thresholds = (
        false_pass == 0
        and false_fail_rate <= 0.10
        and uncertain_rate <= 0.15
        and invalid_recall == 1.0
        and requirement_agreement >= 0.90
        and route_agreement >= 0.90
    )
    legacy_body = {
        "holdoutCohort": labels["holdoutCohort"],
        "labelManifestHash": labels["manifestHash"],
        "perRequirementMetrics": {
            "falsePassCount": false_pass,
            "knownVisualFailCount": known_fail,
            "falseFailRate": false_fail_rate,
            "nonBoundaryUncertainRate": uncertain_rate,
            "confusionMatricesByRequirementType": {
                kind: {requirement: dict(matrix) for requirement, matrix in requirements.items()}
                for kind, requirements in by_type.items()
            },
            "qualificationPreconditions": {
                "exactFullHoldout": exact_full,
                "labelCount": len(label_samples),
                "cohortCounts": counts,
                "uncountedSamples": uncounted,
            },
        },
        "topRouteMetrics": {
            "evidenceInvalidRecall": invalid_recall,
            "evidenceInvalidDetected": detected_invalid,
            "evidenceInvalidExpected": expected_invalid,
            "confusionMatrix": {route: dict(top[route]) for route in sorted(REVIEW_ROUTES_LOCAL)},
        },
        "stability": {
            "isolatedSessionCount": 3,
            "perRequirementAgreement": requirement_agreement,
            "topRouteAgreement": route_agreement,
        },
        "terminalStatus": "S0A_GATE_QUALIFIED" if exact_full and thresholds else "S0A_ADVISORY_ONLY",
    }
    legacy_body["reportHash"] = normalized_sha256(legacy_body, ("reportHash",))
    metric_errors = verify_metrics(legacy_body, labels)
    if metric_errors:
        raise CalibrationValidationError("Internal projected metrics validation failed: " + "; ".join(metric_errors))
    report = {
        "schemaVersion": PROJECTED_METRICS_SCHEMA,
        "scorerVersion": PROJECTED_SCORER_VERSION,
        "scorerSourceHash": _projected_scorer_source_hash(),
        "projectionReceiptHash": projection_receipt_hash,
        "reviewCorpusHash": corpus["corpusHash"],
        **{key: value for key, value in legacy_body.items() if key != "reportHash"},
    }
    report["reportHash"] = normalized_sha256(report, ("reportHash",))
    projected_metric_errors = verify_projected_metrics(report, labels)
    if projected_metric_errors:
        raise CalibrationValidationError(
            "Internal projected metrics v3 validation failed: " + "; ".join(projected_metric_errors)
        )
    return report


def verify_projected_metrics(
    report: Any,
    labels_manifest: Any | None = None,
) -> list[str]:
    errors: list[str] = []
    if not _exact(report, PROJECTED_METRICS_FIELDS, "projected metrics", errors):
        return errors
    if report.get("schemaVersion") != PROJECTED_METRICS_SCHEMA:
        errors.append("projected metrics schemaVersion is invalid")
    if report.get("scorerVersion") != PROJECTED_SCORER_VERSION:
        errors.append("projected metrics scorerVersion is invalid")
    try:
        current_source_hash = _projected_scorer_source_hash()
    except CalibrationValidationError as exc:
        errors.append(str(exc))
        current_source_hash = None
    if report.get("scorerSourceHash") != current_source_hash:
        errors.append("projected metrics scorerSourceHash differs from current frozen scorer sources")
    for field in ("projectionReceiptHash", "reviewCorpusHash"):
        if not _is_hash(report.get(field)):
            errors.append(f"projected metrics {field} is invalid")
    if not _hash_matches(report, "reportHash"):
        errors.append("projected metrics reportHash does not match canonical content")
    legacy = {
        key: copy.deepcopy(report.get(key))
        for key in (
            "holdoutCohort",
            "labelManifestHash",
            "perRequirementMetrics",
            "topRouteMetrics",
            "stability",
            "terminalStatus",
        )
    }
    legacy["reportHash"] = normalized_sha256(legacy, ("reportHash",))
    errors.extend(f"projected metrics body: {error}" for error in verify_metrics(legacy, labels_manifest))
    return errors


def calculate_compatible_metrics(
    labels: Any,
    corpus: Any,
    blind_manifest: Any,
    evidence_bundle: Any,
    review_context: Any,
    operator_ledger: Any,
    reduced_metrics: Any | None = None,
    projection_receipt_hash: str | None = None,
) -> dict[str, Any]:
    """Dispatch one whole protocol family; mixed legacy/projected inputs fail."""
    schemas = (
        blind_manifest.get("schemaVersion") if isinstance(blind_manifest, dict) else None,
        evidence_bundle.get("schemaVersion") if isinstance(evidence_bundle, dict) else None,
        review_context.get("schemaVersion") if isinstance(review_context, dict) else None,
        corpus.get("schemaVersion") if isinstance(corpus, dict) else None,
    )
    projected = (
        PROJECTED_BLIND_SCHEMA,
        PROJECTED_BUNDLE_SCHEMA,
        PROJECTED_CONTEXT_SCHEMA,
        PROJECTED_CORPUS_SCHEMA,
    )
    legacy = (
        BLIND_SCHEMA_VERSION,
        BUNDLE_SCHEMA_VERSION,
        CONTEXT_SCHEMA_VERSION,
        REVIEW_CORPUS_SCHEMA_VERSION,
    )
    if schemas == projected:
        if not _is_hash(projection_receipt_hash):
            raise CalibrationValidationError(
                "Projected scoring requires the verified projection receipt hash"
            )
        return calculate_projected_metrics(
            labels,
            corpus,
            blind_manifest,
            evidence_bundle,
            review_context,
            operator_ledger,
            projection_receipt_hash,
            reduced_metrics,
        )
    if schemas == legacy:
        return calculate_legacy_metrics(
            labels,
            corpus,
            blind_manifest,
            evidence_bundle,
            review_context,
            operator_ledger,
            reduced_metrics,
        )
    raise CalibrationValidationError(
        "Mixed or unsupported S0a protocol families are forbidden; supply either the complete legacy or complete projected set"
    )


def calculate_projected_metrics_from_artifacts(
    artifacts_root: Path,
    labels: Any,
    corpus: Any,
    operator_ledger: Any,
    reduced_metrics: Any | None = None,
) -> dict[str, Any]:
    """Formal projected score entry: callers cannot replace manifest/context/bundle."""
    receipt, manifest, bundle, context = _load_verified_projected_protocol(artifacts_root)
    return calculate_projected_metrics(
        labels,
        corpus,
        manifest,
        bundle,
        context,
        operator_ledger,
        receipt["receiptHash"],
        reduced_metrics,
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    build = commands.add_parser("build-corpus", help="build one write-once projected v3 review corpus")
    build.add_argument("--artifacts", type=Path, required=True, help="verified projected artifacts root")
    build.add_argument("--session-dir", type=Path, action="append", required=True, help="repeat exactly three times")
    build.add_argument("--model-version-id", required=True)
    build.add_argument("--output", type=Path, required=True)
    projected = commands.add_parser(
        "score-projected",
        help="score a verified projected root; manifest/context/bundle cannot be supplied separately",
    )
    projected.add_argument("--artifacts", type=Path, required=True)
    projected.add_argument("--labels", type=Path, required=True)
    projected.add_argument("--reviews", type=Path, required=True)
    projected.add_argument("--operator-ledger", type=Path, required=True)
    projected.add_argument("--reduced-metrics", type=Path)
    projected.add_argument("--output", type=Path, required=True)
    legacy = commands.add_parser(
        "score-legacy", help="explicit legacy-only compatibility entry"
    )
    legacy.add_argument("--labels", type=Path, required=True)
    legacy.add_argument("--reviews", type=Path, required=True)
    legacy.add_argument("--blind-manifest", type=Path, required=True)
    legacy.add_argument("--evidence-bundle", type=Path, required=True)
    legacy.add_argument("--review-context", type=Path, required=True)
    legacy.add_argument("--operator-ledger", type=Path, required=True)
    legacy.add_argument("--reduced-metrics", type=Path)
    legacy.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "build-corpus":
            result = build_projected_review_corpus_from_artifacts(
                args.artifacts,
                args.session_dir,
                args.model_version_id,
                args.output,
            )
        elif args.command == "score-projected":
            _reject_output_within(args.output, (args.artifacts,))
            result = calculate_projected_metrics_from_artifacts(
                args.artifacts,
                load_strict_json(args.labels, "frozen labels"),
                load_strict_json(args.reviews, "review corpus"),
                load_strict_json(args.operator_ledger, "operator ledger"),
                load_strict_json(args.reduced_metrics, "reduced metrics") if args.reduced_metrics else None,
            )
            _write_json_new(args.output, result)
        else:
            result = calculate_legacy_metrics(
                load_strict_json(args.labels, "legacy frozen labels"),
                load_strict_json(args.reviews, "legacy review corpus"),
                load_strict_json(args.blind_manifest, "legacy blind manifest"),
                load_strict_json(args.evidence_bundle, "legacy evidence bundle"),
                load_strict_json(args.review_context, "legacy review context"),
                load_strict_json(args.operator_ledger, "legacy operator ledger"),
                load_strict_json(args.reduced_metrics, "legacy reduced metrics") if args.reduced_metrics else None,
            )
            _write_json_new(args.output, result)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except CalibrationValidationError as exc:
        parser.error(str(exc))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
