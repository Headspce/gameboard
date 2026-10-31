#!/usr/bin/env python3
"""Produce clean (ink-free) cloud textures for the GameBoard Unity scene.

Source: Tyler's sketch photo (clouds + dark-green ink linework).
The ink is dark and green-dominant, while the clouds are blue-gray / warm
cream, so the ink can be masked by hue + local contrast, then diffusion-
filled from surrounding cloud pixels. Outputs (in Assets/Textures/):
  clouds.png       - clean 16:9 clouds (upper/mid band), RGB
  clouds_near.png  - clean 16:9 clouds (lower band), RGBA with an alpha
                     gradient (transparent at top -> opaque) for the near
                     parallax layer.
Both are single non-repeating textures; the Unity importer is set to
Clamp wrap in the scene builder.
"""
import numpy as np
from PIL import Image, ImageFilter

SRC = "/home/hatch/workspace/user/files/2512_0_9scr.png"
OUT_FAR = "/home/hatch/workspace/gameboard-unity/Assets/Textures/clouds.png"
OUT_NEAR = "/home/hatch/workspace/gameboard-unity/Assets/Textures/clouds_near.png"


def build_mask(a: np.ndarray) -> np.ndarray:
    """Boolean mask, True = ink / unknown pixel to fill."""
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    V = a.max(axis=2)
    green_dom = (G > B + 6) & (G >= R - 2) & (V < 185)
    # local-contrast pass: much darker than the blurred neighborhood
    v_img = Image.fromarray(V.astype(np.uint8))
    local_mean = np.asarray(v_img.filter(ImageFilter.GaussianBlur(9))).astype(int)
    dark_line = (local_mean - V > 22) & (V < 200)
    mask = green_dom | dark_line
    # dilate to swallow anti-aliased fringes
    m = Image.fromarray((mask.astype(np.uint8)) * 255)
    m = m.filter(ImageFilter.MaxFilter(7))
    return np.asarray(m) > 0


def diffuse_fill(img: np.ndarray, mask: np.ndarray) -> np.ndarray:
    """Fill masked pixels by successive Gaussian diffusion of known colors."""
    known = ~mask
    cur = img.copy()
    cur[mask] = img[known].mean(axis=0)
    for sigma in (24, 12, 6, 3, 1.5):
        for _ in range(3):
            blurred = np.asarray(
                Image.fromarray((np.clip(cur, 0, 1) * 255).astype(np.uint8)).filter(
                    ImageFilter.GaussianBlur(sigma)
                )
            ).astype(np.float32) / 255.0
            cur = np.where(known[:, :, None], img, blurred)
    return cur


def patch_board_region(clean: np.ndarray) -> np.ndarray:
    """Cover the board's translucent-fill footprint with real cloud texture.

    Diffusion fill removes the ink, but the board interior was drawn as a
    pale translucent wash over the clouds, leaving a faint trapezoid of
    lighter tone. Replace that rectangle with a vertically flipped strip of
    genuine clouds from just above the board, feather-blended at the seams.
    """
    out = clean.copy()
    # dest rectangle (y0:y1, x0:x1) covering the board footprint + labels
    y0, y1, x0, x1 = 398, 682, 158, 792
    h, w = y1 - y0, x1 - x0
    # source strip: same size, from above the board, shifted for variety
    sy1, sx0 = 398, 98
    src = clean[sy1 - h : sy1, sx0 : sx0 + w][::-1, :, :]  # vertical flip
    # feathered alpha (ramps in from every edge)
    fy = np.minimum(np.arange(h) / 28.0, 1.0)
    fy = np.minimum(fy, np.minimum((h - 1 - np.arange(h)) / 28.0, 1.0))
    fx = np.minimum(np.arange(w) / 28.0, 1.0)
    fx = np.minimum(fx, np.minimum((w - 1 - np.arange(w)) / 28.0, 1.0))
    alpha = np.outer(fy, fx)[:, :, None]
    out[y0:y1, x0:x1] = out[y0:y1, x0:x1] * (1 - alpha) + src * alpha
    return out


def main() -> None:
    im = Image.open(SRC).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255.0
    mask = build_mask((a * 255).astype(int))
    print(f"mask covers {mask.mean() * 100:.2f}% of pixels")
    clean = diffuse_fill(a, mask)
    clean = patch_board_region(clean)
    clean_img = Image.fromarray((np.clip(clean, 0, 1) * 255).astype(np.uint8))
    clean_img.save("/tmp/clouds_clean_full.png")

    W, H = clean_img.size  # 882 x 868
    band_h = int(W * 9 / 16)  # 496 -> 16:9

    # Far layer: upper/mid band (below the 'Game Board' handwriting).
    far = clean_img.crop((0, 150, W, 150 + band_h)).resize((1764, 992), Image.LANCZOS)
    far.save(OUT_FAR)
    print("wrote", OUT_FAR, far.size)

    # Near layer: lower band, alpha fades in from the top edge.
    near = clean_img.crop((0, 330, W, 330 + band_h)).resize((1764, 992), Image.LANCZOS)
    near = near.convert("RGBA")
    alpha = np.zeros((992, 1764), dtype=np.uint8)
    fade_end = int(992 * 0.38)
    alpha[:fade_end, :] = np.linspace(0, 255, fade_end, dtype=np.uint8)[:, None]
    alpha[fade_end:, :] = 255
    near.putalpha(Image.fromarray(alpha))
    near.save(OUT_NEAR)
    print("wrote", OUT_NEAR, near.size)


if __name__ == "__main__":
    main()
