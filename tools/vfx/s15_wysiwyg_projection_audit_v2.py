#!/usr/bin/env python3
"""V2 read-only geometry audit for the retained S15 Slash beauty frames.

V2 deliberately does not attribute any RGB component to a spark or turn a
component count into a visibility bound. Recorder live counts are preserved as
context only. Optional outputs are write-once derived files; sealed authority
and superseded v1 outputs are never opened for writing.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from collections import deque
from pathlib import Path
from typing import Any, Iterable

import numpy as np
from PIL import Image


SCHEMA = "s15-wysiwyg-projection-audit/v2"
AUTHORITY_RUN_NAME = "run-20260823T041806959Z"
FRAME_FILE = re.compile(r"frame_(\d{4})\.png\Z")
SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")
KEY_FRAME_INDEXES = (1, 2, 6, 10, 14, 20, 25, 27)
PENDING = "PENDING_INSTANCE_ID_OR_DEPTH_DIAGNOSTIC"
UNATTRIBUTED = "UNAVAILABLE_FROM_BEAUTY_RGB"

# Reproducible geometry-proxy thresholds. They are not calibrated gates.
CANDIDATE_MAX_AREA_PX = 128
CANDIDATE_MAX_EXTENT_PX = 32
CANDIDATE_MIN_RED_DELTA = 64
BLADE_MIN_RED = 150
BLADE_MIN_GREEN_TO_RED = 0.80
BLADE_MIN_BLUE_TO_RED = 0.25

SUPERSEDED_V1_JSON_SHA256 = "a73619e373d7c76984a4328c023be8fa66c45d05ba15e4dddc42aff349306d41"
SUPERSEDED_V1_REPORT_SHA256 = "876aecaf99088b8456d5c49f2b23941a0c56bd9d18e82ad0bb4f80d42297cb48"


class EvidenceInvalid(ValueError):
    """The sealed evidence chain cannot support a derived measurement."""


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _strict_json(path: Path) -> dict[str, Any]:
    def reject_constant(value: str) -> Any:
        raise ValueError("non-finite JSON constant: " + value)

    def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON field: " + key)
            result[key] = value
        return result

    value = json.loads(
        path.read_text(encoding="utf-8"),
        parse_constant=reject_constant,
        object_pairs_hook=unique_object,
    )
    if not isinstance(value, dict):
        raise ValueError("metadata root is not an object")
    return value


def _finite_number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise EvidenceInvalid(label + " is not numeric")
    result = float(value)
    if not math.isfinite(result):
        raise EvidenceInvalid(label + " is not finite")
    return result


def _nonnegative_count(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise EvidenceInvalid(label + " is not a non-negative integer")
    return value


def _declared_background(metadata: dict[str, Any]) -> np.ndarray:
    camera = metadata.get("camera")
    background = camera.get("background") if isinstance(camera, dict) else None
    if not isinstance(background, list) or len(background) != 4:
        raise EvidenceInvalid("camera.background must contain four channels")
    channels = [_finite_number(value, "camera.background") for value in background]
    if any(value < 0.0 or value > 1.0 for value in channels):
        raise EvidenceInvalid("camera.background is outside [0,1]")
    if abs(channels[3] - 1.0) > 1e-9:
        raise EvidenceInvalid("camera background alpha is not opaque")
    return np.asarray(
        [int(math.floor(value * 255.0 + 0.5)) for value in channels[:3]],
        dtype=np.int16,
    )


def _components(mask: np.ndarray) -> list[dict[str, Any]]:
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    result: list[dict[str, Any]] = []
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]:
            continue
        queue: deque[tuple[int, int]] = deque([(int(y), int(x))])
        seen[y, x] = True
        pixels: list[tuple[int, int]] = []
        while queue:
            cy, cx = queue.popleft()
            pixels.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
        points = np.asarray(pixels, dtype=np.int32)
        ys, xs = points[:, 0], points[:, 1]
        result.append(
            {
                "points": points,
                "area": int(points.shape[0]),
                "bounds": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
            }
        )
    result.sort(key=lambda item: (-item["area"], item["bounds"][1], item["bounds"][0]))
    return result


def _mask_from_points(shape: tuple[int, int], points: np.ndarray) -> np.ndarray:
    result = np.zeros(shape, dtype=bool)
    if points.size:
        result[points[:, 0], points[:, 1]] = True
    return result


def _distance_between(left: np.ndarray, right: np.ndarray) -> float | None:
    if left.size == 0 or right.size == 0:
        return None
    best_squared: int | None = None
    right64 = right.astype(np.int64, copy=False)
    for point in left.astype(np.int64, copy=False):
        delta = right64 - point
        local = int(np.sum(delta * delta, axis=1).min())
        best_squared = local if best_squared is None else min(best_squared, local)
        if best_squared == 0:
            break
    return round(math.sqrt(float(best_squared)), 6) if best_squared is not None else None


def _warm_candidate(component: dict[str, Any], rgb: np.ndarray, background: np.ndarray) -> bool:
    x0, y0, x1, y1 = component["bounds"]
    if component["area"] > CANDIDATE_MAX_AREA_PX:
        return False
    if x1 - x0 + 1 > CANDIDATE_MAX_EXTENT_PX or y1 - y0 + 1 > CANDIDATE_MAX_EXTENT_PX:
        return False
    points = component["points"]
    peak_red = int(rgb[points[:, 0], points[:, 1], 0].max())
    return peak_red - int(background[0]) >= CANDIDATE_MIN_RED_DELTA


def _frame_measurement(
    rgb: np.ndarray,
    background: np.ndarray,
    readback: dict[str, Any],
    frame: dict[str, Any],
) -> dict[str, Any]:
    height, width, _ = rgb.shape
    foreground = np.any(rgb.astype(np.int16) != background.reshape(1, 1, 3), axis=2)
    components = _components(foreground)
    main = components[0] if components else None
    main_points = main["points"] if main else np.empty((0, 2), dtype=np.int32)
    main_mask = _mask_from_points((height, width), main_points)

    red = rgb[..., 0].astype(np.float64)
    blade_mask = (
        main_mask
        & (red >= BLADE_MIN_RED)
        & (rgb[..., 1].astype(np.float64) >= red * BLADE_MIN_GREEN_TO_RED)
        & (rgb[..., 2].astype(np.float64) >= red * BLADE_MIN_BLUE_TO_RED)
    )
    blade_points = np.column_stack(np.nonzero(blade_mask)).astype(np.int32)

    candidates: list[dict[str, Any]] = []
    for component in components[1:]:
        if not _warm_candidate(component, rgb, background):
            continue
        points = component["points"]
        x0, y0, x1, y1 = component["bounds"]
        samples = rgb[points[:, 0], points[:, 1]]
        border_clearance = min(x0, y0, width - 1 - x1, height - 1 - y1)
        candidates.append(
            {
                "areaPixels": component["area"],
                "boundsPxInclusive": component["bounds"],
                "peakRgb": [int(value) for value in samples.max(axis=0)],
                "borderClearancePx": int(border_clearance),
                "touchesCanvasBorder": border_clearance == 0,
                "nearestMainEffectProxyPixelDistancePx": _distance_between(points, main_points),
                "nearestBladeProxyPixelDistancePx": _distance_between(points, blade_points),
                "sourceAttribution": UNATTRIBUTED,
            }
        )

    spark_live = _nonnegative_count(readback.get("sparkLiveCount"), "sparkLiveCount")
    dissipation_live = _nonnegative_count(
        readback.get("dissipationLiveCount"), "dissipationLiveCount"
    )
    recorder_total = spark_live + dissipation_live
    return {
        "index": int(frame["index"]),
        "timeSeconds": _finite_number(frame["time"], "frame.time"),
        "file": frame["file"],
        "sha256": frame["sha256"].lower(),
        "canvasPx": [width, height],
        "foregroundPixels": int(foreground.sum()),
        "mainEffectProxyBoundsPxInclusive": main["bounds"] if main else None,
        "mainEffectProxyPixels": main["area"] if main else 0,
        "bladeProxyPixels": int(blade_mask.sum()),
        "recorderContext": {
            "sparkLiveCount": spark_live,
            "dissipationLiveCount": dissipation_live,
            "unattributedRecorderLiveCount": recorder_total,
            "sourceAttribution": UNATTRIBUTED,
        },
        "detachedWarmCandidateComponentCount": len(candidates),
        "allDetectedWarmCandidatesDoNotTouchCanvasBorder": all(
            not item["touchesCanvasBorder"] for item in candidates
        ),
        "minimumDetachedWarmCandidateBorderClearancePx": min(
            (item["borderClearancePx"] for item in candidates), default=None
        ),
        "minimumDetachedWarmCandidateToBladeProxyDistancePx": min(
            (
                item["nearestBladeProxyPixelDistancePx"]
                for item in candidates
                if item["nearestBladeProxyPixelDistancePx"] is not None
            ),
            default=None,
        ),
        "detachedWarmCandidateComponents": candidates,
    }


def audit(authority_run: Path) -> dict[str, Any]:
    authority_run = authority_run.resolve()
    if authority_run.name != AUTHORITY_RUN_NAME:
        raise EvidenceInvalid("input is not the sole retained S15 authority run")
    metadata_path = authority_run / "metadata.json"
    if not metadata_path.is_file():
        raise EvidenceInvalid("authority metadata is missing")
    try:
        metadata = _strict_json(metadata_path)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        raise EvidenceInvalid("authority metadata is not strict JSON") from exc

    frames = metadata.get("frames")
    readback = metadata.get("liveParticleReadback")
    if not isinstance(frames, list) or len(frames) != 28:
        raise EvidenceInvalid("authority metadata must declare exactly 28 frames")
    if not isinstance(readback, list) or len(readback) != len(frames):
        raise EvidenceInvalid("liveParticleReadback must align with every frame")
    background = _declared_background(metadata)
    metadata_hash = _sha256(metadata_path)
    tool_hash = _sha256(Path(__file__).resolve())

    measurements: list[dict[str, Any]] = []
    dimensions: tuple[int, int] | None = None
    seen_names: set[str] = set()
    for expected_index, (frame, live) in enumerate(zip(frames, readback)):
        if not isinstance(frame, dict) or not isinstance(live, dict):
            raise EvidenceInvalid("frame/readback entry is not an object")
        if frame.get("index") != expected_index:
            raise EvidenceInvalid("frame indexes are not the exact sequence 0..27")
        file_name, expected_hash = frame.get("file"), frame.get("sha256")
        match = FRAME_FILE.fullmatch(file_name) if isinstance(file_name, str) else None
        if match is None or int(match.group(1)) != expected_index:
            raise EvidenceInvalid("frame file name/index is not canonical")
        if file_name in seen_names:
            raise EvidenceInvalid("frame file is replayed in metadata")
        seen_names.add(file_name)
        if not isinstance(expected_hash, str) or not SHA256.fullmatch(expected_hash):
            raise EvidenceInvalid("frame hash is not canonical SHA-256")
        frame_path = authority_run / file_name
        if not frame_path.is_file() or _sha256(frame_path).lower() != expected_hash.lower():
            raise EvidenceInvalid("missing or hash-mismatched authority frame: " + file_name)
        try:
            with Image.open(frame_path) as image:
                image.load()
                if image.format != "PNG":
                    raise EvidenceInvalid("authority frame is not PNG: " + file_name)
                rgb = np.asarray(image.convert("RGB"))
        except EvidenceInvalid:
            raise
        except Exception as exc:
            raise EvidenceInvalid("cannot decode authority frame: " + file_name) from exc
        current_dimensions = (int(rgb.shape[1]), int(rgb.shape[0]))
        if dimensions is None:
            dimensions = current_dimensions
        elif current_dimensions != dimensions:
            raise EvidenceInvalid("authority frames have inconsistent dimensions")
        if abs(_finite_number(live.get("time"), "readback.time") - _finite_number(frame.get("time"), "frame.time")) > 1e-6:
            raise EvidenceInvalid("frame and readback times do not align")
        measurements.append(_frame_measurement(rgb, background, live, frame))

    if dimensions != (960, 540):
        raise EvidenceInvalid("authority canvas is not 960x540")
    key_frames = [measurements[index] for index in KEY_FRAME_INDEXES]
    candidates = [
        candidate
        for frame in measurements
        for candidate in frame["detachedWarmCandidateComponents"]
    ]
    return {
        "schema": SCHEMA,
        "auditMode": "READ_ONLY_DERIVED_GEOMETRY_PROXY",
        "visualVerdict": "NOT_EVALUATED",
        "commercialUseVerdict": "NOT_AUTHORIZED_BY_THIS_AUDIT",
        "userSignedBoundary": "CONDITIONAL_PASS_CONTENT_OK_BUT_NOT_COMMERCIAL_USABLE",
        "supersedes": {
            "schema": "s15-wysiwyg-projection-audit/v1",
            "status": "SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO",
            "reason": "beauty RGB cannot attribute detached warm components to spark instances or establish a spark visibility ratio",
            "preservedJsonSha256": SUPERSEDED_V1_JSON_SHA256,
            "preservedReportSha256": SUPERSEDED_V1_REPORT_SHA256,
        },
        "authority": {
            "runName": authority_run.name,
            "metadataFile": "metadata.json",
            "metadataSha256": metadata_hash,
            "frameCount": len(measurements),
            "canvasPx": list(dimensions),
            "declaredBackgroundRgb8": [int(value) for value in background],
            "verifiedFrameSha256": [
                {"file": frame["file"], "sha256": frame["sha256"]}
                for frame in measurements
            ],
        },
        "measurementTool": {
            "path": "tools/vfx/s15_wysiwyg_projection_audit_v2.py",
            "sha256": tool_hash,
            "dependencies": {
                "python": ">=3.10",
                "numpy": "required",
                "Pillow": "required",
            },
        },
        "proxyDefinition": {
            "foreground": "RGB code differs in any channel from declared opaque camera background",
            "connectivity": 8,
            "mainEffect": "largest foreground connected component",
            "detachedWarmCandidate": {
                "notMainEffectComponent": True,
                "maxAreaPixels": CANDIDATE_MAX_AREA_PX,
                "maxWidthOrHeightPixels": CANDIDATE_MAX_EXTENT_PX,
                "minimumPeakRedDeltaFromBackground": CANDIDATE_MIN_RED_DELTA,
                "sourceAttribution": UNATTRIBUTED,
            },
            "mainBladeProxy": {
                "insideMainEffectComponent": True,
                "minimumRedCode": BLADE_MIN_RED,
                "minimumGreenToRedRatio": BLADE_MIN_GREEN_TO_RED,
                "minimumBlueToRedRatio": BLADE_MIN_BLUE_TO_RED,
            },
            "thresholdCalibrationStatus": "PENDING_HUMAN_OR_DIAGNOSTIC_CALIBRATION",
        },
        "machineStatuses": {
            "candidateSourceAttribution": UNATTRIBUTED,
            "trueSparkProjectionCanvasContainment": PENDING,
            "trueSparkBladeOverlap": PENDING,
            "trueSparkVisibility": PENDING,
            "trueSparkOcclusion": PENDING,
        },
        "limitations": {
            "recorderCountsAreContextOnly": True,
            "allRecorderLiveInstancesAreUnattributed": True,
            "unattributedAlternatives": [
                "off_canvas",
                "merged_with_main_effect",
                "occluded",
                "below_threshold",
                "dissipation_or_other_fragment",
                "foreign_source_component",
            ],
            "candidateNote": "a detached warm component may be a spark, dissipation, mesh fragment, anti-aliased island, or another source; no correspondence is inferred",
            "canvasNote": "border clearance applies only to pixels already detected inside the PNG and cannot detect an off-canvas projection",
            "reason": "beauty RGB has no instance ID, renderer ID, depth, or unoccluded projection pass",
        },
        "summary": {
            "evidenceIntegrity": "VERIFIED_28_OF_28",
            "detachedWarmCandidateComponentObservationsAcrossFrames": len(candidates),
            "allDetectedWarmCandidatesDoNotTouchCanvasBorder": all(
                not candidate["touchesCanvasBorder"] for candidate in candidates
            ),
            "minimumDetachedWarmCandidateBorderClearancePx": min(
                (candidate["borderClearancePx"] for candidate in candidates), default=None
            ),
            "trueSparkProjectionCanvasContainment": PENDING,
            "trueSparkBladeOverlap": PENDING,
            "trueSparkVisibility": PENDING,
            "trueSparkOcclusion": PENDING,
            "keyFrameIndexes": list(KEY_FRAME_INDEXES),
        },
        "keyFrames": key_frames,
        "frames": measurements,
    }


def render_markdown(report: dict[str, Any], json_sha256: str) -> str:
    authority = report["authority"]
    summary = report["summary"]
    lines = [
        "# S15 authority-frame detached-warm-component geometry audit v2",
        "",
        "> Status: **machine geometry proxy only; no spark attribution, visibility verdict, visual pass, or commercial-use pass is issued.** The signed user boundary remains: conditional pass, “内容还可以，但无法做商用”.",
        "",
        "## Supersession",
        "",
        "V1 is preserved byte-for-byte but is `SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO`. Its component-to-spark ratio interpretation is invalid because beauty RGB cannot identify particle instances. V1 must not be used as active evidence.",
        f"Preserved v1 JSON SHA-256: `{report['supersedes']['preservedJsonSha256']}`; report SHA-256: `{report['supersedes']['preservedReportSha256']}`.",
        "",
        "## Sealed input binding",
        "",
        f"- Authority run: `{authority['runName']}`; frames hash-verified: `{authority['frameCount']}/{authority['frameCount']}`.",
        f"- Metadata SHA-256: `{authority['metadataSha256']}`.",
        f"- Derived JSON SHA-256: `{json_sha256}`.",
        f"- V2 measurement tool SHA-256: `{report['measurementTool']['sha256']}`.",
        "- Authority metadata/PNGs and superseded v1 outputs are opened read-only or not opened; v2 writes only to its new derived directory.",
        "",
        "## Reproducible geometry proxy",
        "",
        "Foreground is every pixel differing from the declared `[19,21,24]` background. The largest 8-connected component is the main-effect proxy. A detached warm candidate is a non-main component at most 128 pixels and 32×32 pixels with peak red at least 64 codes above background. This says nothing about whether that component is a spark, dissipation, mesh fragment, anti-aliased island, or another source.",
        "",
        "Recorder spark and dissipation counts are copied only as context. Their sum is `unattributedRecorderLiveCount`; no RGB component is matched to any recorder instance.",
        "",
        "## Key-frame measurements",
        "",
        "| Frame | Time | Spark live context | Dissipation live context | Unattributed recorder live | Detached warm candidates | Min candidate border clearance | Nearest blade-proxy pixel |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for frame in report["keyFrames"]:
        context = frame["recorderContext"]
        nearest = frame["minimumDetachedWarmCandidateToBladeProxyDistancePx"]
        lines.append(
            "| `{index:04}` | `{time:.6f}` | {spark} | {dissipation} | {total} | {candidate} | {clearance} | {nearest} |".format(
                index=frame["index"],
                time=frame["timeSeconds"],
                spark=context["sparkLiveCount"],
                dissipation=context["dissipationLiveCount"],
                total=context["unattributedRecorderLiveCount"],
                candidate=frame["detachedWarmCandidateComponentCount"],
                clearance=(
                    frame["minimumDetachedWarmCandidateBorderClearancePx"]
                    if frame["minimumDetachedWarmCandidateBorderClearancePx"] is not None
                    else "—"
                ),
                nearest=f"{nearest:.3f} px" if nearest is not None else "—",
            )
        )
    frame20 = next(frame for frame in report["keyFrames"] if frame["index"] == 20)
    lines.extend(
        [
            "",
            "## Interpretation boundary",
            "",
            f"- Frame `0020` records spark context `{frame20['recorderContext']['sparkLiveCount']}`, dissipation context `{frame20['recorderContext']['dissipationLiveCount']}`, and therefore `{frame20['recorderContext']['unattributedRecorderLiveCount']}` unattributed live instances. RGB contains `{frame20['detachedWarmCandidateComponentCount']}` detached warm candidate; **no correspondence or ratio is inferred**.",
            f"- Detected warm candidate pixels do not touch the canvas border; the minimum measured clearance is `{summary['minimumDetachedWarmCandidateBorderClearancePx']} px`. This cannot reveal off-canvas projections.",
            f"- True spark canvas containment, blade overlap, visibility, and occlusion all remain `{PENDING}`.",
            "- Off-canvas, merged, occluded, below-threshold, dissipation/fragment, and foreign-source explanations cannot be distinguished from these sealed beauty RGB frames.",
            "- Thresholds remain `PENDING_HUMAN_OR_DIAGNOSTIC_CALIBRATION`. Family resemblance, legal provenance, licence scope, and commercial suitability are not evaluated; the user's restriction is unchanged.",
            "",
        ]
    )
    return "\n".join(lines)


def _write_once(path: Path, data: bytes) -> None:
    if path.exists():
        raise FileExistsError("refusing to overwrite derived v2 output: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--authority-run", required=True, type=Path)
    parser.add_argument("--json-output", type=Path)
    parser.add_argument("--markdown-output", type=Path)
    args = parser.parse_args(list(argv) if argv is not None else None)
    if bool(args.json_output) != bool(args.markdown_output):
        parser.error("--json-output and --markdown-output must be supplied together")
    report = audit(args.authority_run)
    json_bytes = (json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    if args.json_output:
        markdown = render_markdown(report, hashlib.sha256(json_bytes).hexdigest()).encode("utf-8")
        _write_once(args.json_output, json_bytes)
        _write_once(args.markdown_output, markdown)
    else:
        print(json.dumps(report["summary"], ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
