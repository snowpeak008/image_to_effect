#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path, PurePosixPath

import numpy as np
from PIL import Image


def load_rgba(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGBA"), dtype=np.int16)


def bbox(mask: np.ndarray) -> list[int] | None:
    ys, xs = np.nonzero(mask)
    if not len(xs):
        return None
    return [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blind-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    blind = args.blind_root.resolve(strict=True)
    manifest = json.loads((blind / "blind-submission-manifest.json").read_text(encoding="utf-8"))
    baseline = load_rgba(blind / "frames/s0a-0793e1170c44a44614cd/seed_0/frame_00300_beauty.png")
    rows = []

    for entry in sorted(manifest["samples"], key=lambda item: item["sampleId"]):
        evidence = json.loads((blind / PurePosixPath(entry["evidenceManifest"])).read_text(encoding="utf-8"))
        for frame in evidence["frames"]:
            beauty = frame["beauty"]
            if beauty["availability"] != "present":
                rows.append(
                    {
                        "sampleId": entry["sampleId"],
                        "seedOrdinal": frame["seedOrdinal"],
                        "frameNumber": frame["frameIndex"],
                        "stateRef": frame["stateRef"],
                        "availability": "missing",
                    }
                )
                continue
            pixels = load_rgba(blind / PurePosixPath(beauty["file"]))
            rgb = pixels[:, :, :3]
            diff = np.max(np.abs(pixels - baseline), axis=2)
            red = rgb[:, :, 0]
            green = rgb[:, :, 1]
            blue = rgb[:, :, 2]
            bright_fire = (red >= 100) & (green >= 30) & (red >= green) & (green >= blue * 1.2)
            hot_fire = (red >= 180) & (green >= 80) & (green >= blue * 1.5)
            glow = (red >= 20) & (red >= green * 1.3) & (red >= blue * 1.5)
            bright_box = bbox(bright_fire)
            hot_box = bbox(hot_fire)
            rows.append(
                {
                    "sampleId": entry["sampleId"],
                    "seedOrdinal": frame["seedOrdinal"],
                    "frameNumber": frame["frameIndex"],
                    "stateRef": frame["stateRef"],
                    "availability": "present",
                    "diffPixelsGt1": int((diff > 1).sum()),
                    "diffPixelsGt5": int((diff > 5).sum()),
                    "diffPixelsGt20": int((diff > 20).sum()),
                    "maxDiff": int(diff.max()),
                    "sumDiff": int(diff.sum()),
                    "brightFirePixels": int(bright_fire.sum()),
                    "hotFirePixels": int(hot_fire.sum()),
                    "glowPixels": int(glow.sum()),
                    "brightBBox": bright_box,
                    "hotBBox": hot_box,
                    "brightTouchesEdge": bool(bright_box and (bright_box[0] == 0 or bright_box[2] == 959 or bright_box[1] == 0 or bright_box[3] == 539)),
                }
            )

    args.output.write_text(json.dumps({"rows": rows}, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"frameRecords": len(rows), "present": sum(row["availability"] == "present" for row in rows), "missing": sum(row["availability"] == "missing" for row in rows)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
