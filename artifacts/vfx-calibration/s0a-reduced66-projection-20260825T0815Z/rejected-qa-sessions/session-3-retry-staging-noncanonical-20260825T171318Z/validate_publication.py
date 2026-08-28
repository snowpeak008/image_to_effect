#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

from jsonschema import Draft202012Validator
from referencing import Registry, Resource
from referencing.jsonschema import DRAFT202012


EXPECTED_REVIEWER_SESSION_ID = "s0a-reduced66-visual-qa-session-3-retry-20260825T1711+0800"
EXPECTED_REVIEW_VERSION = "s0a-visual-qa/1.0"
EXPECTED_MODEL_ID = "gpt-5.6-sol"
EXPECTED_MODEL_HASH = "sha256:284423d514dacec2a236d44fa2ac96d3b595bcec83e06785d85b12731e537dce"
EXPECTED_POLICY_HASH = "sha256:1d9ee0b874721a3eb1e45e1d347744f316bbd0f6b407184afcb37bb9abae18db"
EXPECTED_FRAMES = [1, 21, 60, 120, 180, 240, 300, 360]
EXPECTED_SLOTS = [(seed, frame) for seed in range(3) for frame in EXPECTED_FRAMES]
ROUTES = ["VISUAL_PASS", "VISUAL_FAIL", "EVIDENCE_INVALID", "CONTRACT_AMBIGUOUS", "VISUAL_UNCERTAIN"]


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def canonical_bytes(document: dict, omitted_field: str | None = None) -> bytes:
    material = dict(document)
    if omitted_field is not None:
        material.pop(omitted_field, None)
    return json.dumps(material, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def canonical_hash(document: dict, omitted_field: str) -> str:
    return "sha256:" + hashlib.sha256(canonical_bytes(document, omitted_field)).hexdigest()


def validate_or_add(errors: list[str], validator: Draft202012Validator, document: dict, prefix: str) -> None:
    for error in validator.iter_errors(document):
        errors.append(f"{prefix} {error.json_path}: {error.message}")


def contains_property(document: object, property_name: str) -> bool:
    if isinstance(document, dict):
        return property_name in document or any(contains_property(value, property_name) for value in document.values())
    if isinstance(document, list):
        return any(contains_property(value, property_name) for value in document)
    return False


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--publication", type=Path, required=True)
    parser.add_argument("--blind-root", type=Path, required=True)
    parser.add_argument("--preflight", type=Path, required=True)
    parser.add_argument("--schema-root", type=Path, required=True)
    parser.add_argument("--result", type=Path, required=True)
    args = parser.parse_args()

    publication = args.publication.resolve(strict=True)
    blind = args.blind_root.resolve(strict=True)
    schema_root = args.schema_root.resolve(strict=True)
    preflight = load_json(args.preflight)
    freeze = load_json(blind / "review-freeze-context.json")
    contract = load_json(blind / "review-contract.json")
    manifest = load_json(blind / "blind-submission-manifest.json")
    session_path = publication / "session.json"
    session = load_json(session_path)
    review_schema = load_json(schema_root / "s0a-projected-visual-review.schema.json")
    session_schema = load_json(schema_root / "s0a-isolated-review-session.schema.json")
    registry = Registry().with_resource(review_schema["$id"], Resource.from_contents(review_schema, default_specification=DRAFT202012))
    report_validator = Draft202012Validator(review_schema)
    session_validator = Draft202012Validator(session_schema, registry=registry)
    errors: list[str] = []

    validate_or_add(errors, session_validator, session, "session schema")
    if session_path.read_bytes() != canonical_bytes(session):
        errors.append("session.json is not exact canonical compact UTF-8 bytes")
    if session.get("sessionHash") != canonical_hash(session, "sessionHash"):
        errors.append("sessionHash mismatch")
    if session.get("sessionId") != EXPECTED_REVIEWER_SESSION_ID or session.get("reviewerSessionId") != EXPECTED_REVIEWER_SESSION_ID:
        errors.append("reviewer session identity mismatch")
    if session.get("reviewVersion") != EXPECTED_REVIEW_VERSION:
        errors.append("reviewVersion mismatch")
    if session.get("captureProfileHash") != EXPECTED_POLICY_HASH:
        errors.append("session captureProfileHash is not the cohort policy hash")
    if session.get("contractHash") != contract.get("contractHash") or session.get("buildHash") != freeze.get("buildHash"):
        errors.append("session contract/build identity mismatch")
    if session.get("candidateId") != "C0":
        errors.append("session candidateId mismatch")
    if session.get("qaGateAuthority") != "advisory-only" or session.get("s0aTerminalStatus") is not None:
        errors.append("session grants terminal or non-advisory authority")
    validation_material = "\n".join(session.get("validationChecks", []))
    if EXPECTED_MODEL_ID not in validation_material or EXPECTED_MODEL_HASH not in validation_material:
        errors.append("modelVersionId/frozen model hash not recorded in validation checks")
    if freeze.get("modelVersionHash") != EXPECTED_MODEL_HASH:
        errors.append("freeze model hash mismatch")
    if preflight.get("globalValid") is not True or preflight.get("sampleCount") != 66:
        errors.append("blind preflight global validity/sample count mismatch")

    manifest_by_id = {entry["sampleId"]: entry for entry in manifest["samples"]}
    assignment_by_id = {entry["sampleId"]: entry for entry in contract["assignments"]}
    preflight_by_id = {entry["sampleId"]: entry for entry in preflight["samples"]}
    expected_ids = set(manifest_by_id)
    session_records = session.get("reports", [])
    record_ids = [record.get("sampleId") for record in session_records]
    disk_report_paths = sorted((publication / "reports").glob("*.report.json"))
    disk_ids = {path.name.removesuffix(".report.json") for path in disk_report_paths}
    if len(expected_ids) != 66 or len(record_ids) != 66 or len(set(record_ids)) != 66 or len(disk_report_paths) != 66:
        errors.append("66/66 uniqueness/count check failed")
    if expected_ids != set(assignment_by_id) or expected_ids != set(preflight_by_id) or expected_ids != set(record_ids) or expected_ids != disk_ids:
        errors.append("sample sets differ across blind inputs, disk reports, or session records")

    policy_values: list[str] = []
    observed_routes: Counter[str] = Counter()
    embedded_canonical_bytes_match_count = 0
    reviewed_slot_count = 0
    instance_hash_verified_count = 0
    report_hash_match_count = 0
    schema_valid_report_count = 0
    advisory_report_count = 0

    for record in session_records:
        sample_id = record["sampleId"]
        expected_entry = manifest_by_id.get(sample_id)
        expected_assignment = assignment_by_id.get(sample_id)
        expected_preflight = preflight_by_id.get(sample_id)
        if not expected_entry or not expected_assignment or not expected_preflight:
            continue
        expected_name = f"{sample_id}.report.json"
        expected_relative = f"reports/{expected_name}"
        if record.get("reportFile") != expected_relative:
            errors.append(f"{sample_id}: reportFile mismatch")
            continue
        report_path = publication / "reports" / expected_name
        try:
            disk_report = load_json(report_path)
        except Exception as exc:
            errors.append(f"{sample_id}: cannot load disk report: {exc}")
            continue
        embedded_report = record.get("report")
        report_errors_before = len(errors)
        validate_or_add(errors, report_validator, disk_report, f"{sample_id} report schema")
        if len(errors) == report_errors_before:
            schema_valid_report_count += 1
        if report_path.read_bytes() != canonical_bytes(disk_report):
            errors.append(f"{sample_id}: disk report is not exact canonical compact UTF-8 bytes")
        if disk_report != embedded_report:
            errors.append(f"{sample_id}: disk/embedded report semantic mismatch")
        if report_path.read_bytes() == canonical_bytes(embedded_report):
            embedded_canonical_bytes_match_count += 1
        else:
            errors.append(f"{sample_id}: disk/embedded canonical byte mismatch")
        if disk_report.get("sealedReportHash") == canonical_hash(disk_report, "sealedReportHash"):
            report_hash_match_count += 1
        else:
            errors.append(f"{sample_id}: sealedReportHash mismatch")

        expected_design_id = expected_entry["designRequirementIds"][0]
        actual_design_ids = [item.get("designRequirementId") for item in disk_report.get("perRequirement", [])]
        identity_expectations = {
            "candidateId": "C0",
            "contractHash": contract["contractHash"],
            "buildHash": freeze["buildHash"],
            "captureProfileHash": EXPECTED_POLICY_HASH,
            "evidenceHashes": [expected_entry["evidenceHash"]],
            "reviewVersion": EXPECTED_REVIEW_VERSION,
        }
        for field, expected_value in identity_expectations.items():
            if disk_report.get(field) != expected_value:
                errors.append(f"{sample_id}: {field} mismatch")
        if actual_design_ids != [expected_design_id] or expected_assignment["designRequirementIds"] != [expected_design_id]:
            errors.append(f"{sample_id}: designRequirementId mismatch")
        policy_values.append(disk_report.get("captureProfileHash"))
        if disk_report.get("qaGateAuthority") == "advisory-only" and disk_report.get("s0aTerminalStatus") is None:
            advisory_report_count += 1
        else:
            errors.append(f"{sample_id}: terminal or ordinary gate authority present")
        observed_routes[disk_report.get("topLevelRoute")] += 1

        if record.get("evidenceHash") != expected_entry["evidenceHash"]:
            errors.append(f"{sample_id}: rich record evidenceHash mismatch")
        if record.get("imageReviewComplete") is not True or record.get("reviewedBeautySlotCount") != 24 or record.get("expectedBeautyFrameCount") != 24:
            errors.append(f"{sample_id}: image review completeness fields mismatch")
        if record.get("reviewedSeedOrdinals") != [0, 1, 2] or record.get("reviewedFrameNumbers") != EXPECTED_FRAMES:
            errors.append(f"{sample_id}: reviewed seed/frame declarations mismatch")
        reviewed_slots = record.get("reviewedBeautySlots", [])
        actual_slots = [(slot.get("seedOrdinal"), slot.get("frameNumber")) for slot in reviewed_slots]
        expected_availability = [slot["availability"] for slot in expected_preflight["slots"]]
        actual_availability = [slot.get("availability") for slot in reviewed_slots]
        if actual_slots != EXPECTED_SLOTS or actual_availability != expected_availability:
            errors.append(f"{sample_id}: 24-slot order or availability mismatch")
        else:
            reviewed_slot_count += len(reviewed_slots)
        if record.get("presentBeautyFrameCount") != expected_preflight["presentBeautyFrameCount"]:
            errors.append(f"{sample_id}: presentBeautyFrameCount mismatch")
        if expected_preflight.get("captureProfileInstanceHashVerified") is True:
            instance_hash_verified_count += 1
        else:
            errors.append(f"{sample_id}: blind captureProfileInstanceHash was not verified")
        expected_route = "EVIDENCE_INVALID" if not expected_preflight["evidenceValid"] else disk_report.get("topLevelRoute")
        if not expected_preflight["evidenceValid"] and disk_report.get("topLevelRoute") != expected_route:
            errors.append(f"{sample_id}: invalid evidence not routed EVIDENCE_INVALID")

    calculated_route_counts = {route: observed_routes[route] for route in ROUTES}
    if session.get("routeCounts") != calculated_route_counts or sum(calculated_route_counts.values()) != 66:
        errors.append("routeCounts mismatch or do not sum to 66")
    if len(set(policy_values)) != 1 or policy_values.count(EXPECTED_POLICY_HASH) != 66:
        errors.append("report captureProfileHash policy invariant failed")
    publication_bytes = session_path.read_bytes() + b"".join(path.read_bytes() for path in disk_report_paths)
    forbidden_authority_tokens = [b"ordinary-l3-gate", b"S0A_GATE_QUALIFIED", b'"s0aTerminalStatus":"S0A_ADVISORY_ONLY"', b'"qaGateAuthority":"ordinary-l3-gate"']
    if any(token in publication_bytes for token in forbidden_authority_tokens):
        errors.append("publication contains terminal or ordinary QA authority")
    if contains_property(session, "captureProfileInstanceHash"):
        errors.append("captureProfileInstanceHash property leaked into publication")

    checks = {
        "exactSampleCount": len(expected_ids),
        "diskReportCount": len(disk_report_paths),
        "schemaValidReportCount": schema_valid_report_count,
        "sealedReportHashMatchCount": report_hash_match_count,
        "embeddedCanonicalByteAndSemanticMatchCount": embedded_canonical_bytes_match_count,
        "reviewedBeautySlotCount": reviewed_slot_count,
        "captureProfileInstanceHashVerifiedCount": instance_hash_verified_count,
        "captureProfilePolicyHashMatchCount": policy_values.count(EXPECTED_POLICY_HASH),
        "captureProfilePolicyHashDistinctCount": len(set(policy_values)),
        "advisoryNullTerminalReportCount": advisory_report_count,
        "routeCounts": calculated_route_counts,
        "sessionHash": session.get("sessionHash"),
    }
    result = {
        "validationStatus": "PASS" if not errors else "FAIL",
        "errors": errors,
        "checks": checks,
    }
    args.result.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
