"""Preservation checks for the independently rejected S15 audit v1.

V1 is intentionally not executed as an active measurement. Its tool and two
derived outputs remain byte-identical solely to preserve the historical audit
chain; all active semantics and tests live in the v2 module.
"""
from __future__ import annotations

import hashlib
import unittest
from pathlib import Path


class SupersededS15WysiwygProjectionAuditV1Tests(unittest.TestCase):
    def test_superseded_v1_bytes_remain_frozen(self) -> None:
        repository = Path(__file__).resolve().parents[3]
        v1 = (
            repository
            / "docs/stage-notes/s15-wysiwyg-derived"
            / "run-20260823T041806959Z-projection-audit-v1"
        )
        expected = {
            "projection-metrics.json": "a73619e373d7c76984a4328c023be8fa66c45d05ba15e4dddc42aff349306d41",
            "PROJECTION_AUDIT.md": "876aecaf99088b8456d5c49f2b23941a0c56bd9d18e82ad0bb4f80d42297cb48",
        }
        for name, digest in expected.items():
            self.assertEqual(hashlib.sha256((v1 / name).read_bytes()).hexdigest(), digest)

        tool = repository / "tools/vfx/s15_wysiwyg_projection_audit.py"
        self.assertEqual(
            hashlib.sha256(tool.read_bytes()).hexdigest(),
            "9e7bf9fb15d742606b89544d1de36b0b6f8c82da0f8ec012562325c9dcb39225",
        )


if __name__ == "__main__":
    unittest.main()
