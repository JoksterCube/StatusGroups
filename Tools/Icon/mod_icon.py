# Builds the StatusGroups icon on top of the shared base (Thunderstore/icon_base.png): a corner badge with three status icons stacked into one group.
# The badge frame matches FairCraftingLuck's clover badge so the mod icons read as one family.
# Usage (needs Pillow): python mod_icon.py <icon_base.png> <output icon.png> [preview.png]
import sys
from PIL import Image, ImageDraw, ImageFilter

W, SS = 256, 4
DARK = (24, 20, 16, 255)
GOLD = (176, 132, 62, 255)
GOLD_HI = (214, 172, 92, 255)
GREEN = (92, 150, 62, 255)
GREEN_HI = (128, 184, 86, 255)

def badge_frame(d, X, Y, R):
    # dark rim, gold ring, dark line, cream face
    d.ellipse([X - R, Y - R, X + R, Y + R], fill=DARK)
    R2 = R - 3.5 * SS
    d.ellipse([X - R2, Y - R2, X + R2, Y + R2], fill=GOLD)
    R3 = R2 - 5 * SS
    d.ellipse([X - R3, Y - R3, X + R3, Y + R3], fill=DARK)
    R4 = R3 - 2.5 * SS
    d.ellipse([X - R4, Y - R4, X + R4, Y + R4], fill=(238, 226, 190, 255))

def tile(d, x0, y0, size, face, hi):
    # one status icon: dark outline, gold rim, colored face with a lighter top half
    r = 4 * SS
    d.rounded_rectangle([x0 - 2 * SS, y0 - 2 * SS, x0 + size + 2 * SS, y0 + size + 2 * SS], r + 2 * SS, fill=DARK)
    d.rounded_rectangle([x0, y0, x0 + size, y0 + size], r, fill=GOLD)
    inset = 3 * SS
    d.rounded_rectangle([x0 + inset, y0 + inset, x0 + size - inset, y0 + size - inset], r - SS, fill=face)
    d.rounded_rectangle([x0 + inset, y0 + inset, x0 + size - inset, y0 + size / 2], r - SS, fill=hi)

def stack(layer, X, Y):
    # three status icons stacked into one group, back to front
    d = ImageDraw.Draw(layer)
    size = 26 * SS
    step = 8 * SS
    faces = [((70, 96, 140, 255), (98, 126, 170, 255)), ((150, 70, 60, 255), (182, 98, 84, 255)), (GREEN, GREEN_HI)]
    for i, (face, hi) in enumerate(faces):
        off = (i - 1) * step
        tile(d, X - size / 2 + off, Y - size / 2 - off, size, face, hi)
    # a small mead flask on the front icon
    fx, fy = X + step - 0 * SS, Y - step + 2 * SS
    d.ellipse([fx - 7 * SS, fy - 3 * SS, fx + 7 * SS, fy + 11 * SS], fill=DARK)
    d.ellipse([fx - 5 * SS, fy - 1 * SS, fx + 5 * SS, fy + 9 * SS], fill=(238, 226, 190, 255))
    d.rectangle([fx - 2.5 * SS, fy - 8 * SS, fx + 2.5 * SS, fy], fill=DARK)
    d.rectangle([fx - 1.2 * SS, fy - 7 * SS, fx + 1.2 * SS, fy], fill=(238, 226, 190, 255))

def stack_badge(img, cx=204, cy=204, r=40):
    layer = Image.new("RGBA", (W * SS, W * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    X, Y, R = cx * SS, cy * SS, r * SS
    sh = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse([X - R, Y - R + 3 * SS, X + R, Y + R + 3 * SS], fill=(0, 0, 0, 120))
    sh = sh.filter(ImageFilter.GaussianBlur(4 * SS))
    badge_frame(d, X, Y, R)
    stack(layer, X, Y)
    combined = Image.alpha_composite(sh, layer).resize((W, W), Image.LANCZOS)
    return Image.alpha_composite(img, combined)

base = Image.open(sys.argv[1]).convert("RGBA")
icon = stack_badge(base)
icon.save(sys.argv[2], optimize=True)

if len(sys.argv) > 3:
    sheet = Image.new("RGBA", (W * 2, W * 2), (255, 255, 255, 255))
    sheet.paste((28, 30, 34, 255), (0, W, W * 2, W * 2))
    for row in range(2):
        for i, im in enumerate([base, icon]):
            sheet.alpha_composite(im, (i * W, row * W))
    sheet.convert("RGB").save(sys.argv[3])
