#!/usr/bin/env python3
"""W24 §13.2b diagnostic-render measurements (stdlib, NumPy and Pillow only).

Input is a JSON ``w24-render-metrics-input/v1`` document.  Every referenced
byte is hash-verified before it is decoded.  A malformed, missing, or changed
artifact returns an ``EVIDENCE_INVALID`` report rather than a failed metric.
This module intentionally has no Beauty-image decoder path for receiver-light
measurements and contains no aesthetic or commercial-quality vocabulary.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import deque
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, Sequence, Tuple

import numpy as np
from PIL import Image

SCHEMA = "w24-render-metrics-report/v1"
HASH_PREFIX = "sha256:"
TYPED_REPORT_ENCODING = "w24-typed-binary-v1"
_TYPED_DOMAIN = b"w24-typed-binary-v1\0"
_TYPED_MAX_DEPTH = 64
_TYPED_MAX_NODES = 100000
_TYPED_MAX_STRING_BYTES = 1024 * 1024
_TYPED_MAX_CONTAINER_ITEMS = 100000


class EvidenceInvalid(ValueError):
    """Raised for an evidence chain defect, never for a metric threshold."""


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return HASH_PREFIX + digest.hexdigest()


def _canonical_hash(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 71 and value.startswith(HASH_PREFIX) and all(c in "0123456789abcdef" for c in value[7:])


def _typed_binary_encode(value: Any) -> bytes:
    """Return the W24 v1 typed canonical payload (without its hash domain).

    Tags are null=0, false=1, true=2, int=3, double=4, string=5,
    array=6 and object=7. Variable byte strings use a big-endian u32 length;
    containers use a big-endian u32 item count. Integers are shortest decimal
    ASCII, while floats retain IEEE-754 bits so 1 and 1.0 cannot collide.
    """
    output = bytearray()
    nodes = 0

    def u32(number: int) -> None:
        if number < 0 or number > 0xFFFFFFFF:
            raise EvidenceInvalid("typed canonical length/count is invalid")
        output.extend(number.to_bytes(4, "big"))

    def utf8(text: str) -> bytes:
        try:
            encoded = text.encode("utf-8", "strict")
        except UnicodeEncodeError as exc:
            raise EvidenceInvalid("typed canonical string contains a lone surrogate") from exc
        if len(encoded) > _TYPED_MAX_STRING_BYTES:
            raise EvidenceInvalid("typed canonical string exceeds maximum UTF-8 byte length")
        return encoded

    def bytes_with_length(data: bytes) -> None:
        u32(len(data)); output.extend(data)

    def encode(item: Any, depth: int) -> None:
        nonlocal nodes
        if depth > _TYPED_MAX_DEPTH:
            raise EvidenceInvalid("typed canonical value exceeds maximum depth")
        nodes += 1
        if nodes > _TYPED_MAX_NODES:
            raise EvidenceInvalid("typed canonical value exceeds maximum node count")
        if item is None:
            output.append(0)
        elif isinstance(item, bool):
            output.append(2 if item else 1)
        elif isinstance(item, int):
            output.append(3); bytes_with_length(str(item).encode("ascii"))
        elif isinstance(item, float):
            if not math.isfinite(item):
                raise EvidenceInvalid("typed canonical double must be finite")
            import struct
            output.append(4); output.extend(struct.pack(">d", item))
        elif isinstance(item, str):
            output.append(5); bytes_with_length(utf8(item))
        elif isinstance(item, (list, tuple)):
            if len(item) > _TYPED_MAX_CONTAINER_ITEMS:
                raise EvidenceInvalid("typed canonical array has too many items")
            output.append(6); u32(len(item))
            for child in item: encode(child, depth + 1)
        elif isinstance(item, Mapping):
            if len(item) > _TYPED_MAX_CONTAINER_ITEMS:
                raise EvidenceInvalid("typed canonical object has too many fields")
            entries = []
            for key, child in item.items():
                if not isinstance(key, str):
                    raise EvidenceInvalid("typed canonical object keys must be strings")
                entries.append((utf8(key), child))
            entries.sort(key=lambda entry: entry[0])
            output.append(7); u32(len(entries))
            for key, child in entries:
                bytes_with_length(key); encode(child, depth + 1)
        else:
            raise EvidenceInvalid("typed canonical encoding rejects %s" % type(item).__name__)

    encode(value, 0)
    return bytes(output)


def typed_binary_hash(value: Any) -> str:
    return HASH_PREFIX + hashlib.sha256(_TYPED_DOMAIN + _typed_binary_encode(value)).hexdigest()


def _json_load_strict(text: str) -> Any:
    def reject_constant(value: str) -> Any:
        raise ValueError("non-finite JSON constant is forbidden: " + value)

    def unique_object(pairs: List[Tuple[str, Any]]) -> Dict[str, Any]:
        result: Dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON field: " + key)
            result[key] = value
        return result

    result = json.loads(text, parse_constant=reject_constant, object_pairs_hook=unique_object)
    _validate_strict_json_value(result)
    return result


def _validate_strict_json_value(value: Any) -> None:
    """Reject JSON values Python accepts but the cross-runtime protocol does not."""
    if isinstance(value, float):
        if not math.isfinite(value):
            raise ValueError("non-finite JSON float is forbidden")
    elif isinstance(value, str):
        try:
            value.encode("utf-8", "strict")
        except UnicodeEncodeError as exc:
            raise ValueError("JSON string contains a lone surrogate") from exc
    elif isinstance(value, Mapping):
        for key, child in value.items():
            if not isinstance(key, str):
                raise ValueError("JSON object key is not a string")
            _validate_strict_json_value(key)
            _validate_strict_json_value(child)
    elif isinstance(value, (list, tuple)):
        for child in value:
            _validate_strict_json_value(child)


def _as_path(root: Path, value: str) -> Path:
    path = (root / value).resolve()
    try:
        path.relative_to(root.resolve())
    except ValueError as exc:
        raise EvidenceInvalid("evidence path escapes input directory: %s" % value) from exc
    return path


class EvidenceStore:
    def __init__(self, root: Path, entries: Sequence[Mapping[str, Any]]):
        self.root = root
        self.entries: Dict[str, Mapping[str, Any]] = {}
        paths = set()
        for entry in entries:
            name = entry.get("id")
            if not isinstance(name, str) or not name:
                raise EvidenceInvalid("every evidence item needs a non-empty id")
            if name in self.entries:
                raise EvidenceInvalid("duplicate evidence id: " + name)
            path_value, expected = entry.get("path"), entry.get("sha256")
            if not isinstance(path_value, str) or not isinstance(expected, str):
                raise EvidenceInvalid("evidence %s needs path and sha256" % name)
            if not _canonical_hash(expected):
                raise EvidenceInvalid("evidence %s has non-canonical sha256" % name)
            path = _as_path(root, path_value)
            canonical_path = str(path).lower()
            if canonical_path in paths:
                raise EvidenceInvalid("duplicate evidence path replay: " + path_value)
            if not path.is_file():
                raise EvidenceInvalid("missing evidence: %s" % path_value)
            if _sha256(path) != expected:
                raise EvidenceInvalid("hash mismatch: %s" % path_value)
            self.entries[name] = entry
            paths.add(canonical_path)

    def meta(self, evidence_id: str) -> Mapping[str, Any]:
        try:
            return self.entries[evidence_id]
        except KeyError as exc:
            raise EvidenceInvalid("undeclared evidence id: " + evidence_id) from exc

    def array(self, evidence_id: str) -> np.ndarray:
        entry = self.meta(evidence_id)
        path = _as_path(self.root, str(entry["path"]))
        try:
            if path.suffix.lower() == ".npy":
                result = np.load(path, allow_pickle=False)
            else:
                result = np.asarray(Image.open(path))
        except Exception as exc:  # Pillow's exceptions vary by version.
            raise EvidenceInvalid("cannot decode %s" % entry["path"]) from exc
        if result.ndim not in (2, 3) or result.size == 0:
            raise EvidenceInvalid("invalid raster dimensions: %s" % entry["path"])
        return result


def _verify_capture_metadata(root: Path, descriptor: Mapping[str, Any], store: EvidenceStore) -> None:
    """Validate the recorder's existing W24 metadata/manifest artifact chain.

    Source hashes describe frozen Unity inputs which are normally outside the
    evidence directory; this verifies their required identities are present and
    canonical.  Every byte *inside* the supplied evidence directory is opened
    and SHA-256 verified here.
    """
    path_value, expected = descriptor.get("path"), descriptor.get("sha256")
    if not isinstance(path_value, str) or not _canonical_hash(expected):
        raise EvidenceInvalid("captureMetadata needs path and canonical sha256")
    path = _as_path(root, path_value)
    if not path.is_file() or _sha256(path) != expected:
        raise EvidenceInvalid("capture metadata missing or hash mismatched")
    try:
        metadata = _json_load_strict(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        raise EvidenceInvalid("capture metadata is not valid JSON") from exc
    if metadata.get("schema") != "w24-s0a-capture-evidence/v1":
        raise EvidenceInvalid("unsupported capture metadata schema")
    source_hashes = metadata.get("sourceHashes")
    if not isinstance(source_hashes, Mapping):
        raise EvidenceInvalid("capture metadata lacks sourceHashes")
    # The recorder serialises the four frozen source descriptors below.
    for name in ("scene", "prefab", "manifest", "captureTool"):
        item = source_hashes.get(name)
        if not isinstance(item, Mapping) or not _canonical_hash(item.get("sha256")):
            raise EvidenceInvalid("capture source hash missing: " + name)
    if not _canonical_hash(source_hashes["manifest"].get("buildHash")):
        raise EvidenceInvalid("capture source build hash missing")
    artifacts = []
    manifest = metadata.get("diagnosticPassManifest")
    if isinstance(manifest, Mapping): artifacts.append(manifest)
    else: raise EvidenceInvalid("capture metadata lacks diagnostic pass manifest")
    for frame in metadata.get("frames", []):
        if not isinstance(frame, Mapping): raise EvidenceInvalid("malformed capture frame")
        artifacts.append(frame.get("beauty"))
        artifacts.extend(frame.get("diagnostics", []))
    artifacts.extend(metadata.get("supplementalDiagnostics", []))
    if not artifacts: raise EvidenceInvalid("capture metadata has no artifacts")
    for artifact in artifacts:
        if not isinstance(artifact, Mapping) or not isinstance(artifact.get("file"), str) or not _canonical_hash(artifact.get("sha256")):
            raise EvidenceInvalid("capture metadata has malformed artifact")
        artifact_path = _as_path(root, artifact["file"])
        if not artifact_path.is_file() or _sha256(artifact_path) != artifact["sha256"]:
            raise EvidenceInvalid("capture artifact missing or hash mismatched: " + artifact["file"])
        matches = [entry for entry in store.entries.values() if entry["path"].replace("\\", "/") == artifact["file"].replace("\\", "/") and entry["sha256"] == artifact["sha256"]]
        if len(matches) != 1:
            raise EvidenceInvalid("capture artifact must bind exactly one evidence registry item: " + artifact["file"])

def _mask(value: np.ndarray, threshold: float = 0.0) -> np.ndarray:
    if value.ndim == 3:
        if value.shape[2] == 4:
            value = value[..., 3]
        else:
            value = np.max(value[..., :3], axis=2)
    return np.asarray(value > threshold, dtype=bool)


def _luminance(value: np.ndarray) -> np.ndarray:
    x = np.asarray(value, dtype=np.float64)
    if x.ndim == 2:
        return x
    if x.shape[2] == 1:
        return x[..., 0]
    return 0.2126 * x[..., 0] + 0.7152 * x[..., 1] + 0.0722 * x[..., 2]


def _pixel_scalar(value: np.ndarray) -> np.ndarray:
    """Use alpha for RGBA masks, otherwise linear luminance/value per pixel."""
    array = np.asarray(value)
    return np.asarray(array[..., 3] if array.ndim == 3 and array.shape[2] == 4 else _luminance(array), dtype=np.float64)


def _centroid(mask: np.ndarray) -> List[float] | None:
    ys, xs = np.nonzero(mask)
    if not len(xs):
        return None
    return [float(xs.mean()), float(ys.mean())]


def mask_statistics(mask_value: np.ndarray, luminance_value: np.ndarray | None = None) -> Dict[str, Any]:
    mask = _mask(mask_value)
    lum = _luminance(mask_value if luminance_value is None else luminance_value)
    samples = lum[mask]
    return {
        "areaPixels": int(mask.sum()),
        "centroidPx": _centroid(mask),
        "luminancePercentiles": ([float(x) for x in np.percentile(samples, [5, 50, 95])] if samples.size else [0.0, 0.0, 0.0]),
    }


def _line_slope(values: Sequence[float]) -> float:
    if len(values) < 2:
        return 0.0
    x = np.arange(len(values), dtype=np.float64)
    return float(np.polyfit(x, np.asarray(values, dtype=np.float64), 1)[0])


def steady_windows(frames: Sequence[np.ndarray], windows: Sequence[Sequence[int]], limits: Mapping[str, float]) -> Dict[str, Any]:
    if len(windows) != 3:
        raise ValueError("steady measurement requires exactly three windows")
    stats = [mask_statistics(frame) for frame in frames]
    result = []
    for indexes in windows:
        if not indexes or min(indexes) < 0 or max(indexes) >= len(stats):
            raise EvidenceInvalid("steady window references missing frame")
        areas = [stats[i]["areaPixels"] for i in indexes]
        p50 = [stats[i]["luminancePercentiles"][1] for i in indexes]
        cs = [stats[i]["centroidPx"] for i in indexes if stats[i]["centroidPx"] is not None]
        result.append({"areaMean": float(np.mean(areas)), "areaStd": float(np.std(areas)),
                       "luminanceP50Mean": float(np.mean(p50)), "areaSlopePerFrame": _line_slope(areas),
                       "centroidSlopePxPerFrame": (_line_slope([p[0] for p in cs]) if cs else 0.0,
                                                    _line_slope([p[1] for p in cs]) if cs else 0.0)})
    area_means = [x["areaMean"] for x in result]
    p50_means = [x["luminanceP50Mean"] for x in result]
    max_area_range = float(limits.get("maxAreaMeanRange", math.inf))
    max_lum_range = float(limits.get("maxLuminanceP50Range", math.inf))
    max_drift = float(limits.get("maxAbsAreaSlope", math.inf))
    passed = ((max(area_means) - min(area_means) <= max_area_range) and
              (max(p50_means) - min(p50_means) <= max_lum_range) and
              all(abs(x["areaSlopePerFrame"]) <= max_drift for x in result))
    return {"pass": bool(passed), "windows": result,
            "areaMeanRange": float(max(area_means) - min(area_means)),
            "luminanceP50MeanRange": float(max(p50_means) - min(p50_means))}


def autocorrelation(signal: Sequence[float], expected_period: int | None, min_correlation: float = 0.0, tolerance: int = 0) -> Dict[str, Any]:
    if expected_period is None:
        return {"pass": True, "status": "NOT_APPLICABLE_RANDOM_STEADY"}
    values = np.asarray(signal, dtype=np.float64)
    if len(values) < max(3, expected_period + 2):
        raise EvidenceInvalid("not enough frames for declared period")
    centered = values - values.mean()
    denom = float(np.dot(centered, centered))
    if denom == 0.0:
        correlations = [1.0 for _ in range(1, len(values))]
    else:
        correlations = [float(np.dot(centered[:-lag], centered[lag:]) / denom) for lag in range(1, len(values))]
    start, end = max(1, expected_period - tolerance), min(len(values) - 1, expected_period + tolerance)
    value = max(correlations[start - 1:end])
    observed = start + int(np.argmax(correlations[start - 1:end]))
    return {"pass": bool(value >= min_correlation), "expectedPeriodFrames": expected_period,
            "observedLagFrames": observed, "autocorrelation": value}


def _components(mask: np.ndarray) -> List[int]:
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    areas: List[int] = []
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]:
            continue
        queue, seen[y, x], area = deque([(int(y), int(x))]), True, 0
        while queue:
            cy, cx = queue.popleft(); area += 1
            for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True; queue.append((ny, nx))
        areas.append(area)
    return sorted(areas, reverse=True)


def cleanup(baseline_layers: Mapping[str, np.ndarray], after_layers: Mapping[str, np.ndarray], allowed: Iterable[str], max_mae: float, max_component_area: int) -> Dict[str, Any]:
    allowed_set = set(allowed)
    names = sorted((set(baseline_layers) | set(after_layers)) - allowed_set)
    if not names:
        return {"pass": True, "normalizedMae": 0.0, "residualComponentAreas": [], "measuredLayers": []}
    residuals = []
    for name in names:
        before, after = baseline_layers.get(name), after_layers.get(name)
        if before is None or after is None:
            raise EvidenceInvalid("cleanup layer missing from baseline or after: " + name)
        before_value, after_value = _pixel_scalar(before), _pixel_scalar(after)
        if before_value.shape != after_value.shape:
            raise EvidenceInvalid("cleanup dimensions differ: " + name)
        # Normalise bool/8-bit/HDR values into a per-pixel magnitude consistently.
        scale = max(1.0, float(np.max(np.abs(before_value))), float(np.max(np.abs(after_value))))
        residuals.append(np.abs(after_value - before_value) / scale)
    merged = np.maximum.reduce(residuals)
    residual_mask = merged > 0.0
    areas = _components(residual_mask)
    mae = float(np.mean(merged))
    return {"pass": bool(mae <= max_mae and (not areas or areas[0] <= max_component_area)),
            "normalizedMae": mae, "residualComponentAreas": areas, "measuredLayers": names}


def _zhang_suen(mask: np.ndarray) -> np.ndarray:
    """Small, dependency-free thinning; suitable for diagnostic trail masks."""
    image = _mask(mask).astype(np.uint8)
    changed = True
    while changed:
        changed = False
        for phase in (0, 1):
            remove: List[Tuple[int, int]] = []
            for y in range(1, image.shape[0] - 1):
                for x in range(1, image.shape[1] - 1):
                    if not image[y, x]: continue
                    p = [image[y-1, x], image[y-1, x+1], image[y, x+1], image[y+1, x+1], image[y+1, x], image[y+1, x-1], image[y, x-1], image[y-1, x-1]]
                    transitions = sum((p[i] == 0 and p[(i + 1) % 8] == 1) for i in range(8))
                    count = sum(p)
                    a, b = (p[0]*p[2]*p[4], p[2]*p[4]*p[6]) if phase == 0 else (p[0]*p[2]*p[6], p[0]*p[4]*p[6])
                    if transitions == 1 and 2 <= count <= 6 and a == 0 and b == 0: remove.append((y, x))
            if remove:
                changed = True
                for y, x in remove: image[y, x] = 0
    return image.astype(bool)


def trail_corridor(trail: np.ndarray, history_points: Sequence[Sequence[float]], radius_px: float, max_mean_distance: float, min_coverage: float, previous: np.ndarray | None = None, head_new_space: np.ndarray | None = None) -> Dict[str, Any]:
    skeleton = _zhang_suen(trail)
    if not history_points:
        raise EvidenceInvalid("trail measurement needs projected history points")
    yy, xx = np.indices(skeleton.shape)
    corridor = np.zeros_like(skeleton)
    for point in history_points:
        if len(point) != 2: raise EvidenceInvalid("history point must be [x,y]")
        corridor |= (xx - float(point[0])) ** 2 + (yy - float(point[1])) ** 2 <= radius_px ** 2
    sy, sx = np.nonzero(skeleton)
    if not len(sx): raise EvidenceInvalid("trail-only mask has no skeleton pixels")
    points = np.asarray(history_points, dtype=np.float64)
    dist = np.sqrt(np.min((sx[:, None] - points[None, :, 0]) ** 2 + (sy[:, None] - points[None, :, 1]) ** 2, axis=1))
    coverage = float(corridor[skeleton].mean())
    growth = 0
    if previous is not None and head_new_space is not None:
        if previous.shape != skeleton.shape or head_new_space.shape != skeleton.shape:
            raise EvidenceInvalid("static trail masks must have matching dimensions")
        growth = int((_mask(trail) & ~_mask(previous) & _mask(head_new_space)).sum())
    passed = coverage >= min_coverage and float(dist.mean()) <= max_mean_distance and growth == 0
    return {"pass": bool(passed), "skeletonPixels": int(skeleton.sum()), "corridorCoverage": coverage,
            "meanNearestHistoryDistancePx": float(dist.mean()), "headNewSpacePixels": growth}


def transition(before: Mapping[str, np.ndarray], after: Mapping[str, np.ndarray], mode: str, anchors_before: Mapping[str, Sequence[float]], anchors_after: Mapping[str, Sequence[float]], limits: Mapping[str, float]) -> Dict[str, Any]:
    if mode not in {"continuous", "impulse", "replace", "clear"}:
        raise EvidenceInvalid("unsupported continuityMode: " + mode)
    common = sorted(set(before) & set(after))
    anchor_distances = {name: float(np.linalg.norm(np.asarray(anchors_after[name]) - np.asarray(anchors_before[name])))
                        for name in anchors_before.keys() & anchors_after.keys()}
    max_anchor = float(limits.get("maxAnchorDistancePx", math.inf))
    if mode == "continuous":
        if not common: raise EvidenceInvalid("continuous transition has no layer pair")
        rows = []
        for layer in common:
            a, b = _mask(before[layer]), _mask(after[layer])
            if a.shape != b.shape: raise EvidenceInvalid("transition dimensions differ: " + layer)
            union = int((a | b).sum()); iou = float((a & b).sum() / union) if union else 1.0
            old_area = int(a.sum()); area_change = abs(int(b.sum()) - old_area) / max(1, old_area)
            rows.append({"layer": layer, "iou": iou, "areaChangeRatio": area_change})
        passed = min(x["iou"] for x in rows) >= float(limits.get("minIou", 0)) and max(x["areaChangeRatio"] for x in rows) <= float(limits.get("maxAreaChangeRatio", math.inf)) and all(x <= max_anchor for x in anchor_distances.values())
        return {"pass": bool(passed), "mode": mode, "layers": rows, "anchorDistancesPx": anchor_distances}
    if mode == "impulse":
        # Area discontinuity is permitted; only explicitly preserved anchors are measured.
        return {"pass": bool(all(x <= max_anchor for x in anchor_distances.values())), "mode": mode,
                "areaChangeAllowed": True, "anchorDistancesPx": anchor_distances}
    if mode == "replace":
        # Replacement may remove all IoU; enforce any supplied incoming-area floor instead.
        incoming = int(sum(_mask(x).sum() for x in after.values()))
        return {"pass": bool(incoming >= int(limits.get("minIncomingAreaPixels", 0))), "mode": mode,
                "incomingAreaPixels": incoming, "iouNotRequired": True}
    remaining = int(sum(_mask(x).sum() for x in after.values()))
    return {"pass": bool(remaining <= int(limits.get("maxRemainingAreaPixels", 0))), "mode": mode,
            "remainingAreaPixels": remaining, "anchorsNotRequired": True}


def receiver_luminance(on: np.ndarray, off: np.ndarray, receiver_ids: np.ndarray, receiver_id: int, effect_mask: np.ndarray, min_delta: float) -> Dict[str, Any]:
    if on.shape[:2] != off.shape[:2] or on.shape[:2] != receiver_ids.shape[:2] or on.shape[:2] != effect_mask.shape[:2]:
        raise EvidenceInvalid("receiver A/B dimensions differ")
    roi = (receiver_ids == receiver_id) & ~_mask(effect_mask)
    if not roi.any(): raise EvidenceInvalid("receiver ID has no pixels outside effect mask")
    delta = _luminance(on) - _luminance(off)
    value = float(delta[roi].mean())
    return {"pass": bool(value > min_delta), "receiverId": int(receiver_id), "outsideEffectPixels": int(roi.sum()), "linearLuminanceDelta": value}


def fragment_tracks(frames: Sequence[np.ndarray], fragment_ids: Sequence[int], max_trajectory_correlation: float = 0.98,
                    min_pairwise_distance_variation_ratio: float = 0.05, reject_single_rigid_body: bool = True) -> Dict[str, Any]:
    if len(frames) < 2 or len(fragment_ids) < 2:
        raise EvidenceInvalid("fragment tracking needs at least two frames and two fragment IDs")
    if not 0 <= max_trajectory_correlation <= 1 or min_pairwise_distance_variation_ratio < 0:
        raise EvidenceInvalid("fragment independence thresholds are invalid")
    if not reject_single_rigid_body:
        raise EvidenceInvalid("formal fragment tracking must reject a single rigid-body indication")
    tracks: Dict[str, Any] = {}
    for ident in fragment_ids:
        positions, angles = [], []
        for frame in frames:
            region = np.asarray(frame == ident)
            c = _centroid(region)
            if c is None: raise EvidenceInvalid("fragment ID missing: %s" % ident)
            ys, xs = np.nonzero(region); cov = np.cov(np.stack([xs, ys])) if len(xs) > 1 else np.eye(2)
            eigval, eigvec = np.linalg.eigh(cov); axis = eigvec[:, int(np.argmax(eigval))]
            positions.append(c); angles.append(float(math.atan2(axis[1], axis[0])))
        tracks[str(ident)] = {"centroidsPx": positions, "anglesRad": angles}
    vectors = []
    for item in tracks.values():
        xy = np.asarray(item["centroidsPx"], dtype=float)
        vectors.append(np.diff(xy, axis=0).reshape(-1))
    correlations = []
    for a in range(len(vectors)):
        for b in range(a + 1, len(vectors)):
            if np.std(vectors[a]) == 0 or np.std(vectors[b]) == 0: correlations.append(1.0)
            else: correlations.append(float(np.corrcoef(vectors[a], vectors[b])[0, 1]))
    pairwise_distance_variation = []
    track_values = list(tracks.values())
    for a in range(len(track_values)):
        first = np.asarray(track_values[a]["centroidsPx"], dtype=float)
        for b in range(a + 1, len(track_values)):
            second = np.asarray(track_values[b]["centroidsPx"], dtype=float)
            distance = np.linalg.norm(first - second, axis=1)
            mean_distance = float(np.mean(distance))
            variation = float((np.max(distance) - np.min(distance)) / max(mean_distance, 1e-6))
            pairwise_distance_variation.append(variation)
    max_positive_correlation = float(max(correlations, default=0.0))
    max_abs_correlation = float(max(map(abs, correlations), default=0.0))
    # A rigid translation or whole-group rotation preserves every pairwise centroid distance.
    # Independent fragments must change at least one pairwise relation; this catches the exact
    # "one image of fragments rotating as a whole" cheat without rejecting a radial burst merely
    # because opposite fragments have strongly negative velocity correlation.
    rigid_distance_indication = bool(pairwise_distance_variation and max(pairwise_distance_variation) < min_pairwise_distance_variation_ratio)
    highly_correlated_translation = bool(correlations and min(correlations) > max_trajectory_correlation)
    single_rigid_body = rigid_distance_indication or highly_correlated_translation
    # Independence is a property of the fragment set, not a ban on any two fragments ever
    # sharing a direction.  A real breakup may contain one coincidentally co-moving pair while
    # the remaining fragments change their pairwise relationships.  Reject only when the whole
    # set retains rigid pairwise distances or every trajectory is the same positive translation.
    passed = not single_rigid_body
    return {"pass": bool(passed), "authority": "cross_evidence_only", "tracks": tracks,
            "trajectoryCorrelation": {"pairwise": correlations, "maxAbs": max_abs_correlation,
                                      "maxPositive": max_positive_correlation,
                                      "limit": float(max_trajectory_correlation)},
            "pairwiseDistanceVariationRatio": {"pairwise": pairwise_distance_variation,
                                                "minimumRequired": float(min_pairwise_distance_variation_ratio)},
            "singleRigidBodyIndication": bool(single_rigid_body)}


def multiview_3d(views: Sequence[Mapping[str, np.ndarray]], object_id: int, carrier: str, min_depth_span: float, min_parallax: float, require_parallax: bool = False) -> Dict[str, Any]:
    if carrier.lower() == "billboard":
        return {"pass": True, "status": "BILLBOARD_CONTRACT_EXEMPT", "carrier": carrier}
    if len(views) < 2: raise EvidenceInvalid("3D measurement needs at least two views")
    centroids, spans, masks, depths, normal_spans = [], [], [], [], []
    for view in views:
        ids, depth = view["ids"], view["depth"]
        region = np.asarray(ids == object_id)
        if not region.any(): raise EvidenceInvalid("object ID missing in view")
        centroids.append(_centroid(region)); spans.append(float(np.max(depth[region]) - np.min(depth[region])))
        masks.append(region); depths.append(depth)
        if "normal" in view:
            normal = np.asarray(view["normal"], dtype=np.float64)
            if normal.shape[:2] != region.shape or normal.ndim != 3 or normal.shape[2] < 3:
                raise EvidenceInvalid("normal dimensions differ from object-ID")
            vectors = normal[region, :3]
            length = np.linalg.norm(vectors, axis=1)
            if np.any(length == 0):
                raise EvidenceInvalid("normal pass has zero normal on object-ID")
            unit = vectors / length[:, None]
            mean = unit.mean(axis=0); mean /= max(np.linalg.norm(mean), 1e-12)
            normal_spans.append(float(np.max(np.arccos(np.clip(unit @ mean, -1.0, 1.0)))))
    parallax = [float(np.linalg.norm(np.asarray(centroids[i]) - np.asarray(centroids[0]))) for i in range(1, len(centroids))]
    occlusion = []
    for i in range(1, len(masks)):
        overlap = masks[0] & masks[i]
        if overlap.any(): occlusion.append(float(np.mean(depths[i][overlap] - depths[0][overlap])))
    depth_ok = min(spans) >= min_depth_span
    parallax_ok = max(parallax, default=0.0) >= min_parallax
    passed = depth_ok and (parallax_ok if require_parallax else True)
    return {"pass": bool(passed), "carrier": carrier, "depthSpans": spans, "centroidsPx": centroids,
            "parallaxPx": parallax, "normalAngularSpansRad": normal_spans, "occlusionDepthDelta": occlusion,
            "depthSpanPass": bool(depth_ok), "parallaxPass": bool(parallax_ok), "parallaxRequired": require_parallax}


def _arrays(store: EvidenceStore, refs: Sequence[str]) -> List[np.ndarray]:
    return [store.array(str(ref)) for ref in refs]


def _layers(store: EvidenceStore, refs: Mapping[str, str]) -> Dict[str, np.ndarray]:
    return {str(name): store.array(str(ref)) for name, ref in refs.items()}


def _require_diagnostic(store: EvidenceStore, evidence_id: str, pass_id: str | Sequence[str] | None = None, encoding: str | None = None) -> None:
    meta = store.meta(evidence_id)
    if meta.get("kind") != "diagnostic":
        raise EvidenceInvalid("Beauty or undeclared artifact cannot be used as diagnostic evidence: " + evidence_id)
    accepted = (pass_id,) if isinstance(pass_id, str) else pass_id
    if accepted is not None and meta.get("passId") not in accepted:
        raise EvidenceInvalid("wrong diagnostic pass for %s" % evidence_id)
    if encoding is not None and meta.get("encoding") != encoding:
        raise EvidenceInvalid("wrong diagnostic encoding for %s" % evidence_id)


def _require_diagnostics(store: EvidenceStore, refs: Iterable[str], pass_id: str | Sequence[str], encoding: str) -> None:
    for ref in refs:
        _require_diagnostic(store, str(ref), pass_id, encoding)


def _numeric(value: np.ndarray, label: str) -> np.ndarray:
    array = np.asarray(value)
    if not np.issubdtype(array.dtype, np.number) or not np.isfinite(array).all():
        raise EvidenceInvalid(label + " contains non-finite or non-numeric values")
    return array


def _validate_mask(value: np.ndarray) -> None:
    array = _numeric(value, "mask")
    if array.ndim != 2 or np.min(array) < 0 or np.max(array) > 255 or not np.all((array == 0) | (array == 1) | (array == 255)):
        raise EvidenceInvalid("mask must be a 2D binary 0/1 or 0/255 diagnostic pass")


def _validate_ids(value: np.ndarray, label: str = "ID") -> None:
    array = np.asarray(value)
    if array.ndim != 2 or not np.issubdtype(array.dtype, np.integer) or np.any(array < 0):
        raise EvidenceInvalid(label + " pass must be a non-negative 2D integer/lossless array")


def _validate_depth(value: np.ndarray) -> None:
    array = _numeric(value, "depth")
    if array.ndim != 2 or not np.issubdtype(array.dtype, np.floating):
        raise EvidenceInvalid("depth pass must be a finite 2D floating array")


def _validate_normal(value: np.ndarray) -> None:
    array = _numeric(value, "normal")
    if array.ndim != 3 or array.shape[2] != 3 or not np.issubdtype(array.dtype, np.floating) or np.min(array) < -1.001 or np.max(array) > 1.001:
        raise EvidenceInvalid("normal pass must be finite HxWx3 float data in [-1,1]")


def _validate_hdr(value: np.ndarray) -> None:
    array = _numeric(value, "linear HDR")
    if array.ndim != 3 or array.shape[2] < 3 or not np.issubdtype(array.dtype, np.floating) or np.any(array < 0):
        raise EvidenceInvalid("linear HDR pass must be finite non-negative HxWx3+ float data")


def _validate_ldr(value: np.ndarray) -> None:
    array = _numeric(value, "linear LDR")
    if array.ndim != 3 or array.shape[2] < 3 or not np.issubdtype(array.dtype, np.floating) or np.any(array < 0) or np.any(array > 1):
        raise EvidenceInvalid("linear LDR pass must be finite HxWx3+ floating data in [0,1]")


def _one_check(store: EvidenceStore, spec: Mapping[str, Any]) -> Dict[str, Any]:
    kind = spec.get("kind")
    if kind == "mask_steady":
        _require_diagnostics(store, spec["frames"], ("effect-mask", "layer-mask"), "mask_binary")
        frames = _arrays(store, spec["frames"]); [_validate_mask(x) for x in frames]; basic = [mask_statistics(x) for x in frames]
        result = steady_windows(frames, spec["windows"], spec.get("limits", {}))
        result["frameStatistics"] = basic
    elif kind == "autocorrelation":
        _require_diagnostics(store, spec["frames"], ("effect-mask", "layer-mask"), "mask_binary")
        frames = _arrays(store, spec["frames"]); [_validate_mask(x) for x in frames]; result = autocorrelation([_mask(x).sum() for x in frames], spec.get("expectedPeriodFrames"), float(spec.get("minCorrelation", 0)), int(spec.get("toleranceFrames", 0)))
    elif kind == "cleanup":
        refs = list(spec["baselineLayers"].values()) + list(spec["afterLayers"].values()); _require_diagnostics(store, refs, "layer-mask", "mask_binary")
        baseline, after = _layers(store, spec["baselineLayers"]), _layers(store, spec["afterLayers"]); [_validate_mask(x) for x in list(baseline.values()) + list(after.values())]
        result = cleanup(baseline, after, spec.get("allowedResidualLayers", []), float(spec["maxNormalizedMae"]), int(spec["maxResidualComponentArea"]))
    elif kind == "trail":
        refs = [spec["trail"]] + ([spec["previous"]] if spec.get("previous") else []); _require_diagnostics(store, refs, "trail-only-mask", "mask_binary")
        if spec.get("headNewSpace"): _require_diagnostic(store, spec["headNewSpace"], "head-new-space-mask", "mask_binary")
        trail, previous, head = store.array(spec["trail"]), store.array(spec["previous"]) if spec.get("previous") else None, store.array(spec["headNewSpace"]) if spec.get("headNewSpace") else None; [_validate_mask(x) for x in (trail, previous, head) if x is not None]
        result = trail_corridor(trail, spec["historyProjectedPx"], float(spec["radiusPx"]), float(spec["maxMeanNearestDistancePx"]), float(spec["minCorridorCoverage"]), previous, head)
    elif kind == "transition":
        refs = list(spec["beforeLayers"].values()) + list(spec["afterLayers"].values()); _require_diagnostics(store, refs, "layer-mask", "mask_binary")
        before, after = _layers(store, spec["beforeLayers"]), _layers(store, spec["afterLayers"]); [_validate_mask(x) for x in list(before.values()) + list(after.values())]
        result = transition(before, after, str(spec["continuityMode"]), spec.get("anchorsBefore", {}), spec.get("anchorsAfter", {}), spec.get("limits", {}))
    elif kind == "receiver_luminance":
        for key in ("on", "off", "receiverIds", "effectMask"):
            _require_diagnostic(store, spec[key], "receiver-linear-hdr" if key in ("on", "off") else ("receiver-id" if key == "receiverIds" else "effect-mask"), "linear_hdr" if key in ("on", "off") else ("id_uint" if key == "receiverIds" else "mask_binary"))
        on, off, ids, effect = store.array(spec["on"]), store.array(spec["off"]), store.array(spec["receiverIds"]), store.array(spec["effectMask"]); _validate_hdr(on); _validate_hdr(off); _validate_ids(ids, "receiver ID"); _validate_mask(effect)
        result = receiver_luminance(on, off, ids, int(spec["receiverId"]), effect, float(spec["minLinearLuminanceDelta"]))
    elif kind == "receiver_luminance_ldr":
        for key in ("on", "off", "receiverIds", "effectMask"):
            _require_diagnostic(store, spec[key], "receiver-linear-ldr" if key in ("on", "off") else ("receiver-id" if key == "receiverIds" else "effect-mask"), "linear_ldr" if key in ("on", "off") else ("id_uint" if key == "receiverIds" else "mask_binary"))
        on, off, ids, effect = store.array(spec["on"]), store.array(spec["off"]), store.array(spec["receiverIds"]), store.array(spec["effectMask"]); _validate_ldr(on); _validate_ldr(off); _validate_ids(ids, "receiver ID"); _validate_mask(effect)
        result = receiver_luminance(on, off, ids, int(spec["receiverId"]), effect, float(spec["minLinearLuminanceDelta"]))
    elif kind == "fragment_tracks":
        _require_diagnostics(store, spec["frames"], "fragment-id", "id_uint")
        frames = _arrays(store, spec["frames"]); [_validate_ids(x, "fragment ID") for x in frames]; result = fragment_tracks(frames, [int(x) for x in spec["fragmentIds"]], float(spec["maxTrajectoryCorrelation"]), float(spec["minPairwiseDistanceVariationRatio"]), bool(spec["rejectSingleRigidBody"]))
    elif kind == "multiview_3d":
        for view in spec["views"]:
            _require_diagnostic(store, view["objectIds"], "object-id", "id_uint"); _require_diagnostic(store, view["depth"], "depth-linear", "linear_float")
            if view.get("normal"): _require_diagnostic(store, view["normal"], "normal", "normal_float")
        views = [{"ids": store.array(v["objectIds"]), "depth": store.array(v["depth"]), **({"normal": store.array(v["normal"])} if v.get("normal") else {})} for v in spec["views"]]
        for view in views: _validate_ids(view["ids"], "object ID"); _validate_depth(view["depth"]); _validate_normal(view["normal"]) if "normal" in view else None
        result = multiview_3d(views, int(spec["objectId"]), str(spec["carrier"]), float(spec.get("minDepthSpan", 0)), float(spec.get("minParallaxPx", 0)), bool(spec.get("requireParallax", False)))
    else: raise EvidenceInvalid("unsupported metric kind: %r" % kind)
    return {"id": spec.get("id", kind), "kind": kind, **result}


def _canonical_hash_json(value: Any) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return HASH_PREFIX + hashlib.sha256(payload).hexdigest()


def _seal(report: Dict[str, Any], document: Any) -> Dict[str, Any]:
    report["inputSha256"] = _canonical_hash_json(document)
    report["toolSha256"] = _sha256(Path(__file__).resolve())
    report["sealedReportEncoding"] = TYPED_REPORT_ENCODING
    report["sealedReportHash"] = typed_binary_hash(report)
    return report


def run_report(document: Mapping[str, Any], root: Path) -> Dict[str, Any]:
    """Run a document; errors in evidence are one report-level invalid route."""
    try:
        if not isinstance(document, Mapping): raise EvidenceInvalid("input document must be an object")
        _validate_strict_json_value(document)
        if document.get("schema") != "w24-render-metrics-input/v1": raise EvidenceInvalid("unsupported input schema")
        if not isinstance(document.get("checks"), list) or not document["checks"]: raise EvidenceInvalid("at least one metric check is required")
        store = EvidenceStore(root, document.get("evidence", []))
        if "captureMetadata" in document:
            descriptor = document["captureMetadata"]
            if not isinstance(descriptor, Mapping):
                raise EvidenceInvalid("captureMetadata must be an object")
            _verify_capture_metadata(root, descriptor, store)
        checks = [_one_check(store, item) for item in document.get("checks", [])]
        return _seal({"schema": SCHEMA, "route": "MEASURED", "machineGatesPassed": bool(all(x["pass"] for x in checks)), "checks": checks}, document)
    except (EvidenceInvalid, KeyError, TypeError, ValueError) as exc:
        return _seal({"schema": SCHEMA, "route": "EVIDENCE_INVALID", "machineGatesPassed": False,
                      "reason": str(exc), "checks": []}, document)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="W24 diagnostic-render numeric measurement only")
    parser.add_argument("input", type=Path, help="w24-render-metrics-input/v1 JSON")
    parser.add_argument("--output", type=Path, required=True, help="report JSON output")
    args = parser.parse_args(argv)
    try:
        document = _json_load_strict(args.input.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        report = _seal({"schema": SCHEMA, "route": "EVIDENCE_INVALID", "machineGatesPassed": False, "reason": "invalid input JSON: " + str(exc), "checks": []}, {})
    else:
        report = run_report(document, args.input.parent)
    if args.output.exists():
        parser.error("refusing to overwrite existing report: %s" % args.output)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
