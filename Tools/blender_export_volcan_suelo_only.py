"""
Export limpio: SOLO el terreno (Plane) con material Suelo texturizado.
Sin pasto-alfombra, sin hebras, sin árboles.
"""
from __future__ import annotations

from pathlib import Path
import bpy


def log(m): print(f"[VolcanSuelo] {m}")


def repo() -> Path:
    return Path(r"C:\Users\david\OneDrive\Documentos\Octavo semestre\realidad_mixta_parcial40-")


def set_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def clear_ps(plane):
    names = [m.name for m in plane.modifiers if m.type == "PARTICLE_SYSTEM"]
    for n in names:
        m = plane.modifiers.get(n)
        if m:
            plane.modifiers.remove(m)
            log(f"PS off: {n}")
    while len(getattr(plane, "particle_systems", [])) > 0:
        plane.particle_systems.remove(plane.particle_systems[0])


def load_tex(name_part: str) -> bpy.types.Image | None:
    tex_dir = repo() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan" / "Textures"
    for f in tex_dir.iterdir():
        if name_part.lower() in f.name.lower() and f.suffix.lower() in {".jpg", ".jpeg", ".png"}:
            # Cargar fresco desde disco (evita pack paths rotos)
            img = bpy.data.images.load(str(f), check_existing=True)
            log(f"tex {name_part} <- {f.name}")
            return img
    log(f"NO tex {name_part}")
    return None


def make_suelo_mat() -> bpy.types.Material:
    mat = bpy.data.materials.get("Suelo_Unity") or bpy.data.materials.new("Suelo_Unity")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    # Tile para que la roca se vea a escala de ~90m
    mapping.inputs["Scale"].default_value = (8.0, 8.0, 8.0)

    out.location = (500, 0)
    bsdf.location = (250, 0)
    mapping.location = (-200, 0)
    coord.location = (-450, 0)

    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    def add_img(img, y, non_color=False):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (0, y)
        if non_color:
            try:
                img.colorspace_settings.name = "Non-Color"
            except Exception:
                pass
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        return tex

    col = load_tex("BaseColor")
    nrm = load_tex("Normal")
    rg = load_tex("Roughness")

    if col:
        t = add_img(col, 200)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (0.22, 0.07, 0.04, 1)

    if rg:
        t = add_img(rg, 0, non_color=True)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Roughness"])
    else:
        bsdf.inputs["Roughness"].default_value = 0.85

    if nrm:
        t = add_img(nrm, -200, non_color=True)
        normal = nt.nodes.new("ShaderNodeNormalMap")
        normal.location = (150, -200)
        nt.links.new(t.outputs["Color"], normal.inputs["Color"])
        nt.links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])

    bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def purge_extra_meshes():
    for name in ("Pasto_Volcan", "Pasto_Alfombra", "Pasto_Hebras"):
        o = bpy.data.objects.get(name)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)
            log(f"removed {name}")
    for name in ("Arboles01", "Arboles02"):
        o = bpy.data.objects.get(name)
        if o:
            o.hide_render = True
            o.hide_viewport = True


def main():
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    plane = bpy.data.objects.get("Plane")
    if not plane:
        log("ERROR no Plane")
        return

    clear_ps(plane)
    purge_extra_meshes()

    # UV si faltara
    if not plane.data.uv_layers:
        set_active(plane)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=66, island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        log("Smart UV Project")

    mat = make_suelo_mat()
    plane.data.materials.clear()
    plane.data.materials.append(mat)
    for p in plane.data.polygons:
        p.material_index = 0

    set_active(plane)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    out = repo() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan"
    out.mkdir(parents=True, exist_ok=True)
    fbx = out / "Escenario_Volcan_TerrenoPasto.fbx"
    glb = out / "Escenario_Volcan_TerrenoPasto.glb"

    bpy.ops.object.select_all(action="DESELECT")
    plane.select_set(True)
    bpy.context.view_layer.objects.active = plane

    # FBX con COPY de texturas a carpeta junto al fbx
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
    log(f"FBX {fbx} size={fbx.stat().st_size}")

    try:
        bpy.ops.export_scene.gltf(
            filepath=str(glb),
            use_selection=True,
            export_format="GLB",
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
    log(f"GLB {glb} size={glb.stat().st_size}")
    log("OK solo terreno")


if __name__ == "__main__":
    main()
