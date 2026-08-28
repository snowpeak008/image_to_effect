"""Deterministically export the Frost family runtime atlases from ArtSource.

The current AI source images are RGB files with a baked light checkerboard even
though transparency was requested.  They are retained under RawGenerated for
traceability.  This exporter derives a conservative blue/cyan foreground matte,
tight-crops each semantic module, and packs stable runtime cells.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource" / "VFX" / "Frost" / "RawGenerated"
MODULES = ROOT / "ArtSource" / "VFX" / "Frost" / "Modules"
LAYOUT = ROOT / "ArtSource" / "VFX" / "Frost" / "AtlasLayout"
RUNTIME = ROOT / "project" / "Assets" / "VFX" / "Shared" / "Frost" / "Textures"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def foreground_matte(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGB")
    rgb = np.asarray(image, dtype=np.float32)
    red, green, blue = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mean = (red + green + blue) / 3.0

    # The baked checker is nearly neutral and brighter than 240.  Frost pixels
    # are blue/cyan or substantially darker.  Requiring blue dominance prevents
    # the neutral checker cells from becoming an opaque square.
    blue_bias = blue - (red + green) * 0.5
    cyan_bias = (green + blue) * 0.5 - red
    chroma = np.maximum(blue_bias, cyan_bias * 0.65)
    dark_blue = np.clip((242.0 - mean) / 90.0, 0.0, 1.0) * np.clip((blue - red + 5.0) / 30.0, 0.0, 1.0)
    alpha = np.maximum(np.clip((chroma - 2.0) / 28.0, 0.0, 1.0), dark_blue)
    alpha = np.power(alpha, 0.82)

    matte = Image.fromarray(np.uint8(np.clip(alpha * 255.0, 0.0, 255.0)), "L")
    matte = matte.filter(ImageFilter.MedianFilter(3))
    rgba = image.convert("RGBA")
    rgba.putalpha(matte)
    pixels = np.asarray(rgba).copy()
    pixels[pixels[..., 3] < 2, :3] = 0
    pixels[pixels[..., 3] < 2, 3] = 0
    return Image.fromarray(pixels, "RGBA")


def tight_crop(image: Image.Image, threshold: int = 5, padding: int = 10) -> Image.Image:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= threshold else 0)
    box = mask.getbbox()
    if box is None:
        raise RuntimeError("No foreground pixels found while building Frost atlas.")
    left = max(0, box[0] - padding)
    top = max(0, box[1] - padding)
    right = min(image.width, box[2] + padding)
    bottom = min(image.height, box[3] + padding)
    return image.crop((left, top, right, bottom))


def fitted(image: Image.Image, size: tuple[int, int], padding: int) -> Image.Image:
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    available = (size[0] - padding * 2, size[1] - padding * 2)
    scale = min(available[0] / image.width, available[1] / image.height)
    scaled = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.LANCZOS)
    result.alpha_composite(scaled, ((size[0] - scaled.width) // 2, (size[1] - scaled.height) // 2))
    return result


def core_flash(size: int = 256) -> Image.Image:
    axis = np.linspace(-1.0, 1.0, size, dtype=np.float32)
    x, y = np.meshgrid(axis, axis)
    radius = np.sqrt(x * x + y * y)
    angle = np.arctan2(y, x)
    center = np.clip(1.0 - radius, 0.0, 1.0) ** 3.1
    rays = (np.abs(np.cos(angle * 4.0)) ** 28.0) * np.clip(1.0 - radius, 0.0, 1.0) ** 1.7
    alpha = np.clip(center + rays * 0.72, 0.0, 1.0)
    color = np.zeros((size, size, 4), dtype=np.uint8)
    color[..., 0] = np.uint8(190 + 65 * center)
    color[..., 1] = np.uint8(235 + 20 * center)
    color[..., 2] = 255
    color[..., 3] = np.uint8(alpha * 255)
    return Image.fromarray(color, "RGBA")


def snow_mote(size: int = 256) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    cx = cy = size // 2
    draw.ellipse((cx - 16, cy - 16, cx + 16, cy + 16), fill=(180, 242, 255, 170))
    for length, width, alpha in ((68, 4, 205), (48, 7, 150)):
        draw.line((cx - length, cy, cx + length, cy), fill=(215, 250, 255, alpha), width=width)
        draw.line((cx, cy - length, cx, cy + length), fill=(215, 250, 255, alpha), width=width)
    return image.filter(ImageFilter.GaussianBlur(2.2))


def grade_frost(image: Image.Image, low: tuple[int, int, int], high: tuple[int, int, int]) -> Image.Image:
    pixels = np.asarray(image, dtype=np.float32).copy()
    alpha = pixels[..., 3:4] / 255.0
    low_color = np.asarray(low, dtype=np.float32).reshape((1, 1, 3))
    high_color = np.asarray(high, dtype=np.float32).reshape((1, 1, 3))
    # Rebuild color entirely from the cleaned matte.  The AI source has a baked
    # light checkerboard, so retaining source RGB would preserve a visible grid.
    pixels[..., :3] = low_color * (1.0 - alpha) + high_color * alpha
    pixels[..., :3] = np.where(alpha > 0.005, pixels[..., :3], 0.0)
    return Image.fromarray(np.uint8(np.clip(pixels, 0.0, 255.0)), "RGBA")


def export() -> None:
    MODULES.mkdir(parents=True, exist_ok=True)
    LAYOUT.mkdir(parents=True, exist_ok=True)
    RUNTIME.mkdir(parents=True, exist_ok=True)

    ring = grade_frost(tight_crop(foreground_matte(SOURCE / "FrostBrokenRing_raw_v1.png")), (45, 128, 205), (238, 252, 255))
    mist = grade_frost(tight_crop(foreground_matte(SOURCE / "FrostMistRing_raw_v1.png")), (22, 72, 135), (168, 225, 255))
    shards = foreground_matte(SOURCE / "FrostShardVariants_raw_v1.png")

    ring.save(MODULES / "FrostBrokenRing_clean_v1.png", optimize=True)
    mist.save(MODULES / "FrostMistRing_clean_v1.png", optimize=True)

    impact = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    # Pillow uses a top-left origin; Unity UV cells below use a bottom-left origin.
    impact.alpha_composite(fitted(core_flash(), (256, 256), 12), (0, 0))
    impact.alpha_composite(fitted(snow_mote(), (256, 256), 24), (256, 0))
    impact.alpha_composite(fitted(ring, (256, 256), 8), (0, 256))
    impact.alpha_composite(fitted(mist, (256, 256), 8), (256, 256))
    impact_path = RUNTIME / "T_Frost_ImpactAtlas_A_v1.png"
    impact.save(impact_path, optimize=True)

    shard_atlas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    cell_width = shards.width // 3
    cell_height = shards.height // 2
    selected = ((0, 0), (1, 0), (2, 0), (1, 1))
    clean_shards: list[Image.Image] = []
    for source_x, source_y in selected:
        cell = shards.crop((source_x * cell_width, source_y * cell_height, (source_x + 1) * cell_width, (source_y + 1) * cell_height))
        # Source cells are authored +Y.  Unity's stretched billboard length axis
        # is +X, so rotate once during deterministic export instead of guessing
        # per-particle Euler angles at runtime.
        clean_shards.append(tight_crop(cell, threshold=5, padding=5).transpose(Image.Transpose.ROTATE_270))
    for index, shard in enumerate(clean_shards):
        x = (index % 2) * 128
        y = (index // 2) * 128
        shard_atlas.alpha_composite(fitted(shard, (128, 128), 7), (x, y))
        shard.save(MODULES / f"FrostShard_{index + 1:02d}_clean_v1.png", optimize=True)
    shard_path = RUNTIME / "T_Frost_ShardAtlas_A_v1.png"
    shard_atlas.save(shard_path, optimize=True)

    contract = {
        "contractVersion": 1,
        "family": "frost",
        "directionalConvention": "Runtime shard tip points +X in every cell; Stretch billboard aligns +X to radial velocity.",
        "sourceFiles": [
            {"path": str(path.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(path)}
            for path in sorted(SOURCE.glob("*.png"))
        ],
        "atlases": [
            {
                "path": str(impact_path.relative_to(ROOT)).replace("\\", "/"),
                "size": [512, 512],
                "sha256": sha256(impact_path),
                "cells": {
                    "core_flash": {"rect": [0.0, 0.5, 0.5, 0.5], "pivot": [0.5, 0.5]},
                    "snow_mote": {"rect": [0.5, 0.5, 0.5, 0.5], "pivot": [0.5, 0.5]},
                    "broken_ring": {"rect": [0.0, 0.0, 0.5, 0.5], "pivot": [0.5, 0.5]},
                    "mist_ring": {"rect": [0.5, 0.0, 0.5, 0.5], "pivot": [0.5, 0.5]},
                },
            },
            {
                "path": str(shard_path.relative_to(ROOT)).replace("\\", "/"),
                "size": [256, 256],
                "sha256": sha256(shard_path),
                "grid": [2, 2],
                "cells": ["shard_01", "shard_02", "shard_03", "shard_04"],
            },
        ],
    }
    (LAYOUT / "FrostFamilyAtlas_A_v1.layout.json").write_text(json.dumps(contract, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    export()
