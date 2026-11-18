"""
Genera la textura de la "pelota foca" (estilo boceto a lápiz) para la esfera de Unity.

Formato equirectangular 2048x1024: x = longitud (u), y = latitud (arriba = polo norte).
- Lomo gris con manchas oscuras (más densas arriba), panza clara abajo.
- Cara (ojos, hocico, nariz, bigotes) mirando hacia la longitud CARA_U.
- Trazos de lápiz y grano de papel para que combine con el dibujo.

Uso:  python Tools/generar_textura_foca.py
Salida: Assets/Dino/Textures/T_Pelota_Foca.png
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

W, H = 2048, 1024
# Longitud (0..1) donde va la cara. En la esfera de Unity u = 0.25 mira hacia -Z (hacia la salida del jugador).
CARA_U = 0.25
SALIDA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Dino", "Textures", "T_Pelota_Foca.png")

random.seed(7)


def lat_de_y(y):
    return (0.5 - y / H) * math.pi  # +pi/2 arriba


def estirar(y):
    """Cuánto se estira horizontalmente un dibujo a esa latitud (1/cos)."""
    return 1.0 / max(0.18, math.cos(lat_de_y(y)))


def x_de_u(u):
    return (u % 1.0) * W


def elipse_esferica(d, cx, cy, rx, ry, color, ancho=0, relleno=True):
    """Dibuja una elipse corrigiendo el estiramiento horizontal y repitiéndola en la costura."""
    sx = rx * estirar(cy)
    for desplaz in (-W, 0, W):
        caja = [cx - sx + desplaz, cy - ry, cx + sx + desplaz, cy + ry]
        if relleno:
            d.ellipse(caja, fill=color)
        else:
            d.ellipse(caja, outline=color, width=ancho)


def main():
    # 1. Base: gris claro arriba que se aclara hacia la panza.
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        lat = lat_de_y(y)
        t = (math.sin(lat) + 1) * 0.5  # 1 arriba, 0 abajo
        base = int(205 - 75 * t)       # lomo ~130, panza ~205 (la luz del sol la aclara)
        for x in range(W):
            px[x, y] = (base, base, base + 2)

    d = ImageDraw.Draw(img, "RGBA")

    # 2. Panza blanca delante de la cara (bajo el hocico).
    for k in range(90):
        cy = H * (0.6 + random.uniform(-0.03, 0.12))
        cx = x_de_u(CARA_U + random.uniform(-0.12, 0.12))
        elipse_esferica(d, cx, cy, random.uniform(50, 110), random.uniform(30, 70), (252, 252, 250, 30))

    # 3. Trazos de lápiz (sombreado diagonal), más oscuros en el lomo.
    for _ in range(9000):
        y = random.uniform(0, H)
        lat = lat_de_y(y)
        oscuridad = 0.25 + 0.75 * max(0.0, math.sin(lat) + 0.3)
        x = random.uniform(0, W)
        largo = random.uniform(8, 26) * estirar(y) ** 0.5
        g = int(70 + random.uniform(0, 60))
        a = int(18 + 40 * oscuridad * random.random())
        d.line([x, y, x + largo, y - largo * 0.55], fill=(g, g, g, a), width=1)

    # 4. Manchas del lomo: densas arriba, se apagan hacia el ecuador; no sobre la cara.
    for _ in range(520):
        y = H * (0.06 + random.betavariate(1.8, 2.6) * 0.5)  # sesgadas hacia el lomo
        u = random.random()
        du = min(abs(u - CARA_U), 1 - abs(u - CARA_U))
        if du < 0.06 and y > H * 0.33:
            continue
        x = x_de_u(u)
        r = random.uniform(4, 10)
        g = random.randint(40, 80)
        elipse_esferica(d, x, y, r * random.uniform(0.8, 1.4), r * random.uniform(0.55, 0.95), (g, g, g + 4, 200))
        # contorno de lápiz, como en el dibujo
        elipse_esferica(d, x, y, r * 1.1, r * 0.9, (30, 30, 34, 120), ancho=2, relleno=False)

    # 5. Cara: ojos, hocico, nariz, boca y puntos de bigote.
    cy = H * 0.47
    cx = x_de_u(CARA_U)
    sep = W * 0.045  # separación de los ojos en longitud
    for lado in (-1, 1):
        ex = cx + lado * sep
        elipse_esferica(d, ex, cy - 40, 20, 26, (15, 15, 18, 255))
        elipse_esferica(d, ex + 7, cy - 49, 6, 7, (255, 255, 255, 235))  # brillo del ojo
    # hocico claro
    elipse_esferica(d, cx - sep * 0.42, cy + 22, 44, 30, (238, 238, 236, 235))
    elipse_esferica(d, cx + sep * 0.42, cy + 22, 44, 30, (238, 238, 236, 235))
    for lado in (-1, 1):
        for i in range(5):
            elipse_esferica(d, cx + lado * (sep * 0.25 + 16 * (i % 3)), cy + 14 + 11 * (i // 3) + 4 * i % 7, 2.6, 2.6, (40, 40, 40, 220))
    # nariz
    elipse_esferica(d, cx, cy - 2, 22, 15, (12, 12, 14, 255))
    elipse_esferica(d, cx - 6, cy - 7, 6, 3.5, (200, 200, 200, 200))
    # boquita
    d.arc([cx - 30, cy + 22, cx + 2, cy + 50], 20, 160, fill=(30, 30, 30, 230), width=4)
    d.arc([cx - 2, cy + 22, cx + 30, cy + 50], 20, 160, fill=(30, 30, 30, 230), width=4)

    # 6. Grano de papel suave.
    ruido = Image.effect_noise((W, H), 18).convert("RGB")
    img = Image.blend(img, ruido, 0.06).filter(ImageFilter.SMOOTH)

    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)
    img.save(SALIDA)
    print("Textura guardada en", os.path.abspath(SALIDA))


if __name__ == "__main__":
    main()
