"""
DINO WEST - Control por manos (MediaPipe -> UDP -> Unity)

Lee la webcam, detecta hasta 2 manos y envía el estado a Unity en JSON por UDP (127.0.0.1:5055).

  2 manos  (CONDUCCIÓN)
    - Volante invisible: el ángulo muñeca izquierda -> muñeca derecha gira al dino (zona muerta ±6°).
    - Manos abiertas con pulgares recogidos = CAMINAR (throttle 0.45).
    - Manos abiertas con pulgares extendidos (o manos elevadas) = CORRER (throttle 1.0, sprint 1).
    - Ambas manos en puño = FRENO.
    - Subir las dos manos de golpe = SALTO (cooldown 0.7 s).
    - Pellizco pulgar+índice con la mano derecha = RUGIDO (cooldown 0.8 s).
    - Pellizco mantenido 1 s con la mano izquierda = PAUSA (abre el menú).

  1 mano   (MENÚ / CURSOR)
    - La punta del índice mueve el cursor; pellizco pulgar+índice = clic.

Uso:
    python Tools/mediapipe_hand_controller.py            # cámara elegida la última vez (o pregunta)
    python Tools/mediapipe_hand_controller.py --elegir   # vuelve a mostrar el selector de cámara
    python Tools/mediapipe_hand_controller.py --camara 1
    python Tools/mediapipe_hand_controller.py --prueba   # sin webcam: envía datos sintéticos

Teclas en la ventana de preview: C cambia de cámara, Q o ESC para salir.
"""

import argparse
import json
import math
import os
import socket
import sys
import time
import urllib.request

HOST, PUERTO = "127.0.0.1", 5055

# Conexiones de la mano (21 landmarks) para dibujar sin depender de drawing_utils.
CONEXIONES = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (17, 18), (18, 19), (19, 20), (0, 17),
]

MODELO_URL = ("https://storage.googleapis.com/mediapipe-models/hand_landmarker/"
              "hand_landmarker/float16/latest/hand_landmarker.task")
MODELO_RUTA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "models", "hand_landmarker.task")
# Cámara elegida por el usuario (se recuerda entre ejecuciones).
CAMARA_GUARDADA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "camara_elegida.json")
MAX_CAMARAS = 6


# ----------------------------------------------------------------------------------------
# Cámaras: abrir, listar con miniatura y elegir
# ----------------------------------------------------------------------------------------
def abrir_camara(cv2, indice):
    cap = cv2.VideoCapture(indice, cv2.CAP_DSHOW) if os.name == "nt" else cv2.VideoCapture(indice)
    if not cap.isOpened():
        cap.release()
        return None
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    # Algunas cámaras virtuales dan los primeros cuadros vacíos.
    for _ in range(5):
        ok, frame = cap.read()
        if ok and frame is not None:
            return cap
        time.sleep(0.05)
    cap.release()
    return None


def nombres_camaras():
    """Nombres de las cámaras de Windows (orientativo: el orden puede no coincidir con los índices)."""
    if os.name != "nt":
        return []
    try:
        import subprocess
        salida = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "Get-CimInstance Win32_PnPEntity | Where-Object { $_.PNPClass -in 'Camera','Image' } | "
             "ForEach-Object { $_.Name }"],
            capture_output=True, text=True, timeout=8, creationflags=0x08000000)
        return [n.strip() for n in salida.stdout.splitlines() if n.strip()]
    except Exception:
        return []


def listar_camaras(cv2):
    """Devuelve [(indice, miniatura)] de las cámaras que entregan imagen."""
    encontradas = []
    for i in range(MAX_CAMARAS):
        cap = abrir_camara(cv2, i)
        if cap is None:
            continue
        ok, frame = cap.read()
        cap.release()
        if ok and frame is not None:
            encontradas.append((i, cv2.flip(frame, 1)))
    return encontradas


def leer_camara_guardada():
    try:
        with open(CAMARA_GUARDADA, encoding="utf-8") as f:
            return int(json.load(f)["indice"])
    except Exception:
        return None


def guardar_camara(indice):
    try:
        with open(CAMARA_GUARDADA, "w", encoding="utf-8") as f:
            json.dump({"indice": indice}, f)
    except Exception:
        pass


def elegir_camara(cv2):
    """Ventana con una miniatura por cámara: número o clic para elegir. Devuelve el índice o None."""
    print("[DinoManos] Buscando cámaras...")
    camaras = listar_camaras(cv2)
    if not camaras:
        return None
    if len(camaras) == 1:
        return camaras[0][0]

    nombres = nombres_camaras()
    if nombres:
        print("[DinoManos] Cámaras de Windows: " + " | ".join(nombres))

    import numpy as np
    tw, th = 320, 240
    columnas = min(3, len(camaras))
    filas = (len(camaras) + columnas - 1) // columnas
    lienzo = np.zeros((filas * th + 60, columnas * tw, 3), dtype=np.uint8)
    cajas = []
    for k, (indice, img) in enumerate(camaras):
        x, y = (k % columnas) * tw, (k // columnas) * th + 60
        lienzo[y:y + th, x:x + tw] = cv2.resize(img, (tw, th))
        cv2.rectangle(lienzo, (x, y), (x + tw - 1, y + th - 1), (60, 60, 60), 2)
        cv2.rectangle(lienzo, (x + 6, y + 6), (x + 120, y + 42), (20, 20, 20), -1)
        cv2.putText(lienzo, f"{k + 1}  (cam {indice})", (x + 12, y + 32), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
        cajas.append((x, y, indice))
    cv2.putText(lienzo, "ELIGE LA CAMARA: pulsa su numero o haz clic", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
    cv2.putText(lienzo, "(luego, en el preview, C cambia de camara)", (10, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (180, 180, 180), 1)

    elegida = {"v": None}

    def clic(evento, x, y, *_):
        if evento == cv2.EVENT_LBUTTONDOWN:
            for cx, cy, indice in cajas:
                if cx <= x < cx + tw and cy <= y < cy + th:
                    elegida["v"] = indice

    titulo = "DINO WEST - Elegir camara"
    cv2.namedWindow(titulo)
    cv2.setMouseCallback(titulo, clic)
    while elegida["v"] is None:
        cv2.imshow(titulo, lienzo)
        tecla = cv2.waitKey(30) & 0xFF
        if ord("1") <= tecla < ord("1") + len(camaras):
            elegida["v"] = camaras[tecla - ord("1")][0]
        elif tecla == 27:
            elegida["v"] = camaras[0][0]
        if cv2.getWindowProperty(titulo, cv2.WND_PROP_VISIBLE) < 1:
            elegida["v"] = elegida["v"] if elegida["v"] is not None else camaras[0][0]
    cv2.destroyWindow(titulo)
    return elegida["v"]


def siguiente_camara(cv2, actual):
    """Prueba los índices siguientes al actual y devuelve (indice, cap) de la primera que funcione."""
    for paso in range(1, MAX_CAMARAS + 1):
        i = (actual + paso) % MAX_CAMARAS
        cap = abrir_camara(cv2, i)
        if cap is not None:
            return i, cap
    return actual, None


# ----------------------------------------------------------------------------------------
# Utilidades
# ----------------------------------------------------------------------------------------
class EMA:
    """Media móvil exponencial para quitar el temblor."""

    def __init__(self, alfa):
        self.alfa = alfa
        self.valor = None

    def __call__(self, x):
        self.valor = x if self.valor is None else self.valor + self.alfa * (x - self.valor)
        return self.valor

    def reset(self):
        self.valor = None


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def tam_palma(lm):
    # Muñeca -> nudillo medio: referencia de escala independiente de la distancia a la cámara.
    return max(dist(lm[0], lm[9]), 1e-4)


def dedos_extendidos(lm):
    """Índice, medio, anular y meñique: extendido si la punta está más lejos de la muñeca que la articulación PIP."""
    n = 0
    for punta, pip in ((8, 6), (12, 10), (16, 14), (20, 18)):
        if dist(lm[punta], lm[0]) > dist(lm[pip], lm[0]) * 1.08:
            n += 1
    return n


def pulgar_extendido(lm):
    # Pulgar fuera: su punta queda lejos del nudillo del índice (relativo al tamaño de la palma).
    return dist(lm[4], lm[5]) / tam_palma(lm) > 0.75 and dist(lm[4], lm[0]) > dist(lm[3], lm[0])


def pellizco_ratio(lm):
    return dist(lm[4], lm[8]) / tam_palma(lm)


def gesto_ok(lm, umbral=0.33):
    """Pellizco tipo "OK": pulgar e índice juntos y al menos 2 de los otros 3 dedos estirados (un puño no cuenta)."""
    otros = sum(1 for punta, pip in ((12, 10), (16, 14), (20, 18)) if dist(lm[punta], lm[0]) > dist(lm[pip], lm[0]) * 1.08)
    return pellizco_ratio(lm) < umbral and otros >= 2


# ----------------------------------------------------------------------------------------
# Detector (API Solutions si existe; si no, API Tasks)
# ----------------------------------------------------------------------------------------
class DetectorManos:
    def __init__(self):
        import mediapipe as mp
        self.mp = mp
        self.modo = None
        if hasattr(mp, "solutions") and hasattr(mp.solutions, "hands"):
            self.hands = mp.solutions.hands.Hands(
                static_image_mode=False, max_num_hands=2, model_complexity=0,
                min_detection_confidence=0.6, min_tracking_confidence=0.5)
            self.modo = "solutions"
        else:
            from mediapipe.tasks import python as mp_python
            from mediapipe.tasks.python import vision
            if not os.path.exists(MODELO_RUTA):
                os.makedirs(os.path.dirname(MODELO_RUTA), exist_ok=True)
                print(f"[DinoManos] Descargando el modelo de manos de MediaPipe en {MODELO_RUTA} ...")
                urllib.request.urlretrieve(MODELO_URL, MODELO_RUTA)
            opciones = vision.HandLandmarkerOptions(
                base_options=mp_python.BaseOptions(model_asset_path=MODELO_RUTA),
                running_mode=vision.RunningMode.VIDEO,
                num_hands=2,
                min_hand_detection_confidence=0.6,
                min_hand_presence_confidence=0.5,
                min_tracking_confidence=0.5)
            self.landmarker = vision.HandLandmarker.create_from_options(opciones)
            self.modo = "tasks"
            self.t0 = time.monotonic()
        print(f"[DinoManos] MediaPipe {getattr(mp, '__version__', '?')} usando API '{self.modo}'")

    def detectar(self, frame_rgb):
        """Devuelve una lista de manos; cada mano es una lista de 21 (x, y) normalizados 0..1."""
        if self.modo == "solutions":
            res = self.hands.process(frame_rgb)
            if not res.multi_hand_landmarks:
                return []
            return [[(p.x, p.y) for p in h.landmark] for h in res.multi_hand_landmarks]

        img = self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=frame_rgb)
        ts = int((time.monotonic() - self.t0) * 1000)
        res = self.landmarker.detect_for_video(img, ts)
        return [[(p.x, p.y) for p in h] for h in (res.hand_landmarks or [])]

    def cerrar(self):
        if self.modo == "solutions":
            self.hands.close()
        else:
            self.landmarker.close()


# ----------------------------------------------------------------------------------------
# Interpretación de gestos
# ----------------------------------------------------------------------------------------
class Gestos:
    ZONA_MUERTA = 6.0      # grados
    GIRO_MAX = 40.0        # grados para giro completo
    CD_SALTO = 0.7
    CD_RUGIDO = 0.8
    PAUSA_MANTENER = 1.0

    def __init__(self):
        self.ema_steer = EMA(0.35)
        self.ema_throttle = EMA(0.30)
        self.ema_cx = EMA(0.45)
        self.ema_cy = EMA(0.45)
        self.y_prev = None
        self.t_prev = None
        self.t_salto = -9.0
        self.t_rugido = -9.0
        self.pellizco_der_antes = False
        self.t_pellizco_izq = None
        self.t_pausa = -9.0
        self.pinch_cursor = False
        self.jump_id = 0
        self.roar_id = 0
        self.pause_id = 0
        self.estado = "SIN MANOS"

    def procesar(self, manos, ahora):
        out = {"hands": len(manos), "mode": "none", "steer": 0.0, "throttle": 0.0, "sprint": 0, "brake": 0,
               "cx": 0.5, "cy": 0.5, "pinch": 0}

        if len(manos) >= 2:
            self._conduccion(manos, ahora, out)
        elif len(manos) == 1:
            self._cursor(manos[0], out)
        else:
            self.ema_steer.reset()
            self.ema_throttle.reset()
            self.y_prev = None
            self.t_pellizco_izq = None
            self.estado = "SIN MANOS"

        out["jumpId"] = self.jump_id
        out["roarId"] = self.roar_id
        out["pauseId"] = self.pause_id
        return out

    def _conduccion(self, manos, ahora, out):
        # Imagen en espejo: la mano con menor x es la izquierda del jugador.
        izq, der = sorted(manos[:2], key=lambda lm: lm[0][0])
        out["mode"] = "drive"
        self.ema_cx.reset()
        self.ema_cy.reset()

        # Volante: y crece hacia abajo; si la derecha baja respecto a la izquierda se gira a la derecha.
        dx = der[0][0] - izq[0][0]
        dy = der[0][1] - izq[0][1]
        angulo = math.degrees(math.atan2(dy, max(dx, 1e-4)))
        if abs(angulo) <= self.ZONA_MUERTA:
            crudo = 0.0
        else:
            crudo = math.copysign(min(1.0, (abs(angulo) - self.ZONA_MUERTA) / (self.GIRO_MAX - self.ZONA_MUERTA)), angulo)
        steer = self.ema_steer(crudo)
        if abs(steer) < 0.02:
            steer = 0.0

        ext_i, ext_d = dedos_extendidos(izq), dedos_extendidos(der)
        abiertas = ext_i >= 3 and ext_d >= 3
        punos = ext_i <= 1 and ext_d <= 1
        pulgares = pulgar_extendido(izq) and pulgar_extendido(der)
        y_media = (izq[0][1] + der[0][1]) * 0.5
        elevadas = y_media < 0.33

        sprint = 0
        brake = 0
        if punos:
            objetivo, brake, self.estado = 0.0, 1, "FRENO"
        elif abiertas and (pulgares or elevadas):
            objetivo, sprint, self.estado = 1.0, 1, "CORRER"
        elif abiertas:
            objetivo, self.estado = 0.45, "CAMINAR"
        else:
            objetivo, self.estado = 0.0, "QUIETO"
        throttle = 0.0 if brake else self.ema_throttle(objetivo)
        if brake:
            self.ema_throttle.valor = 0.0
        if abs(steer) > 0.05 and self.estado in ("CAMINAR", "CORRER", "QUIETO"):
            self.estado = f"GIRO {'DER' if steer > 0 else 'IZQ'} · {self.estado}"

        # Salto: las dos manos suben rápido (velocidad en alturas de imagen por segundo).
        if self.y_prev is not None and ahora > self.t_prev:
            vel = (self.y_prev - y_media) / (ahora - self.t_prev)
            if vel > 1.6 and ahora - self.t_salto > self.CD_SALTO:
                self.t_salto = ahora
                self.jump_id += 1
        self.y_prev, self.t_prev = y_media, ahora
        if ahora - self.t_salto < 0.35:
            self.estado = "SALTO"

        # Rugido: pellizco con la mano derecha (por flanco).
        pell_d = gesto_ok(der, 0.33)
        if pell_d and not self.pellizco_der_antes and ahora - self.t_rugido > self.CD_RUGIDO:
            self.t_rugido = ahora
            self.roar_id += 1
        # Histéresis: se considera soltado cuando se separan por encima de 0.45.
        self.pellizco_der_antes = pell_d or (self.pellizco_der_antes and pellizco_ratio(der) < 0.45)
        if ahora - self.t_rugido < 0.5:
            self.estado = "RUGIDO"

        # Pausa: pellizco mantenido con la mano izquierda.
        if gesto_ok(izq, 0.33):
            if self.t_pellizco_izq is None:
                self.t_pellizco_izq = ahora
            elif ahora - self.t_pellizco_izq > self.PAUSA_MANTENER and ahora - self.t_pausa > 2.0:
                self.t_pausa = ahora
                self.pause_id += 1
        else:
            self.t_pellizco_izq = None
        if ahora - self.t_pausa < 0.8:
            self.estado = "PAUSA"

        out.update(steer=round(steer, 3), throttle=round(throttle, 3), sprint=sprint, brake=brake)

    def _cursor(self, lm, out):
        out["mode"] = "cursor"
        self.y_prev = None
        self.ema_steer.reset()
        self.ema_throttle.reset()
        # El centro de la imagen se amplía: no hace falta llevar el dedo hasta el borde.
        def remap(v):
            return min(1.0, max(0.0, (v - 0.15) / 0.7))
        cx = self.ema_cx(remap(lm[8][0]))
        cy = self.ema_cy(remap(lm[8][1]))
        r = pellizco_ratio(lm)
        # Histéresis: se cierra por debajo de 0.33 y se suelta por encima de 0.45.
        self.pinch_cursor = r < 0.33 if not self.pinch_cursor else r < 0.45
        out.update(cx=round(cx, 4), cy=round(cy, 4), pinch=int(self.pinch_cursor))
        self.estado = "CURSOR · CLIC" if self.pinch_cursor else "CURSOR"


# ----------------------------------------------------------------------------------------
# Preview
# ----------------------------------------------------------------------------------------
def dibujar(cv2, frame, manos, gestos, paquete, fps):
    h, w = frame.shape[:2]
    for lm in manos:
        pts = [(int(x * w), int(y * h)) for x, y in lm]
        for a, b in CONEXIONES:
            cv2.line(frame, pts[a], pts[b], (60, 200, 255), 2)
        for i, p in enumerate(pts):
            cv2.circle(frame, p, 4 if i not in (4, 8) else 7, (40, 40, 230) if i in (4, 8) else (255, 255, 255), -1)

    if len(manos) >= 2:
        izq, der = sorted(manos[:2], key=lambda lm: lm[0][0])
        cv2.line(frame, (int(izq[0][0] * w), int(izq[0][1] * h)), (int(der[0][0] * w), int(der[0][1] * h)), (0, 220, 120), 3)
    if paquete["mode"] == "cursor":
        cv2.circle(frame, (int((paquete["cx"] * 0.7 + 0.15) * w), int((paquete["cy"] * 0.7 + 0.15) * h)), 12,
                   (0, 0, 255) if paquete["pinch"] else (0, 255, 0), 2)

    ok = paquete["hands"] > 0
    cv2.rectangle(frame, (0, 0), (w, 64), (20, 20, 20), -1)
    cv2.putText(frame, gestos.estado, (10, 26), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (255, 255, 255), 2)
    cv2.putText(frame, "C: camara  Q: salir", (w - 175, 26), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (180, 180, 180), 1)
    cv2.putText(frame, f"MANOS: {'OK' if ok else 'NO'} ({paquete['hands']})  steer {paquete['steer']:+.2f}  "
                       f"thr {paquete['throttle']:.2f}  {fps:.0f} fps",
                (10, 52), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 220, 120) if ok else (0, 0, 255), 1)


# ----------------------------------------------------------------------------------------
# Bucles
# ----------------------------------------------------------------------------------------
def enviar(sock, seq, paquete):
    paquete["seq"] = seq
    sock.sendto(json.dumps(paquete, separators=(",", ":")).encode("utf-8"), (HOST, PUERTO))


def modo_prueba(sock, hz):
    """Sin webcam: volante sinusoidal, caminar/correr/frenar por fases y un salto/rugido cada pocos segundos."""
    print(f"[DinoManos] MODO PRUEBA -> {HOST}:{PUERTO}  (Ctrl+C para salir)")
    seq, t0 = 0, time.monotonic()
    jump_id = roar_id = 0
    ultimo_evento = 0
    try:
        while True:
            t = time.monotonic() - t0
            fase = int(t // 4) % 3
            if int(t // 3) != ultimo_evento:
                ultimo_evento = int(t // 3)
                if ultimo_evento % 2:
                    jump_id += 1
                else:
                    roar_id += 1
            paquete = {"hands": 2, "mode": "drive", "steer": round(math.sin(t * 0.8) * 0.8, 3),
                       "throttle": [0.45, 1.0, 0.0][fase], "sprint": int(fase == 1), "brake": int(fase == 2),
                       "cx": 0.5, "cy": 0.5, "pinch": 0, "jumpId": jump_id, "roarId": roar_id, "pauseId": 0}
            seq += 1
            enviar(sock, seq, paquete)
            if seq % hz == 0:
                print(f"  t={t:5.1f}s {paquete}")
            time.sleep(1.0 / hz)
    except KeyboardInterrupt:
        pass


def modo_camara(sock, camara, hz, ancho_preview, forzar_eleccion):
    try:
        import cv2
    except ImportError:
        sys.exit("Falta OpenCV: pip install -r Tools/requirements.txt")

    # Prioridad: --camara N > cámara guardada > ventana para elegir.
    if camara is None and not forzar_eleccion:
        camara = leer_camara_guardada()
    cap = abrir_camara(cv2, camara) if camara is not None else None
    if cap is None:
        if camara is not None:
            print(f"[DinoManos] La cámara {camara} no responde; elige otra.")
        camara = elegir_camara(cv2)
        if camara is None:
            sys.exit("No se encontró ninguna cámara.")
        cap = abrir_camara(cv2, camara)
        if cap is None:
            sys.exit(f"No se pudo abrir la cámara {camara}.")
    guardar_camara(camara)
    print(f"[DinoManos] Usando la cámara {camara} (C en el preview para cambiar).")

    detector = DetectorManos()
    gestos = Gestos()
    seq = 0
    periodo = 1.0 / hz
    t_envio = 0.0
    fps, t_fps, cuadros = 0.0, time.monotonic(), 0
    print(f"[DinoManos] Enviando a {HOST}:{PUERTO}. Q o ESC para salir.")

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                time.sleep(0.01)
                continue
            frame = cv2.flip(frame, 1)  # espejo: mover la mano derecha mueve lo de la derecha
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            ahora = time.monotonic()
            manos = detector.detectar(rgb)
            paquete = gestos.procesar(manos, ahora)

            if ahora - t_envio >= periodo * 0.9:
                t_envio = ahora
                seq += 1
                enviar(sock, seq, paquete)

            cuadros += 1
            if ahora - t_fps >= 1.0:
                fps, t_fps, cuadros = cuadros / (ahora - t_fps), ahora, 0

            dibujar(cv2, frame, manos, gestos, paquete, fps)
            alto = int(frame.shape[0] * ancho_preview / frame.shape[1])
            cv2.imshow("DINO WEST - Manos", cv2.resize(frame, (ancho_preview, alto)))
            tecla = cv2.waitKey(1) & 0xFF
            if tecla in (ord("q"), 27):
                break
            if tecla in (ord("c"), ord("C")):
                # Cambiar a la siguiente cámara que funcione y recordarla.
                cap.release()
                nueva, cap_nueva = siguiente_camara(cv2, camara)
                if cap_nueva is None:
                    cap = abrir_camara(cv2, camara)
                else:
                    camara, cap = nueva, cap_nueva
                    guardar_camara(camara)
                    print(f"[DinoManos] Cámara cambiada a {camara}.")
                if cap is None:
                    sys.exit("Se perdió la cámara.")
    finally:
        # Último paquete sin manos: Unity suelta el acelerador al instante.
        enviar(sock, seq + 1, {"hands": 0, "mode": "none", "steer": 0, "throttle": 0, "sprint": 0, "brake": 0,
                               "cx": 0.5, "cy": 0.5, "pinch": 0, "jumpId": gestos.jump_id,
                               "roarId": gestos.roar_id, "pauseId": gestos.pause_id})
        detector.cerrar()
        cap.release()
        cv2.destroyAllWindows()


def main():
    ap = argparse.ArgumentParser(description="Control de DINO WEST con las manos (MediaPipe -> UDP -> Unity)")
    ap.add_argument("--camara", type=int, default=None,
                    help="índice de la webcam; si no se indica se usa la elegida la última vez")
    ap.add_argument("--elegir", action="store_true", help="muestra la ventana para elegir cámara")
    ap.add_argument("--hz", type=int, default=45, help="paquetes por segundo (30-60)")
    ap.add_argument("--preview", type=int, default=480, help="ancho de la ventana de preview en píxeles")
    ap.add_argument("--prueba", action="store_true", help="sin webcam: envía datos sintéticos para probar Unity")
    args = ap.parse_args()

    hz = max(30, min(60, args.hz))
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        if args.prueba:
            modo_prueba(sock, hz)
        else:
            modo_camara(sock, args.camara, hz, args.preview, args.elegir)
    finally:
        sock.close()


if __name__ == "__main__":
    main()
