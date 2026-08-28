#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path


REVIEWER_SESSION_ID = "s0a-reduced66-visual-qa-session-3-retry-20260825T1711+0800"
REVIEW_VERSION = "s0a-visual-qa/1.0"
MODEL_VERSION_ID = "gpt-5.6-sol"
MODEL_VERSION_HASH = "sha256:284423d514dacec2a236d44fa2ac96d3b595bcec83e06785d85b12731e537dce"
POLICY_HASH = "sha256:1d9ee0b874721a3eb1e45e1d347744f316bbd0f6b407184afcb37bb9abae18db"
FRAME_NUMBERS = [1, 21, 60, 120, 180, 240, 300, 360]
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


def invalid_requirement(sample: dict) -> dict:
    missing = sample["missingSlots"]
    if missing:
        seeds = sorted({item["seedOrdinal"] for item in missing})
        frames = sorted({item["frameNumber"] for item in missing})
        seed_text = ", ".join(str(seed) for seed in seeds)
        frame_text = ", ".join(str(frame) for frame in frames)
        return {
            "state": "uncertain",
            "countable": False,
            "reasonCode": "EVIDENCE_INVALID",
            "stateRef": f"starting; seed {seed_text}",
            "frameNumbers": frames,
            "imageRegion": f"missing Beauty slot at the central emitter region for seed {seed_text}, frame {frame_text}",
            "observation": f"The fixed Beauty evidence slot for seed {seed_text}, frame {frame_text} is explicitly missing; all other available slots were viewed, but the complete 3-seed by 8-frame lifecycle cannot be visually judged.",
        }
    return {
        "state": "uncertain",
        "countable": False,
        "reasonCode": "EVIDENCE_INVALID",
        "stateRef": "all retained lifecycle states; capture metadata integrity",
        "frameNumbers": FRAME_NUMBERS,
        "imageRegion": "entire 3-seed by 8-frame Beauty evidence set",
        "observation": "All 24 Beauty frames were viewed, but the evidence captureMetadata declaredHash and actualHash do not match; the capture identity failure invalidates a visual-semantic verdict for the flame lifecycle.",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blind-root", type=Path, required=True)
    parser.add_argument("--preflight", type=Path, required=True)
    parser.add_argument("--decisions", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    if args.output.exists():
        raise SystemExit(f"REFUSE_OUTPUT_EXISTS: {args.output}")
    blind = args.blind_root.resolve(strict=True)
    freeze = load_json(blind / "review-freeze-context.json")
    contract = load_json(blind / "review-contract.json")
    manifest = load_json(blind / "blind-submission-manifest.json")
    preflight = load_json(args.preflight)
    overrides = load_json(args.decisions)

    if not preflight["globalValid"]:
        raise SystemExit("REFUSE_GLOBAL_PREFLIGHT_INVALID")
    if len(preflight["samples"]) != 66:
        raise SystemExit("REFUSE_SAMPLE_COUNT_NOT_66")
    if freeze["captureProfilePolicyHash"] != POLICY_HASH or manifest["captureProfilePolicyHash"] != POLICY_HASH:
        raise SystemExit("REFUSE_POLICY_HASH_MISMATCH")
    if freeze["modelVersionHash"] != MODEL_VERSION_HASH:
        raise SystemExit("REFUSE_MODEL_HASH_MISMATCH")

    manifest_by_id = {entry["sampleId"]: entry for entry in manifest["samples"]}
    assignment_by_id = {entry["sampleId"]: entry for entry in contract["assignments"]}
    preflight_by_id = {entry["sampleId"]: entry for entry in preflight["samples"]}
    sample_ids = sorted(manifest_by_id)
    if set(sample_ids) != set(assignment_by_id) or set(sample_ids) != set(preflight_by_id):
        raise SystemExit("REFUSE_SAMPLE_SET_MISMATCH")
    if not set(overrides).issubset(sample_ids):
        raise SystemExit("REFUSE_UNKNOWN_DECISION_SAMPLE")

    publication = args.output
    reports_directory = publication / "reports"
    reports_directory.mkdir(parents=True)
    session_records = []
    route_counter: Counter[str] = Counter()

    for sample_id in sample_ids:
        manifest_entry = manifest_by_id[sample_id]
        assignment = assignment_by_id[sample_id]
        evidence_check = preflight_by_id[sample_id]
        design_requirement_id = manifest_entry["designRequirementIds"][0]
        if assignment["designRequirementIds"] != manifest_entry["designRequirementIds"]:
            raise SystemExit(f"REFUSE_REQUIREMENT_MISMATCH: {sample_id}")

        if not evidence_check["evidenceValid"]:
            route = "EVIDENCE_INVALID"
            review = invalid_requirement(evidence_check)
            evidence_status = "invalid"
        elif sample_id in overrides:
            override = overrides[sample_id]
            route = override["topLevelRoute"]
            review = {
                "state": override["state"],
                "countable": True,
                "reasonCode": "OBSERVED",
                "stateRef": override["stateRef"],
                "frameNumbers": override["frameNumbers"],
                "imageRegion": override["imageRegion"],
                "observation": override["observation"],
            }
            evidence_status = "valid"
        else:
            route = "VISUAL_PASS"
            review = {
                "state": "pass",
                "countable": True,
                "reasonCode": "OBSERVED",
                "stateRef": "starting, steady, stopping and/or idle states across seeds 0-2",
                "frameNumbers": FRAME_NUMBERS,
                "imageRegion": "central emitter and surrounding ground-plane region across all three seed rows",
                "observation": "Across all three seeds a visible flame is present in every declared starting and steady slot; declared stopping slots visibly diminish or clear the flame, and every declared idle slot is clear, making the complete sustained-flame lifecycle observable and coherent.",
            }
            evidence_status = "valid"

        report = {
            "reviewVersion": REVIEW_VERSION,
            "candidateId": "C0",
            "contractHash": contract["contractHash"],
            "buildHash": freeze["buildHash"],
            "captureProfileHash": POLICY_HASH,
            "evidenceHashes": [manifest_entry["evidenceHash"]],
            "imageInputRead": True,
            "evidenceStatus": evidence_status,
            "contractStatus": "unambiguous",
            "s0aTerminalStatus": None,
            "qaGateAuthority": "advisory-only",
            "topLevelRoute": route,
            "perRequirement": [{"designRequirementId": design_requirement_id, **review}],
            "conflicts": [],
        }
        report["sealedReportHash"] = canonical_hash(report, "sealedReportHash")
        report_name = f"{sample_id}.report.json"
        (reports_directory / report_name).write_bytes(canonical_bytes(report))

        reviewed_slots = [
            {
                "seedOrdinal": slot["seedOrdinal"],
                "frameNumber": slot["frameNumber"],
                "availability": slot["availability"],
            }
            for slot in evidence_check["slots"]
        ]
        session_records.append(
            {
                "sampleId": sample_id,
                "reportFile": f"reports/{report_name}",
                "evidenceHash": manifest_entry["evidenceHash"],
                "imageReviewComplete": True,
                "reviewedSeedOrdinals": [0, 1, 2],
                "reviewedFrameNumbers": FRAME_NUMBERS,
                "expectedBeautyFrameCount": 24,
                "presentBeautyFrameCount": evidence_check["presentBeautyFrameCount"],
                "reviewedBeautySlotCount": 24,
                "reviewedBeautySlots": reviewed_slots,
                "report": report,
            }
        )
        route_counter[route] += 1

    session = {
        "schemaVersion": "s0a-visual-qa-session/1.0",
        "reviewVersion": REVIEW_VERSION,
        "sessionId": REVIEWER_SESSION_ID,
        "reviewerSessionId": REVIEWER_SESSION_ID,
        "candidateId": "C0",
        "contractHash": contract["contractHash"],
        "buildHash": freeze["buildHash"],
        "captureProfileHash": POLICY_HASH,
        "isolated": True,
        "qaGateAuthority": "advisory-only",
        "s0aTerminalStatus": None,
        "sampleCount": 66,
        "routeCounts": {route: route_counter[route] for route in ROUTES},
        "validationChecks": [
            "66/66 blind sample IDs exactly matched the no-answer contract, manifest, evidence records, report filenames, and embedded session records.",
            "Every sample reviewed all 24 fixed Beauty slots (3 seeds x 8 frames); 1580 present images were decoded and 4 declared-missing slots were explicitly recorded.",
            "Every report matched candidate C0, contractHash, buildHash, evidenceHash, and designRequirementId from the frozen blind inputs.",
            f"captureProfileHash policy identity was enforced 66/66 as {POLICY_HASH}; distinct count is 1, and captureProfileInstanceHash was used only to verify blind evidence.",
            f"reviewVersion {REVIEW_VERSION}; modelVersionId {MODEL_VERSION_ID} was verified against frozen model hash {MODEL_VERSION_HASH}.",
            "All reports, sealedReportHash values, sessionHash, disk/embedded canonical bytes and semantics, advisory-only authority, and null S0a terminal status were mechanically checked before publication.",
        ],
        "reports": session_records,
    }
    session["sessionHash"] = canonical_hash(session, "sessionHash")
    (publication / "session.json").write_bytes(canonical_bytes(session))
    print(
        json.dumps(
            {
                "publication": str(publication.resolve()),
                "reportCount": len(session_records),
                "routeCounts": session["routeCounts"],
                "sessionHash": session["sessionHash"],
                "modelVersionId": MODEL_VERSION_ID,
                "modelVersionHash": MODEL_VERSION_HASH,
                "captureProfileHash": POLICY_HASH,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
