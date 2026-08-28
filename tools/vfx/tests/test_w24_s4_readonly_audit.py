from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from vfx.w24_s4_readonly_audit import main, scan  # noqa: E402


class W24S4ReadonlyAuditTests(unittest.TestCase):
    def _project(self) -> tuple[tempfile.TemporaryDirectory[str], Path]:
        temp = tempfile.TemporaryDirectory()
        repository = Path(temp.name)
        project = repository / "project"
        (repository / "docs/rules").mkdir(parents=True)
        (repository / "docs/rules/ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md").write_text("状态：`Proposed`\n决策人：待填写\n", encoding="utf-8")
        return temp, project

    @staticmethod
    def _write(path: Path, value: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value, encoding="utf-8")

    def _formal(self, project: Path, effect: str, *, legacy: bool = False) -> None:
        prefab = project / f"Assets/VFX/Generated/{effect}/VFX_{effect}.prefab"
        self._write(prefab, "prefab")
        self._write(prefab.with_name(prefab.name + ".meta"), "guid: 0123456789abcdef0123456789abcdef\n")
        recipe = project / f"Assets/VFX/Recipes/{effect}.json"
        self._write(recipe, "{}")
        import hashlib
        digest = hashlib.sha256(prefab.read_bytes()).hexdigest()
        manifest = {"manifestVersion": 1, "effectId": effect, "enforcement": "legacy_audit" if legacy else "strict", "rulesVersion": "1", "recipeVersion": 1, "recipeRevision": 1, "recipeHash": "0" * 64, "buildHash": "1" * 64, "compilerVersion": "test", "unityVersion": "2022", "sourceRecipePath": f"Assets/VFX/Recipes/{effect}.json", "runtimeEntry": {"kind": "prefab", "path": f"Assets/VFX/Generated/{effect}/VFX_{effect}.prefab", "guid": "0123456789abcdef0123456789abcdef"}, "ownedOutputs": [{"path": f"Assets/VFX/Generated/{effect}/VFX_{effect}.prefab", "guid": "0123456789abcdef0123456789abcdef", "assetType": "GameObject", "sha256": digest}], "dependencies": [{"path": "Assets/VFX/Shared/T.asset"}]}
        self._write(project / f"ProjectSettings/VFXComposer/BuildManifests/{effect}.manifest.json", json.dumps(manifest))

    def test_audit_is_deterministic_nonvisual_and_m3_is_frozen(self) -> None:
        temp, project = self._project()
        try:
            self._formal(project, "good")
            first, second = scan(project), scan(project)
            self.assertEqual(first["inventoryHash"], second["inventoryHash"])
            item = first["entries"][0]
            self.assertTrue(item["ownershipVerified"])
            self.assertEqual(item["visualStatus"], "VISUAL_PENDING")
            self.assertEqual(item["suggestedRoute"], "RebuildCandidate")
            self.assertTrue(first["adr001"]["m3Frozen"])
            self.assertFalse(first["migrationApplyAllowed"])
        finally:
            temp.cleanup()

    def test_manifest_only_is_quarantined_and_legacy_is_retained(self) -> None:
        temp, project = self._project()
        try:
            self._formal(project, "legacy", legacy=True)
            self._write(project / "ProjectSettings/VFXComposer/BuildManifests/missing.manifest.json", '{"effectId":"missing"}')
            entries = {entry["effectId"]: entry for entry in scan(project)["entries"]}
            self.assertEqual(entries["legacy"]["suggestedRoute"], "LegacyRetain")
            self.assertEqual(entries["missing"]["suggestedRoute"], "QuarantineReview")
        finally:
            temp.cleanup()

    def test_optional_outputs_are_write_once(self) -> None:
        temp, project = self._project()
        try:
            self._formal(project, "good")
            json_out, markdown_out = Path(temp.name) / "out.json", Path(temp.name) / "out.md"
            self.assertEqual(main(["--project", str(project), "--json-output", str(json_out), "--markdown-output", str(markdown_out)]), 0)
            with self.assertRaises(FileExistsError):
                main(["--project", str(project), "--json-output", str(json_out), "--markdown-output", str(markdown_out)])
        finally:
            temp.cleanup()


if __name__ == "__main__":
    unittest.main()
