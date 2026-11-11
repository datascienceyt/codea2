"""Dibuja los diagramas de Docs/Diagramas (SVG) y los pasa a JPG con Edge y PowerShell.

Uso, desde la raíz del repositorio:  python Docs/Herramientas/generar_diagramas.py

Cada diagrama se describe aquí con cajas y flechas; para cambiar uno, editar su función y
volver a generar. Los JPG son los que usan el README y el informe del mes 3.
"""
import os, subprocess, sys
from html import escape

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Docs", "Diagramas")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

NAVY, ORANGE, TEAL, GREY = "#1E4466", "#F28C28", "#2FB3B5", "#5B6675"
FILL = {"navy": ("#E3ECF5", NAVY), "orange": ("#FDEBD6", ORANGE), "teal": ("#DDF4F4", "#1E8E90"),
        "grey": ("#EEF1F5", GREY), "green": ("#E1F5E6", "#2E8B4E"), "solid": (NAVY, NAVY)}


class Svg:
    def __init__(self, w, h, title):
        self.w, self.h, self.parts = w, h, []
        self.text(w / 2, 44, title, size=30, bold=True, color=NAVY)

    def text(self, x, y, s, size=20, bold=False, color="#1B2430", anchor="middle"):
        self.parts.append(f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{700 if bold else 400}" '
                          f'fill="{color}" text-anchor="{anchor}">{escape(s)}</text>')

    def box(self, x, y, w, h, title, lines=(), kind="navy", title_size=21, line_size=17):
        fill, stroke = FILL[kind]
        ink = "#FFFFFF" if kind == "solid" else "#1B2430"
        self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="12" fill="{fill}" stroke="{stroke}" stroke-width="2.5"/>')
        total = title_size + len(lines) * (line_size + 7)
        ty = y + (h - total) / 2 + title_size - 3
        self.text(x + w / 2, ty, title, size=title_size, bold=True, color=ink)
        for i, line in enumerate(lines):
            self.text(x + w / 2, ty + (i + 1) * (line_size + 7), line, size=line_size, color=ink if kind == "solid" else "#3A4656")

    def frame(self, x, y, w, h, label):
        self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="14" fill="none" stroke="{GREY}" stroke-width="2" stroke-dasharray="8 6"/>')
        self.text(x + 16, y + 26, label, size=18, bold=True, color=GREY, anchor="start")

    def arrow(self, points, label=None, color=NAVY, dashed=False, label_at=None):
        d = "M " + " L ".join(f"{x} {y}" for x, y in points)
        dash = ' stroke-dasharray="7 6"' if dashed else ""
        self.parts.append(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="2.5"{dash} marker-end="url(#a{color[1:]})"/>')
        if label:
            (x1, y1), (x2, y2) = points[0], points[1]
            lx, ly = label_at or ((x1 + x2) / 2, (y1 + y2) / 2 - 8)
            self.text(lx, ly, label, size=15, color=color)

    def render(self):
        markers = "".join(f'<marker id="a{c[1:]}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
                          f'<path d="M 0 0 L 10 5 L 0 10 z" fill="{c}"/></marker>' for c in (NAVY, ORANGE, GREY, "#2E8B4E"))
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}" '
                f'font-family="Arial, Helvetica, sans-serif"><defs>{markers}</defs>'
                f'<rect width="{self.w}" height="{self.h}" fill="#FFFFFF"/>' + "".join(self.parts) + "</svg>")


def flujo_sesion():
    s = Svg(1200, 640, "Recorrido de una sesión")
    y, h = 90, 150
    s.box(30, y, 200, h, "Setup", ["El supervisor pone", "el PIN y elige la", "dificultad"], "orange")
    s.box(270, y, 180, h, "Tutorial", ["Practicar: pulsar,", "encajar y colocar.", "Botón Empezar"], "grey")
    s.arrow([(230, y + h / 2), (270, y + h / 2)])
    s.arrow([(450, y + h / 2), (490, y + h / 2)])

    s.frame(490, 70, 680, 330, "Juego · reloj de la sesión: 15 minutos, corre solo durante los retos")
    rooms = [("Sala 1", "Secuencialidad", ["Guiar a Roki", "hasta el botón"]), ("Sala 2", "Condicionales", ["Reparar los", "tres sistemas"]),
             ("Sala 3", "Bucles", ["Clasificar la carga", "con el brazo"]), ("Sala 4", "Patrones", ["Encontrar la ficha", "idéntica"])]
    for i, (name, pillar, lines) in enumerate(rooms):
        x = 508 + i * 164
        s.box(x, 112, 148, 128, name, [pillar] + lines, "navy", title_size=20, line_size=15)
        if i:
            s.arrow([(x - 16, 176), (x, 176)])
    s.box(508, 270, 644, 108, "Máximo 5 minutos por sala", ["Resuelta o agotada, se abre la siguiente. Si la sala se agota, queda registrada",
          "como no resuelta. Si se agotan los 15 minutos, la sesión se cierra."], "teal", title_size=19, line_size=15)

    s.box(690, 450, 300, 110, "Regreso a la Tierra", ["Celebración al completar", "la sala 4"], "green")
    s.arrow([(1100, 400), (1100, 505), (990, 505)])
    s.box(330, 450, 300, 110, "Cierre y subida de datos", ["Un archivo JSON por", "participante"], "orange")
    s.arrow([(690, 505), (630, 505)])
    s.arrow([(330, 505), (130, 505), (130, y + h)], "vuelve a Setup", label_at=(230, 492))
    s.arrow([(560, 400), (560, 450)], "tiempo agotado", color=ORANGE, dashed=True, label_at=(628, 430))
    return "flujo_sesion", s


def arquitectura():
    s = Svg(1200, 760, "Subsistemas del módulo y cómo se comunican")
    s.frame(30, 70, 250, 250, "Escena Setup")
    s.box(50, 110, 210, 90, "Sesión", ["SessionSetup · PinEntry"], "orange", line_size=15)
    s.box(50, 215, 210, 90, "Sesiones pendientes", ["TelemetryOutbox"], "orange", title_size=18, line_size=15)

    s.frame(310, 70, 860, 660, "Escena Juego")
    s.box(620, 105, 240, 86, "Director", ["Tramos y pasos de la historia"], "solid", line_size=15)
    s.box(340, 105, 230, 86, "Narración", ["Narrator · voz y texto"], "grey", line_size=15)
    s.box(910, 105, 240, 86, "Tiempo", ["ScenarioTimeLimit · Timer"], "grey", line_size=15)
    s.arrow([(620, 148), (570, 148)], "dice", label_at=(595, 138))
    s.arrow([(910, 148), (860, 148)], "cierra", label_at=(885, 138))

    s.box(340, 250, 810, 120, "Salas", ["Scenario1Controller … Scenario4Controller", "LevelLoader · Bot · SystemModule · RoboticArm · ShapeSocket"], "navy", line_size=16)
    s.arrow([(740, 191), (740, 250)], "activa cada sala y espera a que termine", label_at=(905, 226))

    s.box(340, 420, 250, 110, "Programación por bloques", ["BlockNode · Socket · SocketRow", "ProgramRunner · ProgramTrigger"], "teal", title_size=18, line_size=14)
    s.box(620, 420, 250, 110, "Dificultad", ["DifficultyApplier", "DifficultyVariant · DifficultyOnly"], "teal", title_size=18, line_size=14)
    s.box(900, 420, 250, 110, "Interacción", ["Meta XR SDK · VRGrabEvents", "ControllerReach · Teleporter"], "teal", title_size=18, line_size=14)
    s.arrow([(465, 420), (465, 370)], "salas 1 y 3", label_at=(520, 400))
    s.arrow([(745, 420), (745, 370)], "configura", label_at=(795, 400))
    s.arrow([(1025, 420), (1025, 370)], "agarrar, pulsar", label_at=(1092, 400))

    s.box(340, 590, 810, 110, "Telemetría", ["TelemetryManager registra intentos, tiempos y errores de cada sala",
          "TelemetryData (modelo) · JSONUploader (envío)"], "orange", line_size=16)
    s.arrow([(560, 530), (560, 590)], color=ORANGE)
    s.arrow([(930, 370), (930, 400), (875, 400), (875, 590)], "eventos de cada reto", color=ORANGE, label_at=(790, 566))

    s.box(50, 590, 210, 110, "Servidor", ["POST /upload", "GET /health"], "solid", line_size=16)
    s.arrow([(340, 645), (260, 645)], "JSON", color=ORANGE, label_at=(283, 672))
    s.arrow([(155, 305), (155, 590)], "pendientes", color=ORANGE, label_at=(205, 450))
    s.arrow([(260, 155), (310, 155)], "PIN", label_at=(285, 180))
    return "arquitectura", s


def flujo_datos():
    s = Svg(1200, 520, "Camino de los datos de una sesión")
    s.frame(30, 70, 755, 420, "En el visor (funciona sin conexión)")
    s.box(50, 120, 210, 120, "Lo que hace el niño", ["Intentos, tiempos,", "errores y aciertos"], "navy", title_size=19, line_size=16)
    s.box(300, 120, 210, 120, "Registro", ["TelemetryManager", "lo anota al momento"], "navy", line_size=16)
    s.box(550, 120, 220, 120, "Archivo de la sesión", ["PIN_sesión_visor.json", "guardado en el visor"], "orange", title_size=19, line_size=16)
    s.arrow([(260, 180), (300, 180)])
    s.arrow([(510, 180), (550, 180)])

    s.box(850, 120, 320, 120, "Servidor de Yachay Tech", ["datascience.yachaytech.edu.ec", "POST /upload con clave"], "solid", line_size=16)
    s.arrow([(770, 180), (850, 180)], "Wi-Fi", color=ORANGE, label_at=(820, 168))

    s.box(550, 330, 220, 120, "Sesiones pendientes", ["Si no hay red, el archivo", "espera en el visor"], "grey", title_size=19, line_size=15)
    s.arrow([(660, 240), (660, 330)], "sin red", color=GREY, dashed=True, label_at=(700, 290))
    s.arrow([(770, 390), (1010, 390), (1010, 240)], "se envían solas al abrir Setup, o con «Subir datos»", color=ORANGE, label_at=(985, 418))
    s.box(50, 330, 460, 120, "Sin Wi-Fi en el aula", ["Los archivos se copian por cable USB", "con Meta Quest Developer Hub o adb pull"], "grey", line_size=16)
    s.arrow([(550, 390), (510, 390)], color=GREY, dashed=True)
    return "flujo_datos", s


def main():
    os.makedirs(os.path.join(OUT, "fuentes"), exist_ok=True)
    for name, svg in (flujo_sesion(), arquitectura(), flujo_datos()):
        src = os.path.join(OUT, "fuentes", name + ".svg")
        with open(src, "w", encoding="utf-8") as f:
            f.write(svg.render())
        html = os.path.join(OUT, "fuentes", name + ".html")
        with open(html, "w", encoding="utf-8") as f:
            f.write(f'<!doctype html><meta charset="utf-8"><style>html,body{{margin:0;background:#fff}}img{{display:block}}</style><img src="{name}.svg">')
        png = os.path.join(OUT, name + ".png")
        subprocess.run([EDGE, "--headless", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=2",
                        f"--window-size={svg.w},{svg.h}", f"--screenshot={png}", "file:///" + html.replace("\\", "/")],
                       check=True, capture_output=True)
        jpg = os.path.join(OUT, name + ".jpg")
        ps = (f"Add-Type -AssemblyName System.Drawing; $i=[Drawing.Image]::FromFile('{png}'); "
              f"$c=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | ? {{ $_.MimeType -eq 'image/jpeg' }}; "
              f"$p=New-Object Drawing.Imaging.EncoderParameters 1; $p.Param[0]=New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality, [long]92); "
              f"$i.Save('{jpg}', $c, $p); $i.Dispose()")
        subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=True)
        os.remove(png)
        os.remove(html)
        print("ok", jpg)


if __name__ == "__main__":
    main()
