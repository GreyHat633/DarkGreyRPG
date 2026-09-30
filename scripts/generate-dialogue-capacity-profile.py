"""Extract numeric advances from locally provisioned Minecraft 1.7.10 assets; no font media is shipped."""
import json, re, math
from pathlib import Path
from PIL import Image
root = Path(__file__).resolve().parents[1]
src = root / "build/rfg/minecraft-src"
java = (src / "java/net/minecraft/client/gui/FontRenderer.java").read_text(encoding="utf-8")
part = java[java.index("public int getCharWidth"):]
literal = re.search(r'String|int i = "((?:\\.|[^"\\])*)"\.indexOf', part)
encoded = re.search(r'int i = "((?:\\.|[^"\\])*)"\.indexOf', part).group(1)
mapping = bytes(encoded, "ascii").decode("unicode_escape")
image = Image.open(src / "resources/assets/minecraft/textures/font/ascii.png").convert("RGBA")
cell = image.width // 16
ascii_widths = []
for i in range(256):
    right = -1
    for x in range(cell):
        if any(image.getpixel(((i % 16)*cell+x,(i//16)*cell+y))[3] for y in range(cell)):
            right = x
    ascii_widths.append(math.floor(0.5+(right+1)*8/cell)+1)
glyph = (src / "resources/assets/minecraft/font/glyph_sizes.bin").read_bytes()
normal, unicode = [], []
for code, value in enumerate(glyph):
    lo, hi = value >> 4, value & 15
    if hi > 7: lo, hi = 0, 15
    advance = (hi + 1-lo)//2+1 if value else 0
    if code == 32: advance = 4
    unicode.append(advance)
    index = mapping.find(chr(code))
    normal.append(4 if code == 32 else ascii_widths[index] if code > 0 and index >= 0 else advance)
profile = {
 "profile_version": "0333-standard-1", "calibration": "native-normal-font-20260921; unicode numeric extraction",
 "source": "Minecraft 1.7.10 FontRenderer.getCharWidth; numeric extraction only",
 "logical_width":320,"logical_height":240,"scale":1.5,"line_height":14,
 "text_left":80,"text_right":296,"body_top":175,"body_bottom":221,
 "wrap_width":math.floor((216-2*math.ceil(9*1.5))/1.5),"rows":3,"unit":1,
 "normal":normal, "unicode":unicode
}
profile["raw"] = profile["wrap_width"] * profile["rows"]
profile["safe"] = math.floor(profile["raw"] * .9)
samples = ["", "i"*160, "W"*60, "中"*40, "中"*43, "你好，世界！Hello 123", "a  b "+"   "*50, "§l"+"W"*30+"§r"+"i"*30]
def measure(text, advances):
    row, used, bold, at = 1, 0, False, 0
    while at < len(text):
        if text[at] == "§" and at+1 < len(text):
            code=text[at+1].lower()
            if code in "0123456789abcdefr": bold=False
            elif code == "l": bold=True
            at+=2; continue
        advance=advances[ord(text[at])]
        if bold and advance > 0: advance+=1
        if used and used+advance > profile["wrap_width"]: row+=1; used=0
        used+=advance; at+=1
    return {"lines":row,"used":(row-1)*profile["wrap_width"]+used}
profile["vectors"]=[{"text":text,"normal_result":measure(text,normal),"unicode_result":measure(text,unicode)} for text in samples]
out=root/"schema/dialogue-capacity-profile.json"
out.write_text(json.dumps(profile,separators=(",",":")),encoding="utf-8")
print(f"Numeric profile {out}: raw={profile['raw']} safe={profile['safe']}; {profile['calibration']}")
