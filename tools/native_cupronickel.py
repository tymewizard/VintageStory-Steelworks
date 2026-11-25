"""Preserve native cupronickel textures and derive only missing equipment atlases."""
import io
from PIL import Image

def native_palette(z, src, preserve_colored=False):
    native=Image.open(io.BytesIO(z.read('survival/textures/block/metal/ingot/cupronickel.png'))).convert('RGBA')
    im=Image.open(io.BytesIO(z.read(src))).convert('RGBA')
    def lum(p): return .299*p[0]+.587*p[1]+.114*p[2]
    palette=sorted([p for p in native.getdata() if p[3]],key=lum)
    # Map source luminance to the native palette, retaining original texture detail and alpha.
    for y in range(im.height):
        for x in range(im.width):
            p=im.getpixel((x,y))
            if not p[3] or (preserve_colored and max(p[:3])-min(p[:3])>40):continue
            q=palette[round(max(0,min(1,(lum(p)-45)/165))*(len(palette)-1))]
            im.putpixel((x,y),(*q[:3],p[3]))
    return im
