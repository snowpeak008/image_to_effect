#!/usr/bin/env python3
"""Validate a strict W24 S1 design contract, canonical hash, and pre-C0 identities."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from jsonschema import Draft202012Validator


def canonical_sha256(document: dict, omitted_field: str) -> str:
    material = dict(document)
    material.pop(omitted_field, None)
    encoded = json.dumps(material, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("contract", type=Path)
    parser.add_argument("--schema", type=Path, default=Path(__file__).resolve().parents[1] / "schemas" / "vfx-design-contract.schema.json")
    args = parser.parse_args()

    contract = json.loads(args.contract.read_text(encoding="utf-8"))
    schema = json.loads(args.schema.read_text(encoding="utf-8"))
    errors = [f"{error.json_path or '$'}: {error.message}" for error in Draft202012Validator(schema).iter_errors(contract)]
    profile = contract.get("captureProfile", {})
    profile = profile if isinstance(profile, dict) else {}
    canonical_seed = profile.get("canonicalSeed")
    robustness_seeds = profile.get("robustnessSeeds", [])
    robustness_seeds = robustness_seeds if isinstance(robustness_seeds, list) else []
    if canonical_seed in robustness_seeds:
        errors.append("captureProfile.canonicalSeed must differ from both robustnessSeeds")
    extensions = contract.get("extensions", {})
    extensions = extensions if isinstance(extensions, dict) else {}
    binding_status = extensions.get("captureBindingStatus")
    pending = "pending:formal-build"
    scene_identity = profile.get("sceneHash")
    manifest_identity = profile.get("prefabManifestHash")
    tool_identity = profile.get("captureToolHash")
    if binding_status == "PENDING_FIRST_FORMAL_BUILD":
        if scene_identity != pending or manifest_identity != pending:
            errors.append("pre-C0 PENDING_FIRST_FORMAL_BUILD requires sceneHash and prefabManifestHash to equal pending:formal-build")
    else:
        if scene_identity == pending or manifest_identity == pending:
            errors.append("pending capture identities are forbidden unless captureBindingStatus is PENDING_FIRST_FORMAL_BUILD")
        zero_hash = "sha256:" + "0" * 64
        if scene_identity == zero_hash or manifest_identity == zero_hash:
            errors.append("all-zero scene or prefab-manifest hashes are not content identities; use pending:formal-build before C0")
    if tool_identity == "sha256:" + "0" * 64:
        errors.append("captureProfile.captureToolHash must identify a real registered tool bundle and may not be all-zero")
    if binding_status == "FROZEN_PRE_C0" and (scene_identity == pending or manifest_identity == pending):
        errors.append("FROZEN_PRE_C0 requires real scene and prefab-manifest identities")
    state_machine = contract.get("semanticStateMachine", {})
    state_machine = state_machine if isinstance(state_machine, dict) else {}
    states = state_machine.get("states", [])
    state_ids = [state.get("stateId") for state in states if isinstance(state, dict)]
    if len(state_ids) != len(set(state_ids)):
        errors.append("semanticStateMachine.stateId values must be unique")
    if not state_machine.get("transitions"):
        errors.append("semanticStateMachine.transitions must contain at least one transition")
    for transition in state_machine.get("transitions", []):
        if not isinstance(transition, dict):
            continue
        if transition.get("from") not in state_ids or transition.get("to") not in state_ids:
            errors.append("semanticStateMachine transitions may reference only declared stateId values")
            break
    layers = contract.get("layers", [])
    layers = layers if isinstance(layers, list) else []
    layer_ids = [layer.get("layerId") for layer in layers if isinstance(layer, dict)]
    if len(layer_ids) != len(set(layer_ids)):
        errors.append("layerId values must be unique")
    for group_name in ("allowedSubstitutions", "forbiddenSubstitutions"):
        substitution_ids = [item.get("substitutionId") for item in contract.get(group_name, []) if isinstance(item, dict)]
        if len(substitution_ids) != len(set(substitution_ids)):
            errors.append(f"{group_name}.substitutionId values must be unique")
    cleanup = contract.get("cleanup", {})
    cleanup = cleanup if isinstance(cleanup, dict) else {}
    residual = cleanup.get("allowedResidualLayers", [])
    if any(layer_id not in layer_ids for layer_id in residual):
        errors.append("cleanup.allowedResidualLayers may reference only declared layerId values")
    budget = contract.get("budget", {})
    budget = budget if isinstance(budget, dict) else {}
    residency = budget.get("textureResidency", {})
    residency = residency if isinstance(residency, dict) else {}
    if residency.get("totalDependencyMb", 0) < residency.get("localExclusiveMb", 0):
        errors.append("budget.textureResidency.totalDependencyMb must be >= localExclusiveMb")
    requirements = contract.get("requirements", [])
    requirements = requirements if isinstance(requirements, list) else []
    requirement_ids = [item.get("designRequirementId") for item in requirements if isinstance(item, dict)]
    if len(requirement_ids) != len(set(requirement_ids)):
        errors.append("designRequirementId values must be unique")
    for requirement in requirements:
        if not isinstance(requirement, dict):
            continue
        verdict = requirement.get("visualVerdict") or {}
        verdict = verdict if isinstance(verdict, dict) else {}
        location = verdict.get("evidenceLocation") or {}
        location = location if isinstance(location, dict) else {}
        if location.get("layerMask") is not None and location.get("layerMask") not in layer_ids:
            errors.append("visualVerdict.evidenceLocation.layerMask may reference only a declared layerId")
            break
    supplied_hash = contract.get("contractHash")
    expected_hash = canonical_sha256(contract, "contractHash")
    if supplied_hash != expected_hash:
        errors.append("contractHash does not match canonical UTF-8 JSON with contractHash omitted")
    if errors:
        print("W24 design contract invalid:")
        print("\n".join(f"- {error}" for error in errors))
        return 1
    print("W24 design contract valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
