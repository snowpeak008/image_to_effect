"""Deterministic, file-system-only W24 S4 legacy VFX inventory.

This intentionally has no Unity/AssetDatabase dependency and never writes below the
Unity project.  Its only optional write is an explicitly named, write-once audit
record outside ``project/``.  It is a migration *preparation* tool: every item is
reported as ``VISUAL_PENDING`` and ADR-001 Proposed freezes all apply operations.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


SCHEMA = "w24-s4/readonly-inventory-v1"
HEX64 = re.compile(r"^[0-9a-f]{64}$")
GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.IGNORECASE | re.MULTILINE)


def _sha256_bytes(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    return _sha256_bytes(path.read_bytes())


def _canonical_hash(value: object) -> str | None:
    if not isinstance(value, str):
        return None
    value = value.lower()
    value = value if value.startswith("sha256:") else "sha256:" + value
    return value if HEX64.fullmatch(value[7:]) else None


def _project_path(project: Path, value: object) -> Path | None:
    if not isinstance(value, str) or not value.startswith("Assets/") or ".." in value.replace("\\", "/").split("/"):
        return None
    candidate = (project / value).resolve()
    assets = (project / "Assets").resolve()
    try:
        candidate.relative_to(assets)
    except ValueError:
        return None
    return candidate


def _meta_guid(path: Path) -> str | None:
    meta = path.with_name(path.name + ".meta")
    if not meta.is_file():
        return None
    found = GUID.search(meta.read_text(encoding="utf-8", errors="replace"))
    return found.group(1).lower() if found else None


def _bound_effect_files(root: Path, effect_id: str, directories: Iterable[Path]) -> list[str]:
    found: list[str] = []
    marker = '"effectId"'
    for directory in directories:
        if not directory.is_dir():
            continue
        for path in sorted(directory.rglob("*.json")):
            try:
                data = json.loads(path.read_text(encoding="utf-8"))
                matched = isinstance(data, dict) and data.get("effectId") == effect_id
            except (OSError, json.JSONDecodeError):
                # Legacy registries sometimes use a file name convention.  A malformed
                # JSON file is never counted as a contract/trace evidence source.
                matched = False
            if matched and marker:
                found.append(path.relative_to(root).as_posix())
    return found


def _named_artifacts(project: Path, effect_id: str, folders: Iterable[str]) -> list[str]:
    result: list[str] = []
    needle = effect_id.lower()
    for folder in folders:
        directory = project / folder
        if directory.is_dir():
            result.extend(
                path.relative_to(project).as_posix()
                for path in sorted(directory.rglob("*"))
                if path.is_file() and needle in path.name.lower()
            )
    return result


def _adr001(repository: Path) -> dict[str, str | bool | None]:
    path = repository / "docs/rules/ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md"
    if not path.is_file():
        return {"path": "docs/rules/ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md", "status": "Missing", "decisionMaker": None, "documentHash": None, "m3Frozen": True}
    text = path.read_text(encoding="utf-8")
    status = re.search(r"^状态：\s*`?([^`（\r\n]+)", text, re.MULTILINE)
    maker = re.search(r"^决策人：\s*([^\r\n]+)", text, re.MULTILINE)
    status_value = status.group(1).strip() if status else "Unknown"
    maker_value = maker.group(1).strip().strip("`") if maker else None
    accepted = status_value == "Accepted" and bool(maker_value and maker_value != "待填写")
    return {"path": path.relative_to(repository).as_posix(), "status": status_value, "decisionMaker": maker_value, "documentHash": _sha256_file(path), "m3Frozen": not accepted}


def _entry(project: Path, repository: Path, effect_id: str) -> dict[str, Any]:
    manifest_path = project / "ProjectSettings/VFXComposer/BuildManifests" / f"{effect_id}.manifest.json"
    generated = project / "Assets/VFX/Generated" / effect_id
    reasons: list[str] = []
    warnings: list[str] = []
    carrier_keys: list[str] = []
    manifest: dict[str, Any] | None = None
    if not manifest_path.is_file():
        reasons.append("missing_build_manifest")
    else:
        try:
            loaded = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest = loaded if isinstance(loaded, dict) and loaded.get("effectId") == effect_id else None
        except json.JSONDecodeError:
            manifest = None
        if manifest is None:
            reasons.append("invalid_or_mismatched_build_manifest")

    verification = {"runtimeEntry": False, "runtimeGuid": False, "runtimeHash": False, "allOwnedOutputs": False}
    recipe_path: str | None = None
    runtime_path: str | None = None
    runtime_guid: str | None = None
    enforcement: str | None = None
    if manifest is not None:
        enforcement = manifest.get("enforcement") if isinstance(manifest.get("enforcement"), str) else None
        recipe_candidate = _project_path(project, manifest.get("sourceRecipePath"))
        recipe_path = manifest.get("sourceRecipePath") if isinstance(manifest.get("sourceRecipePath"), str) else None
        if recipe_candidate is None or not recipe_candidate.is_file():
            reasons.append("missing_declared_recipe")
        if not isinstance(manifest.get("rulesVersion"), str) or not manifest["rulesVersion"]:
            reasons.append("missing_rules_version")
        if not enforcement:
            reasons.append("missing_enforcement")
        for key in ("recipeHash", "buildHash"):
            if _canonical_hash(manifest.get(key)) is None:
                reasons.append(f"invalid_{key}")
        if not isinstance(manifest.get("compilerVersion"), str) or not isinstance(manifest.get("unityVersion"), str):
            reasons.append("missing_build_identity")

        runtime = manifest.get("runtimeEntry")
        runtime_candidate = _project_path(project, runtime.get("path") if isinstance(runtime, dict) else None)
        if not isinstance(runtime, dict) or runtime.get("kind") != "prefab" or runtime_candidate is None or not runtime_candidate.is_file():
            reasons.append("missing_or_invalid_runtime_entry")
        else:
            verification["runtimeEntry"] = True
            runtime_path = runtime["path"]
            runtime_guid = runtime.get("guid").lower() if isinstance(runtime.get("guid"), str) else None
            owned = manifest.get("ownedOutputs") if isinstance(manifest.get("ownedOutputs"), list) else []
            owned_runtime = next((item for item in owned if isinstance(item, dict) and item.get("path") == runtime_path), None)
            verification["runtimeGuid"] = bool(owned_runtime and runtime_guid and owned_runtime.get("guid", "").lower() == runtime_guid == _meta_guid(runtime_candidate))
            verification["runtimeHash"] = bool(owned_runtime and _canonical_hash(owned_runtime.get("sha256")) == _sha256_file(runtime_candidate))
            if not verification["runtimeGuid"]:
                reasons.append("runtime_guid_unverified")
            if not verification["runtimeHash"]:
                reasons.append("runtime_hash_unverified")

            seen: set[str] = set()
            outputs_ok = bool(owned)
            for output in owned:
                path_value = output.get("path") if isinstance(output, dict) else None
                candidate = _project_path(project, path_value)
                if not isinstance(output, dict) or not isinstance(path_value, str) or path_value in seen or candidate is None or not candidate.is_file():
                    outputs_ok = False
                    continue
                seen.add(path_value)
                expected_guid = output.get("guid").lower() if isinstance(output.get("guid"), str) else None
                if not isinstance(output.get("assetType"), str) or not expected_guid or expected_guid != _meta_guid(candidate) or _canonical_hash(output.get("sha256")) != _sha256_file(candidate):
                    outputs_ok = False
                if enforcement == "strict" and not path_value.startswith(f"Assets/VFX/Generated/{effect_id}/"):
                    outputs_ok = False
            verification["allOwnedOutputs"] = outputs_ok
            if not outputs_ok:
                reasons.append("owned_outputs_unverified")

        for warning in manifest.get("audit", []) if isinstance(manifest.get("audit"), list) else []:
            if isinstance(warning, dict) and isinstance(warning.get("code"), str):
                warnings.append(warning["code"])
        for group in (manifest.get("dependencies"), manifest.get("templates")):
            if not isinstance(group, list):
                continue
            for dependency in group:
                value = dependency.get("path") if isinstance(dependency, dict) else dependency.get("assetPath") if isinstance(dependency, dict) else None
                if isinstance(value, str) and ("/Shared/" in value or "/Templates/" in value):
                    carrier_keys.append(value.replace("\\", "/"))

    contract_paths = _bound_effect_files(repository, effect_id, [project / "Assets/VFX/Contracts", project / "ProjectSettings/VFXComposer/W24/Contracts", repository / "docs/vfx-contracts"])
    trace_paths = _bound_effect_files(repository, effect_id, [project / "Assets/VFX/Traces", project / "ProjectSettings/VFXComposer/W24/Traces", repository / "docs/vfx-traces"])
    preview_paths = _named_artifacts(project, effect_id, ("Assets/VFX/Preview", "Assets/VFX/Previews"))
    evidence_paths = _named_artifacts(project, effect_id, ("Assets/VFX/Evidence", "ProjectSettings/VFXComposer/W24/Evidence"))
    if not contract_paths:
        reasons.append("missing_w24_contract")
    if not trace_paths:
        reasons.append("missing_implementation_trace")
    if not preview_paths:
        reasons.append("missing_preview_artifact")
    if not evidence_paths:
        reasons.append("missing_four_route_evidence")

    ownership = all(verification.values()) and manifest is not None
    if not ownership:
        route, batch = "QuarantineReview", "B0-quarantine-review"
    elif enforcement == "legacy_audit":
        route, batch = "LegacyRetain", "B1-legacy-preservation"
    elif warnings:
        route, batch = "WaiverReview", "B2-waiver-review"
    else:
        route, batch = "RebuildCandidate", "B3-rebuild-candidates"
    score = min(100, (40 if manifest is None else 0) + (30 if not verification["runtimeEntry"] else 0) + (20 if not verification["runtimeGuid"] or not verification["runtimeHash"] else 0) + (20 if not verification["allOwnedOutputs"] else 0) + 10 * (not contract_paths) + 10 * (not trace_paths) + 7 * (not preview_paths) + 8 * (not evidence_paths) + 8 * (enforcement == "legacy_audit") + min(12, 2 * len(warnings)))
    return {
        "effectId": effect_id, "visualStatus": "VISUAL_PENDING", "generatedDirectory": generated.relative_to(project).as_posix(), "hasGeneratedDirectory": generated.is_dir(),
        "manifestPath": manifest_path.relative_to(project).as_posix(), "hasManifest": manifest is not None, "enforcement": enforcement,
        "runtimeEntryPath": runtime_path, "runtimeEntryGuid": runtime_guid, "recipePath": recipe_path,
        "verification": verification, "ownershipVerified": ownership, "contractPaths": contract_paths, "tracePaths": trace_paths,
        "previewPaths": preview_paths, "evidencePaths": evidence_paths, "auditWarnings": sorted(set(warnings)), "carrierKeys": sorted(set(carrier_keys)),
        "missingOrBlocking": sorted(set(reasons)), "riskScore": score, "riskBand": "HIGH" if score >= 70 else "MEDIUM" if score >= 35 else "LOW", "suggestedRoute": route, "suggestedBatch": batch,
    }


def scan(project: Path) -> dict[str, Any]:
    project = project.resolve()
    repository = project.parent
    generated = project / "Assets/VFX/Generated"
    manifests = project / "ProjectSettings/VFXComposer/BuildManifests"
    ids = {path.name for path in generated.iterdir() if path.is_dir()} if generated.is_dir() else set()
    ids.update(path.name.removesuffix(".manifest.json") for path in manifests.glob("*.manifest.json") if manifests.is_dir())
    entries = [_entry(project, repository, effect_id) for effect_id in sorted(ids)]
    carrier_groups: dict[str, list[str]] = defaultdict(list)
    for entry in entries:
        for key in entry["carrierKeys"]:
            carrier_groups[key].append(entry["effectId"])
    reusable = [{"carrierKey": key, "effectIds": sorted(values), "reviewReason": "shared dependency/template is only a visual-QA sampling signal; it is not a homogeneity or quality verdict"} for key, values in sorted(carrier_groups.items()) if len(set(values)) >= 3]
    canonical_entries = json.dumps(entries, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    routes = Counter(entry["suggestedRoute"] for entry in entries)
    blockers = Counter(reason for entry in entries for reason in entry["missingOrBlocking"])
    return {
        "schemaVersion": SCHEMA, "scanMode": "READ_ONLY", "projectRoot": project.as_posix(), "visualVerdict": "NOT_EVALUATED", "migrationApplyAllowed": False,
        "adr001": _adr001(repository), "entries": entries, "carrierReuseSampling": reusable,
        "summary": {"entryCount": len(entries), "generatedDirectoryCount": sum(entry["hasGeneratedDirectory"] for entry in entries), "manifestCount": sum(entry["hasManifest"] for entry in entries), "routeCounts": dict(sorted(routes.items())), "blockingCounts": dict(sorted(blockers.items()))},
        "inventoryHash": _sha256_bytes(canonical_entries),
    }


def render_markdown(inventory: dict[str, Any]) -> str:
    lines = ["# W24 S4 既有资产只读审计快照", "", "- 审计模式：`READ_ONLY`；本工具不会写入 Unity Project。", "- 视觉结论：`NOT_EVALUATED`；全部条目保持 `VISUAL_PENDING`，不产生 L0–L4 或视觉通过。", "- 迁移 Apply：`false`；ADR-001 仍冻结 M3 时不得迁移。", f"- Inventory hash: `{inventory['inventoryHash']}`", f"- ADR-001: `{inventory['adr001']['status']}`；M3 frozen: `{inventory['adr001']['m3Frozen']}`", "", "## 汇总", "", "| 指标 | 数量 |", "|---|---:|", f"| 正式条目并集 | {inventory['summary']['entryCount']} |", f"| Generated 目录 | {inventory['summary']['generatedDirectoryCount']} |", f"| 可解析 Manifest | {inventory['summary']['manifestCount']} |"]
    for route, count in inventory["summary"]["routeCounts"].items():
        lines.append(f"| {route} | {count} |")
    lines.extend(["", "## 条目路由", "", "| EffectId | 风险 | 路由 | 批次 | 所有权 | 合同/Trace | 阻塞项 |", "|---|---:|---|---|---|---|---|"])
    for entry in inventory["entries"]:
        lines.append("| `{effectId}` | {riskScore} ({riskBand}) | {suggestedRoute} | {suggestedBatch} | {ownership} | {contract}/{trace} | {blockers} |".format(effectId=entry["effectId"], riskScore=entry["riskScore"], riskBand=entry["riskBand"], suggestedRoute=entry["suggestedRoute"], suggestedBatch=entry["suggestedBatch"], ownership="yes" if entry["ownershipVerified"] else "no", contract="yes" if entry["contractPaths"] else "no", trace="yes" if entry["tracePaths"] else "no", blockers=", ".join(entry["missingOrBlocking"]) or "—"))
    lines.extend(["", "## 约束", "", "本快照仅提供用户后续抽样和迁移裁决的输入。它不改写 Recipe、Manifest、Generated、Preview 或证据；不执行 Apply；不把共享载体复用解释为视觉同质化。ADR-001 只有 Accepted 且决策人具名后，仍需重新盘点、所有权核验、显式用户 token 和可回滚事务。", ""])
    return "\n".join(lines)


def _write_once(path: Path, content: str) -> None:
    if path.exists():
        raise FileExistsError(f"refusing to overwrite write-once audit output: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--json-output", type=Path)
    parser.add_argument("--markdown-output", type=Path)
    args = parser.parse_args(argv)
    if bool(args.json_output) != bool(args.markdown_output):
        parser.error("--json-output and --markdown-output must be supplied together")
    inventory = scan(args.project)
    if args.json_output:
        _write_once(args.json_output, json.dumps(inventory, ensure_ascii=False, sort_keys=True, indent=2) + "\n")
        _write_once(args.markdown_output, render_markdown(inventory))
    else:
        print(json.dumps(inventory["summary"], ensure_ascii=False, sort_keys=True))
        print(inventory["inventoryHash"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
