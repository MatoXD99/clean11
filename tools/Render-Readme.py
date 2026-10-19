"""Draw original documentation illustrations using only synthetic content.

Requires Pillow. Does not read screenshots, user settings, installed apps, or
wallpapers. PNG outputs have no EXIF, account information, or external assets.
"""
from pathlib import Path
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = Path(__file__).resolve().parents[1] / "docs" / "images"
OUT.mkdir(parents=True, exist_ok=True)
W, H = 1440, 940
FONT = Path(os.environ.get("SystemRoot", "C:/Windows")) / "Fonts"

def font(size, bold=False):
    for name in (FONT / ("segoeuib.ttf" if bold else "segoeui.ttf"), "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(str(name), size)
        except OSError:
            pass
    return ImageFont.load_default()

def canvas():
    im = Image.new("RGB", (W, H), "#09111e")
    glow = Image.new("RGB", (W, H), "#09111e")
    d = ImageDraw.Draw(glow)
    d.ellipse((680, 120, 1700, 920), fill="#164768")
    d.ellipse((-400, 480, 600, 1300), fill="#164b4e")
    d.ellipse((800, -350, 1450, 400), fill="#29254d")
    im = Image.blend(im, glow.filter(ImageFilter.GaussianBlur(115)), .8)
    return im, ImageDraw.Draw(im)

def text(d, x, y, value, size=20, color="#eaf3fa", bold=False):
    d.text((x, y), value, font=font(size, bold), fill=color)

def box(d, bounds, fill="#192c40", radius=20, outline="#40576b", width=1):
    d.rounded_rectangle(bounds, radius=radius, fill=fill, outline=outline, width=width)

def icon(d, x, y, kind, size=36, color="#9cdbf2"):
    s = size
    if kind == "folder":
        d.rounded_rectangle((x,y+7,x+s,y+s-2),radius=5,fill=color)
        d.rounded_rectangle((x+2,y+1,x+s*.46,y+13),radius=3,fill=color)
        d.line((x+6,y+14,x+s-6,y+14),fill="#365578",width=2)
    elif kind == "search":
        d.ellipse((x+2,y+2,x+s-10,y+s-10),outline=color,width=3)
        d.line((x+s-12,y+s-12,x+s,y+s),fill=color,width=3)
    elif kind == "grid":
        for a,b in ((0,0),(1,0),(0,1),(1,1)):
            d.rounded_rectangle((x+a*s*.55,y+b*s*.55,x+a*s*.55+s*.38,y+b*s*.55+s*.38),radius=3,outline=color,width=2)
    elif kind == "music":
        d.line((x+13,y+25,x+13,y+5,x+29,y+1,x+29,y+22),fill=color,width=3)
        d.ellipse((x+3,y+22,x+13,y+31),fill=color)
        d.ellipse((x+19,y+19,x+29,y+28),fill=color)
    else:
        d.rounded_rectangle((x+3,y,x+s-3,y+s),radius=5,outline=color,width=2)
        for j in range(3):d.line((x+10,y+10+j*7,x+s-10,y+10+j*7),fill=color,width=2)

def heading(d, eyebrow, title, description):
    text(d,72,42,eyebrow,15,"#98dce9",True)
    text(d,68,80,title,64,bold=True)
    text(d,72,166,description,23,"#b6c8d8")

def footer(d):
    text(d,72,894,"CLEAN11  /  WINDOWS, WITH ROOM TO BREATHE",14,"#90a9ba",True)
    text(d,935,894,"Illustration · synthetic sample content",14,"#90a9ba")

im,d=canvas()
heading(d,"A CALMER WINDOWS DESKTOP","Clean11","Your windows. Your apps. A little more space to think.")
box(d,(72,240,1368,858),"#112134",28,"#395068")
box(d,(445,272,995,331),"#31485a",15,"#647a8b")
icon(d,466,289,"search",22)
text(d,503,284,"Search anything",20,"#d1dde7")
text(d,945,286,"×",22,"#becbd5")

# Fictional window contents; no real applications or user desktop are sampled.
cards=[(116,377,488,672,"Files","folder"),(526,390,944,720,"Project notes","document"),(981,397,1323,652,"Music","music")]
for x,y,r,b,title,kind in cards:
    box(d,(x,y-34,r,y-5),"#2b4053",9)
    text(d,x+13,y-32,title,16,bold=True);text(d,r-27,y-32,"×",17,"#bed0dd")
    box(d,(x,y,r,b),"#172a3d",15)
    icon(d,x+20,y+22,kind,26)
    if kind=="folder":
        text(d,x+62,y+22,"Workspace",20,bold=True)
        for j,name in enumerate(("Design","Documents","Projects","Reference")):
            icon(d,x+24,y+79+j*43,"folder",23,"#7dcedd")
            text(d,x+64,y+77+j*43,name,17,"#c0d0dd")
    elif kind=="document":
        text(d,x+65,y+22,"A fresh start",24,bold=True)
        text(d,x+26,y+90,"Less clutter. More focus.",25,bold=True)
        for j,length in enumerate((318,344,249,305)):
            d.rounded_rectangle((x+27,y+148+j*24,x+length,y+153+j*24),radius=2,fill="#385268")
        box(d,(x+26,b-67,r-26,b-27),"#254b5f",10,"#376e80")
        text(d,x+41,b-61,"Make room for the next idea.",17,"#a9dce5")
    else:
        box(d,(x+98,y+29,r-98,y+130),"#48627d",20,"#6485a0")
        icon(d,x+143,y+62,"music",36,"#d0f5f4")
        text(d,x+80,y+147,"Quiet afternoon",21,bold=True)
        text(d,x+114,y+180,"Sample playlist",16,"#9fb9cb")
        d.line((x+47,b-31,r-47,b-31),fill="#3f5a70",width=3)
        d.line((x+47,b-31,x+138,b-31),fill="#9bdde6",width=3)

box(d,(433,752,1007,824),"#304455",23,"#6a8292")
for j,kind in enumerate(("grid","folder","search","document","music","folder","document")):
    x=466+j*76
    icon(d,x,770,kind,34,("#b6c9de","#ecd79a","#88d3e1","#afc1ed","#c1b4e8","#b6dcc9","#bec9d7")[j])
    if j in (1,3,4):d.ellipse((x+15,812,x+19,816),fill="#bbd9e6")
footer(d);im.save(OUT/"clean11-desktop.png")

im,d=canvas()
heading(d,"ONE SHARED DESIGN LANGUAGE","Make it feel like you.","Glass, motion, and everyday behavior — together in Clean11 Settings.")
box(d,(72,244,1368,858),"#233549",24,"#536b80")
box(d,(88,260,348,842),"#1b2d40",17,"#1b2d40")
text(d,116,293,"Clean11",31,bold=True)
text(d,117,340,"Make your desktop yours",15,"#a9bece")
for j,name in enumerate(("Appearance","Dock","Overview","Apps & search","System")):
    y=402+j*64
    if j==0:box(d,(104,y-6,332,y+45),"#3b5266",12,"#536e83")
    text(d,122,y+4,name,20,bold=j==0)
text(d,116,801,"Built with WPF UI",15,"#9fb4c5")
text(d,390,280,"Appearance",34,bold=True)
text(d,391,330,"Materials, clarity, and a little personality.",18,"#b4c9d7")
box(d,(390,382,1327,473),"#2f5066",16,"#5b8295")
text(d,414,397,"Frosted glass",22,bold=True)
text(d,414,434,"One material across dock, search, Settings, and previews.",17,"#bed6e3")
box(d,(1240,411,1296,438),"#a2dfec",14,"#a2dfec")
d.ellipse((1273,415,1292,434),fill="#183648")
for y,label,value,fraction in ((493,"Glass visibility","25%",.25),(619,"Blur radius","15 px",.375)):
    box(d,(390,y,1327,y+106),"#263e52",16,"#4b6378")
    text(d,414,y+16,label,21,bold=True);text(d,1238,y+19,value,18,"#c5dae5")
    d.rounded_rectangle((416,y+72,1296,y+76),radius=2,fill="#5d768a")
    end=int(416+880*fraction)
    d.rounded_rectangle((416,y+72,end,y+76),radius=2,fill="#9edeea")
    d.ellipse((end-9,y+65,end+9,y+83),fill="#a8e4ed",outline="#447a91",width=3)
text(d,392,799,"All changes saved",17,"#b3c9d9")
box(d,(1142,782,1327,831),"#315a70",12,"#638ba0")
text(d,1170,793,"Apply changes",19)
footer(d);im.save(OUT/"clean11-settings.png")
print("Rendered two original synthetic illustrations; no screenshot or system content read.")
