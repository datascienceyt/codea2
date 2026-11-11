"""Exporta a Unity el Roki elegido (B, Rodante) como FBX partido en piezas animables.

Uso (sin abrir Blender):
  blender -b --python Docs/Roki/exportar_roki.py -- "Assets/_Main/3D Models/Roki/Roki.fbx"

Piezas: Cuerpo, Cabeza (pivote en el cuello), Brazo_I y Brazo_D (pivote en el hombro).
El frente es -Y en Blender, que en Unity queda +Z: el frente logico de Bot.cs.
"""
import bpy, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generar_roki as g

OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])

HEAD = ("Cabeza", "Pantalla", "Ojo_", "Sonrisa", "Flecha_Frente", "Antena", "Oreja_")
GROUPS = {
    "Cabeza": (lambda n: n.startswith(HEAD), (0, 0, 0.67)),
    "Brazo_I": (lambda n: n in ("Brazo_I", "Mano_I"), (-0.26, -0.02, 0.50)),
    "Brazo_D": (lambda n: n in ("Brazo_D", "Mano_D"), (0.26, -0.02, 0.50)),
    "Cuerpo": (lambda n: True, (0, 0, 0)),
}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    robot = g.build_rodante((0, 0, 0))
    parts = [o for o in robot.col.objects if o.type in ("MESH", "CURVE")]

    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    pending = {o.name: o for o in parts}
    result = []
    for name, (match, pivot) in GROUPS.items():
        members = [o for n, o in pending.items() if match(n.split(".")[0])]
        for o in members:
            del pending[o.name]
        bpy.ops.object.select_all(action="DESELECT")
        for o in members:
            o.select_set(True)
        bpy.context.view_layer.objects.active = members[0]
        bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        o.name = o.data.name = "__tmp"
        bpy.context.scene.cursor.location = pivot
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.material_slot_remove_unused()
        result.append((name, o))
    for name, o in result:
        o.name = o.data.name = name
        o.data.calc_loop_triangles()
        print(f"PIEZA {name}: {len(o.data.loop_triangles)} tris, materiales {[m.name for m in o.data.materials]}")

    bpy.ops.object.select_all(action="DESELECT")
    for _, o in result:
        o.select_set(True)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="OFF",
                             add_leaf_bones=False, bake_anim=False)
    print("FBX", OUT)


main()
