"""Genera las propuestas de modelo para Roki y las renderiza para compararlas.

Uso (sin abrir Blender):
  blender -b --python Docs/Roki/generar_roki.py -- Docs/Roki/opciones

Los tres robots miran hacia -Y (al exportar a FBX queda +Z en Unity, el frente de Bot.prefab).
Son rigidos: sin esqueleto, pensados para animarse por codigo (flotar, parpadear, mover la mano).
"""
import bpy, math, os, sys
from mathutils import Vector

OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else "."
R = math.radians


# ---------- materiales ----------
def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexc(h):
    h = h.lstrip("#")
    return tuple(_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


MATS = {}


def mat(name, color, rough=0.45, metal=0.0, emit=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = hexc(color)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit > 0:
        b.inputs["Emission Color"].default_value = hexc(color)
        b.inputs["Emission Strength"].default_value = emit
    MATS[name] = m
    return m


def WHITE(): return mat("Blanco", "#F2F4F7", 0.35)
def TEAL(): return mat("Turquesa", "#2FB3B5", 0.4)
def NAVY(): return mat("Azul_Codea", "#1E4466", 0.4)
def SCREEN(): return mat("Pantalla", "#0E2238", 0.12)
def ORANGE(): return mat("Naranja_Codea", "#F28C28", 0.4)
def GREY(): return mat("Gris", "#8D99A8", 0.4, 0.3)
def DARK(): return mat("Oruga", "#2B3340", 0.6)
def GLOW(): return mat("Luz_Cian", "#5FE9F5", 0.3, 0, 1.1)
def GLOW_O(): return mat("Luz_Naranja", "#FFB04A", 0.3, 0, 0.9)
def GLOW_G(): return mat("Luz_Verde", "#6CF08A", 0.3, 0, 1.0)
def GREEN(): return mat("Verde_Colibri", "#12A07A", 0.3, 0.15)
def BELLY(): return mat("Panza", "#EAF7F0", 0.4)
def MAGENTA(): return mat("Garganta", "#C2337E", 0.3, 0.2)
def WING(): return mat("Ala", "#9FE3EA", 0.35)
def HILITE(): return mat("Brillo", "#FFFFFF", 0.3, 0, 1.2)


# ---------- constructor ----------
class Robot:
    def __init__(self, name, pos):
        self.col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(self.col)
        self.root = bpy.data.objects.new(name, None)
        self.root.location = pos
        self.col.objects.link(self.root)

    def _fin(self, name, m, sharp=None):
        o = bpy.context.active_object
        o.name = name
        for c in list(o.users_collection):
            c.objects.unlink(o)
        self.col.objects.link(o)
        o.parent = self.root
        if m:
            o.data.materials.append(m)
        if o.type == "MESH":
            for p in o.data.polygons:
                p.use_smooth = True
            if sharp:
                o.data.set_sharp_from_angle(angle=R(sharp))
        return o

    def sphere(self, name, loc, scale, m, rot=(0, 0, 0), segs=24, rings=12):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=1, location=loc, rotation=rot)
        o = self._fin(name, m)
        o.scale = scale if isinstance(scale, (tuple, list)) else (scale,) * 3
        return o

    def box(self, name, loc, dims, m, bevel=0.04, segs=3, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
        o = bpy.context.active_object
        o.scale = dims
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if bevel > 0:
            b = o.modifiers.new("b", "BEVEL")
            b.width, b.segments = bevel, segs
            bpy.ops.object.modifier_apply(modifier="b")
        return self._fin(name, m, sharp=35)

    def cyl(self, name, loc, r, depth, m, axis="Z", verts=24, r2=None, sharp=40, scale=None):
        rot = {"Z": (0, 0, 0), "X": (0, R(90), 0), "Y": (R(90), 0, 0)}[axis]
        if r2 is None:
            bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
        else:
            bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=rot)
        o = self._fin(name, m, sharp=sharp)
        if scale:
            o.scale = scale
        return o

    def rod(self, name, p0, p1, r, m, verts=12):
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(p0 + p1) / 2)
        o = self._fin(name, m, sharp=40)
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = d.to_track_quat("Z", "Y")
        return o

    def torus(self, name, loc, major, minor, m, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=8,
                                         location=loc, rotation=rot)
        return self._fin(name, m)

    def arc(self, name, loc, radius, thick, a0, a1, m, n=12):
        """Tubo en arco sobre el plano XZ (el de la cara). Angulos en grados."""
        cu = bpy.data.curves.new(name, "CURVE")
        cu.dimensions = "3D"
        sp = cu.splines.new("POLY")
        sp.points.add(n)
        for i in range(n + 1):
            t = R(a0 + (a1 - a0) * i / n)
            sp.points[i].co = (radius * math.cos(t), 0, radius * math.sin(t), 1)
        cu.bevel_depth, cu.bevel_resolution, cu.use_fill_caps = thick, 2, True
        cu.materials.append(m)
        o = bpy.data.objects.new(name, cu)
        o.location = loc
        self.col.objects.link(o)
        o.parent = self.root
        return o

    def chevron(self, name, loc, size, m, h=0.02):
        """Flecha plana que apunta al frente (-Y), para leer la direccion desde arriba."""
        s = size
        pts = [(0, -s), (s, 0.1 * s), (s, 0.6 * s), (0, -0.3 * s), (-s, 0.6 * s), (-s, 0.1 * s)]
        v = [(x, y, 0) for x, y in pts] + [(x, y, h) for x, y in pts]
        n = len(pts)
        f = [(0, 5, 4, 3), (0, 3, 2, 1), (6, 9, 10, 11), (6, 7, 8, 9)]
        f += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
        me = bpy.data.meshes.new(name)
        me.from_pydata(v, [], f)
        me.materials.append(m)
        o = bpy.data.objects.new(name, me)
        o.location = loc
        self.col.objects.link(o)
        o.parent = self.root
        return o

    def face(self, y, z, eye_dx, eye=(0.052, 0.022, 0.075), smile_r=0.065, smile_dz=-0.075):
        for s, side in ((-1, "I"), (1, "D")):
            self.sphere("Ojo_" + side, (s * eye_dx, y, z), eye, GLOW(), segs=16, rings=8)
        self.arc("Sonrisa", (0, y - 0.004, z + smile_dz + smile_r * 0.6), smile_r, 0.011, 205, 335, GLOW())


# ---------- A · Orbital: evolucion del Roki flotante actual ----------
def build_orbital(pos):
    r = Robot("A_Orbital", pos)
    r.cyl("Propulsor", (0, 0, 0.17), 0.10, 0.10, NAVY(), r2=0.17)
    r.cyl("Propulsor_Luz", (0, 0, 0.115), 0.085, 0.02, GLOW())
    r.sphere("Cuerpo", (0, 0, 0.43), (0.27, 0.25, 0.27), WHITE())
    r.torus("Cinturon", (0, 0, 0.385), 0.255, 0.035, TEAL())
    r.cyl("Pecho_Luz", (0, -0.235, 0.50), 0.06, 0.04, GLOW_O(), axis="Y")
    r.box("Mochila", (0, 0.25, 0.48), (0.30, 0.16, 0.26), ORANGE(), bevel=0.05)
    r.box("Mochila_Franja", (0, 0.335, 0.48), (0.16, 0.02, 0.06), NAVY(), bevel=0.008)
    for s, side in ((-1, "I"), (1, "D")):
        r.sphere("Mano_" + side, (s * 0.38, -0.06, 0.40), (0.07, 0.08, 0.085), TEAL(), segs=16, rings=8)
        r.cyl("Oreja_" + side, (s * 0.35, 0, 0.87), 0.085, 0.07, TEAL(), axis="X")
    r.sphere("Cabeza", (0, 0, 0.87), (0.36, 0.32, 0.29), WHITE())
    r.sphere("Visor", (0, -0.16, 0.86), (0.29, 0.20, 0.20), SCREEN())
    r.face(-0.346, 0.885, 0.115)
    r.rod("Antena", (0, 0.03, 1.14), (0, 0.03, 1.29), 0.012, GREY())
    r.sphere("Antena_Luz", (0, 0.03, 1.31), 0.042, GLOW_O(), segs=16, rings=8)
    return r


# ---------- B · Rodante: orugas, lectura clarisima de avanzar y girar ----------
def build_rodante(pos):
    r = Robot("B_Rodante", pos)
    for s, side in ((-1, "I"), (1, "D")):
        r.box("Oruga_" + side, (s * 0.31, 0, 0.13), (0.15, 0.70, 0.26), DARK(), bevel=0.125, segs=5)
        for i, y in enumerate((-0.21, 0, 0.21)):
            r.cyl(f"Rueda_{side}{i}", (s * 0.385, y, 0.13), 0.07, 0.03, GREY(), axis="X")
        r.sphere("Hombro_" + side, (s * 0.255, -0.02, 0.50), 0.055, GREY(), segs=16, rings=8)
        r.rod("Brazo_" + side, (s * 0.26, -0.02, 0.50), (s * 0.28, -0.25, 0.40), 0.028, GREY())
        r.sphere("Mano_" + side, (s * 0.28, -0.27, 0.39), 0.058, NAVY(), segs=16, rings=8)
        r.cyl("Oreja_" + side, (s * 0.30, -0.02, 0.84), 0.065, 0.05, ORANGE(), axis="X")
    r.box("Chasis", (0, 0, 0.20), (0.50, 0.56, 0.14), NAVY(), bevel=0.04)
    r.box("Cuerpo", (0, 0, 0.44), (0.44, 0.40, 0.32), ORANGE(), bevel=0.09, segs=4)
    r.box("Pecho_Panel", (0, -0.20, 0.45), (0.24, 0.03, 0.11), SCREEN(), bevel=0.012)
    for x, m in ((-0.07, GLOW_G()), (0, GLOW()), (0.07, GLOW_G())):
        r.sphere("Pecho_Luz", (x, -0.217, 0.45), 0.02, m, segs=12, rings=6)
    r.box("Bateria", (0, 0.225, 0.45), (0.30, 0.08, 0.20), NAVY(), bevel=0.03)
    for x in (-0.08, 0, 0.08):
        r.box("Bateria_Barra", (x, 0.268, 0.45), (0.05, 0.02, 0.12), GLOW_G(), bevel=0.008)
    r.cyl("Cuello", (0, 0, 0.63), 0.075, 0.08, GREY())
    r.box("Cabeza", (0, -0.02, 0.84), (0.58, 0.36, 0.34), WHITE(), bevel=0.11, segs=5)
    r.box("Pantalla", (0, -0.195, 0.84), (0.46, 0.04, 0.24), SCREEN(), bevel=0.018)
    r.face(-0.214, 0.86, 0.115, eye=(0.055, 0.015, 0.07), smile_r=0.06, smile_dz=-0.085)
    r.chevron("Flecha_Frente", (0, -0.03, 1.008), 0.085, NAVY())
    r.rod("Antena", (0.19, 0.09, 1.0), (0.19, 0.09, 1.13), 0.012, GREY())
    r.sphere("Antena_Luz", (0.19, 0.09, 1.15), 0.038, GLOW(), segs=16, rings=8)
    return r


# ---------- C · Colibri: dron inspirado en el colibri andino; el pico marca el frente ----------
def build_colibri(pos):
    r = Robot("C_Colibri", pos)
    r.torus("Aro_Flotador", (0, 0.03, 0.22), 0.11, 0.022, NAVY())
    r.cyl("Aro_Luz", (0, 0.03, 0.205), 0.075, 0.02, GLOW())
    r.sphere("Cuerpo", (0, 0.05, 0.50), (0.24, 0.31, 0.24), GREEN(), rot=(R(-12), 0, 0))
    r.sphere("Panza", (0, -0.03, 0.455), (0.20, 0.26, 0.20), BELLY(), rot=(R(-12), 0, 0))
    r.sphere("Cabeza", (0, -0.20, 0.76), 0.215, GREEN())
    r.sphere("Garganta", (0, -0.255, 0.555), (0.10, 0.07, 0.06), MAGENTA(), segs=16, rings=8)
    r.torus("Collar", (0, -0.13, 0.615), 0.17, 0.022, NAVY(), rot=(R(-50), 0, 0))
    r.cyl("Pico", (0, -0.57, 0.735), 0.04, 0.36, ORANGE(), axis="Y", r2=0.009, verts=12, sharp=80)
    for s, side in ((-1, "I"), (1, "D")):
        r.sphere("Ojo_" + side, (s * 0.105, -0.372, 0.79), (0.075, 0.035, 0.085), SCREEN(), segs=16, rings=8)
        r.sphere("Iris_" + side, (s * 0.105, -0.398, 0.79), (0.044, 0.016, 0.052), GLOW(), segs=16, rings=8)
        r.sphere("Brillo_" + side, (s * 0.105 - 0.018, -0.412, 0.815), 0.013, HILITE(), segs=8, rings=4)
        r.sphere("Ala_Eje_" + side, (s * 0.215, 0.02, 0.58), 0.055, NAVY(), segs=16, rings=8)
        r.sphere("Ala_" + side, (s * 0.43, 0.09, 0.66), (0.26, 0.115, 0.018), WING(),
                 rot=(0, R(-22) * s, R(-18) * s), segs=16, rings=8)
    for i, a in enumerate((-24, 0, 24)):
        length = 0.36 if a == 0 else 0.30
        x = math.sin(R(a)) * (length / 2 + 0.20)
        y = 0.05 + math.cos(R(a)) * (length / 2 + 0.20)
        r.box(f"Cola_{i}", (x, y, 0.40), (0.11, length, 0.024), NAVY() if a == 0 else TEAL(),
              bevel=0.01, rot=(R(-24), 0, R(-a)))
    r.rod("Cresta", (0, -0.17, 0.96), (0, -0.14, 1.06), 0.011, GREY())
    r.sphere("Cresta_Luz", (0, -0.135, 1.08), 0.034, GLOW_O(), segs=16, rings=8)
    return r


# ---------- escena ----------
def stage(xs):
    sc = bpy.context.scene
    floor_m = mat("Suelo", "#AEB7C6", 0.8)
    tile_m = mat("Casilla", "#6B7488", 0.7)
    inset_m = mat("Casilla_Centro", "#C9CCE4", 0.6)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.03))
    bpy.context.active_object.data.materials.append(floor_m)
    st = Robot("Escenario", (0, 0, 0))
    for x in xs:
        st.box("Casilla", (x, 0, -0.015), (1.25, 1.25, 0.03), tile_m, bevel=0.01)
        st.box("Casilla_Centro", (x, 0, -0.008), (0.95, 0.95, 0.03), inset_m, bevel=0.012)
    w = bpy.data.worlds.new("Mundo")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value, bg.inputs[1].default_value = hexc("#C8D2E2"), 0.55
    sc.world = w

    def light(kind, loc, energy, target=(0, 0, 0.5), **kw):
        d = bpy.data.lights.new(kind, kind)
        d.energy = energy
        for k, v in kw.items():
            setattr(d, k, v)
        o = bpy.data.objects.new(kind, d)
        o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(o)
        return o

    sun_front = light("SUN", (3, -5, 7), 1.7, angle=R(12))
    sun_back = light("SUN", (-3, 5, 7), 1.7, angle=R(12))
    light("AREA", (-5, -3, 3), 110, size=5)
    light("AREA", (5, 3, 3), 110, size=5)
    return sun_front, sun_back


def camera(name, loc, target, lens=40):
    d = bpy.data.cameras.new(name)
    d.lens = lens
    o = bpy.data.objects.new(name, d)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(o)
    return o


def label(text, loc, back=False, size=0.2):
    bpy.ops.object.text_add(location=loc, rotation=(0, 0, R(180) if back else 0))
    o = bpy.context.active_object
    o.data.body, o.data.size, o.data.align_x, o.data.extrude = text, size, "CENTER", 0.004
    o.data.materials.append(mat("Rotulo", "#1E4466", 0.6))
    return o


def tris(col):
    dg = bpy.context.evaluated_depsgraph_get()
    n = 0
    for o in col.objects:
        if o.type in ("MESH", "CURVE"):
            ev = o.evaluated_get(dg)
            me = ev.to_mesh()
            me.calc_loop_triangles()
            n += len(me.loop_triangles)
            ev.to_mesh_clear()
    return n


def render(path, cam, res=(1600, 900)):
    sc = bpy.context.scene
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            pass
    sc.view_settings.view_transform = "Standard"
    try:
        sc.eevee.taa_render_samples = 64
    except Exception:
        pass

    xs = (-1.55, 0, 1.55)
    names = ("A  ORBITAL", "B  RODANTE", "C  COLIBRÍ")
    sun_front, sun_back = stage(xs)
    robots = [build_orbital((xs[0], 0, 0.06)), build_rodante((xs[1], 0, 0)), build_colibri((xs[2], 0, 0.06))]

    front = [label(n, (x, -0.98, 0)) for n, x in zip(names, xs)]
    back = [label(n, (x, 0.98, 0), back=True) for n, x in zip(names, xs)]

    def show(front_on):
        for o in front:
            o.hide_render = not front_on
        for o in back:
            o.hide_render = front_on
        sun_front.hide_render, sun_back.hide_render = not front_on, front_on

    os.makedirs(OUT, exist_ok=True)
    show(True)
    render(os.path.join(OUT, "comparativa_frente.png"), camera("Cam_Frente", (1.5, -5.6, 1.7), (0, 0, 0.5)))
    show(False)
    # Como se ve en la sala 1: de espaldas y desde arriba, con el nino tras la mesa.
    render(os.path.join(OUT, "comparativa_vista_jugador.png"), camera("Cam_Jugador", (0.6, 5.0, 3.1), (0, 0, 0.45)))
    show(True)
    for rb, x in zip(robots, xs):
        cam = camera("Cam_" + rb.root.name, (x - 1.25, -2.35, 1.05), (x, 0, 0.6), lens=50)
        render(os.path.join(OUT, rb.root.name + ".png"), cam, res=(900, 900))

    for rb in robots:
        print(f"TRIS {rb.root.name}: {tris(rb.col)}")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "roki_opciones.blend"))


if __name__ == "__main__":
    main()
