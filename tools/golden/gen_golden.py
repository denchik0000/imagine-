import sys
import json, re
from PIL import Image
from materialyoucolor.hct.hct import Hct
from materialyoucolor.palettes.tonal_palette import TonalPalette
from materialyoucolor.dynamiccolor.material_dynamic_colors import MaterialDynamicColors
from materialyoucolor.dynamiccolor.dynamic_scheme import DynamicScheme
from materialyoucolor.dynamiccolor.variant import Variant
from materialyoucolor.quantize import QuantizeCelebi
from materialyoucolor.score.score import Score
from materialyoucolor.temperature.temperature_cache import TemperatureCache

out = {}
colors = [0xFFD55E5D,0xFF4285F4,0xFFB79531,0xFF00FF00,0xFF6750A4,0xFF000000,0xFFFFFFFF,0xFF123456,0xFFFF0000,0xFF0000FF,0xFFFFFF00,0xFF7C3756,0xFFD0C194]
out["hct"] = [{"argb": c, "hue": Hct.from_int(c).hue, "chroma": Hct.from_int(c).chroma, "tone": Hct.from_int(c).tone} for c in colors]
solve = []
for h in range(0, 360, 30):
    for c in [0, 10, 36, 60, 120, 200]:
        for t in [0, 10, 25, 40, 50, 60, 80, 95, 100]:
            solve.append([h, c, t, Hct.from_hct(h, c, t).to_int()])
out["solve"] = solve
pal = []
for h, c in [(21, 36), (270, 36), (100, 48), (200, 200), (60, 16)]:
    p = TonalPalette.from_hue_and_chroma(h, c)
    pal.append({"hue": h, "chroma": c, "key": p.key_color.to_int(), "tones": {str(t): p.tone(t) for t in [0,10,20,30,40,50,60,70,80,90,95,99,100]}})
out["palettes"] = pal
temps = []
for c in [0xFFD55E5D, 0xFF4285F4, 0xFF00FF00]:
    tc = TemperatureCache(Hct.from_int(c))
    temps.append({"argb": c, "complement": tc.complement.to_int(), "analogous": [x.to_int() for x in tc.analogous(5, 12)], "analogous3_6": [x.to_int() for x in tc.analogous(3, 6)]})
out["temperature"] = temps
mdc = MaterialDynamicColors(spec="2021")
names = [n for n in ["primaryPaletteKeyColor","secondaryPaletteKeyColor","tertiaryPaletteKeyColor","neutralPaletteKeyColor","neutralVariantPaletteKeyColor","background","onBackground","surface","surfaceDim","surfaceBright","surfaceContainerLowest","surfaceContainerLow","surfaceContainer","surfaceContainerHigh","surfaceContainerHighest","onSurface","surfaceVariant","onSurfaceVariant","inverseSurface","inverseOnSurface","outline","outlineVariant","shadow","scrim","surfaceTint","primary","onPrimary","primaryContainer","onPrimaryContainer","inversePrimary","secondary","onSecondary","secondaryContainer","onSecondaryContainer","tertiary","onTertiary","tertiaryContainer","onTertiaryContainer","error","onError","errorContainer","onErrorContainer","primaryFixed","primaryFixedDim","onPrimaryFixed","onPrimaryFixedVariant","secondaryFixed","secondaryFixedDim","onSecondaryFixed","onSecondaryFixedVariant","tertiaryFixed","tertiaryFixedDim","onTertiaryFixed","onTertiaryFixedVariant"]]
schemes = []
for seed in [0xFFD55E5D, 0xFF4285F4, 0xFFB79531, 0xFF00C853, 0xFF6750A4]:
    for v in Variant:
        for dark in [False, True]:
            for contrast in [-1.0, 0.0, 0.5, 1.0]:
                if contrast not in (0.0,) and v not in (Variant.TONAL_SPOT, Variant.FIDELITY, Variant.MONOCHROME): continue
                s = DynamicScheme(Hct.from_int(seed), v, contrast, dark, spec_version="2021")
                roles = {n: getattr(mdc, n).get_argb(s) for n in names}
                schemes.append({"seed": seed, "variant": v.name, "dark": dark, "contrast": contrast, "roles": roles})
out["schemes"] = schemes
im = Image.open('sys.argv[1]').convert('RGB')
im.thumbnail((128, 128))
px = list(im.getdata())
argbs = [0xFF000000 | (r<<16) | (g<<8) | b for r,g,b in px]
q = QuantizeCelebi(px, 128)
out["wallpaper"] = {"width": im.size[0], "height": im.size[1], "quantized": {str(k): v for k, v in q.items()}, "ranked": Score.score(q)}
json.dump(out, open('tests/Imagine.Color.Tests/TestData/golden.json', 'w'))
with open('tests/Imagine.Color.Tests/TestData/wallpaper-72x128.argb.txt','w') as f:
    f.write("# Обои пользователя (белая машина в цветущих деревьях), уменьшенные до 72x128, ARGB построчно\n")
    for i in range(0, len(argbs), 16):
        f.write(" ".join('%08X' % a for a in argbs[i:i+16]) + "\n")
print(len(schemes), [('#%06X' % (c & 0xFFFFFF)) for c in Score.score(q)])
