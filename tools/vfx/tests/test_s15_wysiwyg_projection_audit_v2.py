from __future__ import annotations

import hashlib
import json
import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from vfx.s15_wysiwyg_projection_audit_v2 import (  # noqa: E402
    AUTHORITY_RUN_NAME,
    EvidenceInvalid,
    PENDING,
    SUPERSEDED_V1_JSON_SHA256,
    SUPERSEDED_V1_REPORT_SHA256,
    UNATTRIBUTED,
    audit,
    main,
)


class S15WysiwygProjectionAuditV2Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repository = Path(__file__).resolve().parents[3]
        cls.authority_run = (
            cls.repository
            / "docs/stage-notes/s15-wysiwyg-evidence"
            / AUTHORITY_RUN_NAME
        )

    def test_real_authority_is_hash_verified_without_source_attribution(self) -> None:
        result = audit(self.authority_run)
        self.assertEqual(result["schema"], "s15-wysiwyg-projection-audit/v2")
        self.assertEqual(result["summary"]["evidenceIntegrity"], "VERIFIED_28_OF_28")
        self.assertEqual(result["visualVerdict"], "NOT_EVALUATED")
        self.assertEqual(result["machineStatuses"]["candidateSourceAttribution"], UNATTRIBUTED)
        for status in (
            "trueSparkProjectionCanvasContainment",
            "trueSparkBladeOverlap",
            "trueSparkVisibility",
            "trueSparkOcclusion",
        ):
            self.assertEqual(result["machineStatuses"][status], PENDING)
        for frame in result["frames"]:
            context = frame["recorderContext"]
            self.assertEqual(
                context["unattributedRecorderLiveCount"],
                context["sparkLiveCount"] + context["dissipationLiveCount"],
            )
            self.assertEqual(context["sourceAttribution"], UNATTRIBUTED)
            self.assertTrue(
                all(
                    candidate["sourceAttribution"] == UNATTRIBUTED
                    for candidate in frame["detachedWarmCandidateComponents"]
                )
            )

    def test_frame20_counterexample_cannot_become_a_particle_ratio(self) -> None:
        result = audit(self.authority_run)
        frame20 = result["frames"][20]
        context = frame20["recorderContext"]
        self.assertEqual(context["sparkLiveCount"], 1)
        self.assertEqual(context["dissipationLiveCount"], 3)
        self.assertEqual(context["unattributedRecorderLiveCount"], 4)
        self.assertEqual(frame20["detachedWarmCandidateComponentCount"], 1)
        serialized = json.dumps(result, ensure_ascii=False, sort_keys=True)
        obsolete_key = "detachedVisibility" + "LowerBound"
        forbidden_ratios = ("4" + "/8", "6" + "/8", "1" + "/1")
        self.assertNotIn(obsolete_key, serialized)
        for ratio in forbidden_ratios:
            self.assertNotIn(ratio, serialized)

    def test_hash_mismatch_is_evidence_invalid(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            clone = Path(temp) / AUTHORITY_RUN_NAME
            clone.mkdir()
            metadata = json.loads((self.authority_run / "metadata.json").read_text(encoding="utf-8"))
            # Only the first copied frame needs to fail: audit rejects before
            # reaching the missing later files.
            first = metadata["frames"][0]
            (clone / first["file"]).write_bytes((self.authority_run / first["file"]).read_bytes() + b"tamper")
            (clone / "metadata.json").write_text(json.dumps(metadata), encoding="utf-8")
            with self.assertRaisesRegex(EvidenceInvalid, "hash-mismatched"):
                audit(clone)

    def test_v2_outputs_are_write_once_and_v1_bytes_are_preserved(self) -> None:
        v1 = (
            self.repository
            / "docs/stage-notes/s15-wysiwyg-derived"
            / f"{AUTHORITY_RUN_NAME}-projection-audit-v1"
        )
        self.assertEqual(
            hashlib.sha256((v1 / "projection-metrics.json").read_bytes()).hexdigest(),
            SUPERSEDED_V1_JSON_SHA256,
        )
        self.assertEqual(
            hashlib.sha256((v1 / "PROJECTION_AUDIT.md").read_bytes()).hexdigest(),
            SUPERSEDED_V1_REPORT_SHA256,
        )
        with tempfile.TemporaryDirectory() as temp:
            json_output = Path(temp) / "projection-metrics-v2.json"
            markdown_output = Path(temp) / "PROJECTION_AUDIT_V2.md"
            args = [
                "--authority-run",
                str(self.authority_run),
                "--json-output",
                str(json_output),
                "--markdown-output",
                str(markdown_output),
            ]
            self.assertEqual(main(args), 0)
            generated = json.loads(json_output.read_text(encoding="utf-8"))
            self.assertEqual(generated["supersedes"]["status"], "SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO")
            with self.assertRaises(FileExistsError):
                main(args)

    def test_active_v2_references_have_no_obsolete_ratio_semantics(self) -> None:
        stage_notes = self.repository / "docs/stage-notes"
        v2_dir = (
            stage_notes
            / "s15-wysiwyg-derived"
            / f"{AUTHORITY_RUN_NAME}-projection-audit-v2"
        )
        active_files = [
            self.repository / "tools/vfx/s15_wysiwyg_projection_audit_v2.py",
            Path(__file__),
            stage_notes / "S15_VISUAL_DELTA_AND_TECHNICAL_PLAN.md",
            stage_notes / "S15_REPORT.md",
            v2_dir / "projection-metrics.json",
            v2_dir / "PROJECTION_AUDIT.md",
        ]
        obsolete_key = "detachedVisibility" + "LowerBound"
        forbidden_ratios = ("4" + "/8", "6" + "/8", "1" + "/1")
        for path in active_files:
            text = path.read_text(encoding="utf-8")
            self.assertNotIn(obsolete_key, text, str(path))
            for ratio in forbidden_ratios:
                self.assertNotIn(ratio, text, str(path))

        reports = "\n".join(
            path.read_text(encoding="utf-8")
            for path in active_files[2:4]
        )
        derived_links = re.findall(
            r"\]\((s15-wysiwyg-derived/[^)]+)\)", reports
        )
        self.assertTrue(derived_links)
        self.assertTrue(all("projection-audit-v2/" in link for link in derived_links))
        self.assertIn("SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO", reports)

        allowed_v1_dir = (
            self.repository
            / "docs/stage-notes/s15-wysiwyg-derived"
            / f"{AUTHORITY_RUN_NAME}-projection-audit-v1"
        ).resolve()
        allowed_v1_tool = (
            self.repository / "tools/vfx/s15_wysiwyg_projection_audit.py"
        ).resolve()
        active_obsolete_references: list[str] = []
        for scan_root in (self.repository / "docs", self.repository / "tools"):
            for path in scan_root.rglob("*"):
                if not path.is_file() or path.suffix.lower() not in {".py", ".md", ".json"}:
                    continue
                resolved = path.resolve()
                if resolved == allowed_v1_tool or allowed_v1_dir in resolved.parents:
                    continue
                relative = path.relative_to(self.repository)
                try:
                    if obsolete_key in path.read_text(encoding="utf-8"):
                        active_obsolete_references.append(relative.as_posix())
                except UnicodeError:
                    continue
        self.assertEqual(active_obsolete_references, [])


if __name__ == "__main__":
    unittest.main()
