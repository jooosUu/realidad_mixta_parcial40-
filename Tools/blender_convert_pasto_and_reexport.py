"""
Convierte el Particle System 'Pasto' (tipo PATH/hair) del Plane a malla
y reexporta FBX+GLB a Assets/Dino/Models/Environment/Volcan/
"""
from __future__ import annotations

from pathlib import Path
import bpy


def log(msg: str) -> None:
    print(f"[Pasto→Mesh] {msg}")


def repo_root() -> Path:
    p = Path(bpy.data.filepath).resolve()
    for parent in [p.parent, *p.parents]:
        if (parent / "Assets").is_dir() and (parent / "ProjectSettings").is_dir():
            return parent
    raise RuntimeError("No se encontró la raíz del repo Unity")


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def main() -> None:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    terrain = bpy.data.objects.get("Plane")
    if terrain is None:
        log("ERROR: no hay objeto Plane")
        return

    # Localizar modifier de partículas Pasto
    pasto_mod = None
    for mod in terrain.modifiers:
        if mod.type != "PARTICLE_SYSTEM":
            continue
        ps = mod.particle_system
        name = (ps.name if ps else mod.name).casefold()
        if "pasto" in name or "grass" in name or "hierba" in name:
            pasto_mod = mod
            break

    grass = bpy.data.objects.get("Pasto_Volcan")

    if pasto_mod is not None:
        log(f"Convirtiendo modifier '{pasto_mod.name}' (PS '{pasto_mod.particle_system.name}') a malla…")
        before = {o.name for o in bpy.data.objects}
        set_active(terrain)
        try:
            bpy.ops.object.modifier_convert(modifier=pasto_mod.name)
        except Exception as e:
            log(f"modifier_convert falló: {e}")
            # Fallback Blender 4+/5: Convert To Mesh sobre el objeto con PS
            try:
                set_active(terrain)
                bpy.ops.object.convert(target="MESH", keep_original=True)
            except Exception as e2:
                log(f"convert también falló: {e2}")

        new_objs = [o for o in bpy.data.objects if o.name not in before and o.type == "MESH"]
        if new_objs:
            bpy.ops.object.select_all(action="DESELECT")
            for o in new_objs:
                o.select_set(True)
            bpy.context.view_layer.objects.active = new_objs[0]
            if len(new_objs) > 1:
                bpy.ops.object.join()
            grass = bpy.context.view_layer.objects.active
            grass.name = "Pasto_Volcan"
            log(f"Pasto mesh creado: {grass.name} ({len(grass.data.vertices)} verts)")
        else:
            # A veces modifier_convert reemplaza / renombra sin "new" claro
            cand = bpy.context.view_layer.objects.active
            if cand and cand != terrain and cand.type == "MESH":
                cand.name = "Pasto_Volcan"
                grass = cand
                log(f"Pasto mesh (activo): {grass.name}")
            else:
                log("No se generó malla de pasto.")

        # Quitar PS Pasto del terreno si sigue
        for mod in list(terrain.modifiers):
            if mod.type == "PARTICLE_SYSTEM":
                ps = mod.particle_system
                n = (ps.name if ps else mod.name).casefold()
                if "pasto" in n or "grass" in n:
                    terrain.modifiers.remove(mod)
                    log("Particle System Pasto eliminado del terreno")
    elif grass is None:
        log("No hay PS Pasto ni objeto Pasto_Volcan. Se reexporta solo el terreno.")

    # Export
    out = repo_root() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan"
    out.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="DESELECT")
    terrain.select_set(True)
    if grass is not None:
        grass.hide_set(False)
        grass.select_set(True)
    bpy.context.view_layer.objects.active = terrain

    fbx = out / "Escenario_Volcan_TerrenoPasto.fbx"
    glb = out / "Escenario_Volcan_TerrenoPasto.glb"

    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
    )
    log(f"FBX → {fbx}")

    try:
        bpy.ops.export_scene.gltf(
            filepath=str(glb),
            use_selection=True,
            export_format="GLB",
            export_apply=True,
            export_texcoords=True,
            export_normals=True,
            export_materials="EXPORT",
            export_yup=True,
        )
    except TypeError:
        bpy.ops.export_scene.gltf(
            filepath=str(glb),
            use_selection=True,
            export_format="GLB",
            export_texcoords=True,
            export_normals=True,
            export_materials="EXPORT",
            export_yup=True,
        )
    log(f"GLB → {glb}")

    if bpy.data.filepath:
        bpy.ops.wm.save_mainfile()
        log("Blend guardado")


if __name__ == "__main__":
    main()
