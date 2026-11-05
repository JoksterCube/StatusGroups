"""Renders the built-in groups' icons (Domain/GroupIcons.cs) to Docs/Icons/<Group>.png, plain and tinted gold as the
board shows them. Build StatusGroups.csproj -c Release first. Run: python -I Tools/Icon/group_icons.py"""
import os
import struct
import subprocess
import sys
import tempfile

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GOLD = (0.84, 0.6, 0.26)


def read_rgba(path):
    with open(path, 'rb') as f:
        width, height = struct.unpack('<ii', f.read(8))
        return Image.frombytes('RGBA', (width, height), f.read(width * height * 4))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'Docs', 'Icons')
    os.makedirs(out, exist_ok=True)
    with tempfile.TemporaryDirectory() as rendered:
        env = dict(os.environ, STATUSGROUPS_ICON_OUT=rendered)
        subprocess.run(['dotnet', 'test', os.path.join(ROOT, 'Tests', 'StatusGroups.Tests'), '-c', 'Release',
                        '--filter', 'Render_group_icons'], env=env, check=True, stdout=subprocess.DEVNULL)
        for name in sorted(os.listdir(rendered)):
            image = read_rgba(os.path.join(rendered, name))
            group = os.path.splitext(name)[0]
            image.save(os.path.join(out, group + '.png'))
            r, g, b, a = image.split()
            tinted = Image.merge('RGBA', [c.point(lambda v, k=k: round(v * k)) for c, k in zip((r, g, b), GOLD)] + [a])
            tinted.save(os.path.join(out, group + '_gold.png'))
            print(group)


if __name__ == '__main__':
    main()
