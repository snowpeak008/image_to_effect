#!/usr/bin/env python3
"""Validate a sealed Visual QA report's schema and canonical report hash."""

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
    parser.add_argument("review", type=Path)
    parser.add_argument("--schema", type=Path, default=Path(__file__).resolve().parents[1] / "schemas" / "vfx-visual-review.schema.json")
    args = parser.parse_args()

    review = json.loads(args.review.read_text(encoding="utf-8"))
    schema = json.loads(args.schema.read_text(encoding="utf-8"))
    errors = [f"{error.json_path or '$'}: {error.message}" for error in Draft202012Validator(schema).iter_errors(review)]
    if review.get("sealedReportHash") != canonical_sha256(review, "sealedReportHash"):
        errors.append("sealedReportHash does not match canonical UTF-8 JSON with sealedReportHash omitted")
    if errors:
        print("Visual QA report invalid:")
        print("\n".join(f"- {error}" for error in errors))
        return 1
    print("Visual QA report valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
