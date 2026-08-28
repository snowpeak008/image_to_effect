#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath

from jsonschema import Draft202012Validator
from PIL import Image


EXPECTED_POLICY_HASH = "sha256:1d9ee0b874721a3eb1e45e1d347744f316bbd0f6b407184afcb37bb9abae18db"
EXPECTED_MODEL_HASH = "sha256:284423d514dacec2a236d44fa2ac96d3b595bcec83e06785d85b12731e537dce"
EXPECTED_FRAMES = [1, 21, 60, 120, 180, 240, 300, 360]
EXPECTED_SLOTS = [(seed, frame) for seed in range(3) for frame in EXPECTED_FRAMES]


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def canonical_hash(document: dict, omitted_field: str) -> str:
    material = dict(document)
    material.pop(omitted_field, None)
    encoded = json.dumps(material, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


def file_hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return "sha256:" + digest.hexdigest()


def schema_errors(document: dict, schema: dict) -> list[str]:
    return [f"{error.json_path}: {error.message}" for error in Draft202012Validator(schema).iter_errors(document)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blind-root", type=Path, required=True)
    parser.add_argument("--schema-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    blind = args.blind_root.resolve(strict=True)
    schema_root = args.schema_root.resolve(strict=True)
    freeze_path = blind / "review-freeze-context.json"
    contract_path = blind / "review-contract.json"
    manifest_path = blind / "blind-submission-manifest.json"
    freeze = load_json(freeze_path)
    contract = load_json(contract_path)
    manifest = load_json(manifest_path)

    schemas = {
        "freeze": load_json(schema_root / "s0a-projected-review-freeze-context.schema.json"),
        "contract": load_json(schema_root / "s0a-blind-review-contract.schema.json"),
        "manifest": load_json(schema_root / "s0a-projected-blind-manifest.schema.json"),
        "evidence": load_json(schema_root / "s0a-projected-blind-evidence.schema.json"),
    }

    global_errors: list[str] = []
    for name, document in (("freeze", freeze), ("contract", contract), ("manifest", manifest)):
        global_errors.extend(f"{name} schema: {error}" for error in schema_errors(document, schemas[name]))

    hash_checks = {
        "contextHash": canonical_hash(freeze, "contextHash") == freeze["contextHash"],
        "contractHash": canonical_hash(contract, "contractHash") == contract["contractHash"],
        "manifestHash": canonical_hash(manifest, "manifestHash") == manifest["manifestHash"],
    }
    for name, passed in hash_checks.items():
        if not passed:
            global_errors.append(f"canonical {name} mismatch")

    identity_checks = {
        "answerDisclosureNONE": contract.get("answerDisclosure") == "NONE",
        "freezeContractHashMatches": freeze.get("reviewContractHash") == contract.get("contractHash"),
        "manifestContractHashMatches": manifest.get("reviewContractHash") == contract.get("contractHash"),
        "manifestContextHashMatches": manifest.get("reviewContextHash") == freeze.get("contextHash"),
        "freezePolicyHashMatchesExpected": freeze.get("captureProfilePolicyHash") == EXPECTED_POLICY_HASH,
        "manifestPolicyHashMatchesExpected": manifest.get("captureProfilePolicyHash") == EXPECTED_POLICY_HASH,
        "modelVersionHashMatchesExpected": freeze.get("modelVersionHash") == EXPECTED_MODEL_HASH,
        "contractManifestSampleSetsMatch": {x["sampleId"] for x in contract["assignments"]} == {x["sampleId"] for x in manifest["samples"]},
        "contractSampleIdsUnique": len({x["sampleId"] for x in contract["assignments"]}) == len(contract["assignments"]),
        "manifestSampleIdsUnique": len({x["sampleId"] for x in manifest["samples"]}) == len(manifest["samples"]),
        "exactSampleCount66": len(contract["assignments"]) == 66 and len(manifest["samples"]) == 66,
    }
    for name, passed in identity_checks.items():
        if not passed:
            global_errors.append(f"identity check failed: {name}")

    assignment_by_id = {x["sampleId"]: x for x in contract["assignments"]}
    samples: list[dict] = []
    total_present = 0
    total_missing = 0

    for entry in sorted(manifest["samples"], key=lambda x: x["sampleId"]):
        sample_id = entry["sampleId"]
        evidence_path = blind / PurePosixPath(entry["evidenceManifest"])
        errors: list[str] = []
        missing_slots: list[dict] = []
        slots: list[dict] = []
        evidence = load_json(evidence_path)
        errors.extend(f"schema: {error}" for error in schema_errors(evidence, schemas["evidence"]))

        if canonical_hash(evidence, "evidenceHash") != evidence.get("evidenceHash"):
            errors.append("canonical evidenceHash mismatch")
        if evidence.get("evidenceHash") != entry.get("evidenceHash"):
            errors.append("manifest/evidence evidenceHash mismatch")
        if evidence.get("sampleId") != sample_id:
            errors.append("sampleId mismatch")
        if evidence.get("designRequirementIds") != entry.get("designRequirementIds"):
            errors.append("manifest/evidence designRequirementIds mismatch")
        if evidence.get("designRequirementIds") != assignment_by_id[sample_id].get("designRequirementIds"):
            errors.append("contract/evidence designRequirementIds mismatch")
        if evidence.get("reviewContractHash") != contract.get("contractHash"):
            errors.append("reviewContractHash mismatch")
        if evidence.get("captureProfilePolicyHash") != EXPECTED_POLICY_HASH:
            errors.append("captureProfilePolicyHash mismatch")
        if evidence.get("captureProfileInstanceHash") != entry.get("captureProfileInstanceHash"):
            errors.append("captureProfileInstanceHash mismatch")
        if evidence.get("frameTableHash") != freeze.get("frameTableHash"):
            errors.append("frameTableHash mismatch")

        source = evidence.get("sourceIdentity", {})
        for evidence_name, freeze_name in (
            ("sceneHash", "sceneHash"),
            ("prefabManifestHash", "prefabManifestHash"),
            ("buildHash", "buildHash"),
            ("captureToolHash", "captureToolHash"),
        ):
            if source.get(evidence_name) != freeze.get(freeze_name):
                errors.append(f"source identity mismatch: {evidence_name}")

        capture_metadata = evidence.get("captureMetadata", {})
        if capture_metadata.get("declaredHash") != capture_metadata.get("actualHash"):
            errors.append("capture metadata declared/actual mismatch")

        observed_slots = [(frame.get("seedOrdinal"), frame.get("frameIndex")) for frame in evidence.get("frames", [])]
        if observed_slots != EXPECTED_SLOTS:
            errors.append("frame records are missing, duplicated, or out of fixed order")

        for frame in evidence.get("frames", []):
            seed = frame["seedOrdinal"]
            frame_number = frame["frameIndex"]
            beauty = frame["beauty"]
            availability = beauty["availability"]
            slot = {
                "seedOrdinal": seed,
                "frameNumber": frame_number,
                "stateRef": frame["stateRef"],
                "availability": availability,
            }
            slots.append(slot)
            if availability == "missing":
                total_missing += 1
                missing_slots.append(slot)
                continue

            total_present += 1
            relative_file = PurePosixPath(beauty["file"])
            frame_path = (blind / relative_file).resolve()
            expected_prefix = (blind / "frames" / sample_id).resolve()
            if frame_path != expected_prefix and expected_prefix not in frame_path.parents:
                errors.append(f"frame path escapes assigned sample: seed {seed} frame {frame_number}")
                continue
            if not frame_path.is_file():
                errors.append(f"declared-present frame absent: seed {seed} frame {frame_number}")
                continue
            actual_file_hash = file_hash(frame_path)
            if beauty.get("declaredHash") != beauty.get("actualHash"):
                errors.append(f"declared/actual frame hash mismatch: seed {seed} frame {frame_number}")
            if actual_file_hash != beauty.get("actualHash"):
                errors.append(f"on-disk frame hash mismatch: seed {seed} frame {frame_number}")
            try:
                with Image.open(frame_path) as image:
                    image.verify()
                with Image.open(frame_path) as image:
                    if image.size != (960, 540):
                        errors.append(f"unexpected frame dimensions {image.size}: seed {seed} frame {frame_number}")
                    image.convert("RGBA").getpixel((480, 270))
            except Exception as exc:
                errors.append(f"unreadable frame seed {seed} frame {frame_number}: {type(exc).__name__}: {exc}")

        if missing_slots:
            errors.append("one or more fixed Beauty frames are explicitly missing")

        samples.append(
            {
                "sampleId": sample_id,
                "designRequirementId": entry["designRequirementIds"][0],
                "evidenceHash": entry["evidenceHash"],
                "captureProfileInstanceHashVerified": evidence.get("captureProfileInstanceHash") == entry.get("captureProfileInstanceHash"),
                "presentBeautyFrameCount": sum(slot["availability"] == "present" for slot in slots),
                "missingBeautyFrameCount": len(missing_slots),
                "missingSlots": missing_slots,
                "slots": slots,
                "evidenceValid": not errors,
                "errors": errors,
            }
        )

    summary = {
        "schemaVersion": "s0a-blind-evidence-preflight/1.0",
        "globalValid": not global_errors,
        "globalErrors": global_errors,
        "hashChecks": hash_checks,
        "identityChecks": identity_checks,
        "sampleCount": len(samples),
        "validEvidenceSampleCount": sum(item["evidenceValid"] for item in samples),
        "invalidEvidenceSampleCount": sum(not item["evidenceValid"] for item in samples),
        "presentBeautyFrameCount": total_present,
        "missingBeautyFrameCount": total_missing,
        "samples": samples,
    }
    args.output.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: summary[key] for key in ("globalValid", "sampleCount", "validEvidenceSampleCount", "invalidEvidenceSampleCount", "presentBeautyFrameCount", "missingBeautyFrameCount")}, indent=2))
    return 0 if summary["globalValid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
