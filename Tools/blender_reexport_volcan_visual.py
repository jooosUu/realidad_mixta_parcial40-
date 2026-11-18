"""
Reexporta el volcán para Unity con:
- Material Suelo (Principled + texturas)
- Pasto con CARAS reales (alfombra + hebras)
- Sin árboles
"""
from __future__ import annotations

import math
import random
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def log(msg: str) -> None:
    print(f"[VolcanVisual] {msg}")


def repo_root() -> Path:
    p = Path(bpy.data.filepath).resolve()
    for parent in [p.parent, *p.parents]:
        if (parent / "ProjectSettings").is_dir() and (parent / "Assets" / "Dino").is_dir():
            return parent
    raise RuntimeError("No repo root")


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def load_or_find_image(*needles: str) -> bpy.types.Image | None:
    needles_l = [n.lower() for n in needles]
    for img in bpy.data.images:
        key = (img.name + " " + (img.filepath or "")).lower()
        if all(n in key for n in needles_l):
            return img
    # Disco: Assets/.../Volcan/Textures
    tex_dir = repo_root() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan" / "Textures"
    if tex_dir.is_dir():
        for f in tex_dir.iterdir():
            key = f.name.lower()
            if all(n.replace(" ", "_") in key or n in key for n in needles_l):
                return bpy.data.images.load(str(f), check_existing=True)
    return None


def ensure_principled_suelo() -> bpy.types.Material:
    mat = bpy.data.materials.get("Suelo_Unity")
    if mat is None:
        mat = bpy.data.materials.new("Suelo_Unity")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (0, 0)
    out.location = (300, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    img_color = load_or_find_image("volcanic", "basecolor") or load_or_find_image("basecolor")
    img_normal = load_or_find_image("volcanic", "normal") or load_or_find_image("rock_normal")
    img_rough = load_or_find_image("volcanic", "rough") or load_or_find_image("roughness")

    def link_tex(img, input_name, non_color=False, is_normal=False):
        if img is None:
            return
        try:
            img.pack()
        except Exception:
            pass
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (-400, 200 if input_name == "Base Color" else -200)
        if non_color:
            try:
                tex.image.colorspace_settings.name = "Non-Color"
            except Exception:
                pass
        if is_normal:
            norm = nt.nodes.new("ShaderNodeNormalMap")
            norm.location = (-150, -200)
            nt.links.new(tex.outputs["Color"], norm.inputs["Color"])
            nt.links.new(norm.outputs["Normal"], bsdf.inputs["Normal"])
        elif input_name == "Roughness":
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Roughness"])
        else:
            nt.links.new(tex.outputs["Color"], bsdf.inputs[input_name])

    link_tex(img_color, "Base Color")
    link_tex(img_rough, "Roughness", non_color=True)
    link_tex(img_normal, "Normal", non_color=True, is_normal=True)

    if img_color is None:
        bsdf.inputs["Base Color"].default_value = (0.25, 0.08, 0.04, 1.0)
        log("Suelo sin BaseColor; color volcánico plano")
    else:
        log(f"Suelo BaseColor={img_color.name}")

    bsdf.inputs["Metallic"].default_value = 0.0
    if img_rough is None:
        bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def ensure_pasto_material() -> bpy.types.Material:
    mat = bpy.data.materials.get("Pasto_Unity")
    if mat is None:
        mat = bpy.data.materials.new("Pasto_Unity")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (0.55, 0.22, 0.06, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Metallic"].default_value = 0.0
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def assign_only_suelo(plane: bpy.types.Object, suelo: bpy.types.Material) -> None:
    plane.data.materials.clear()
    plane.data.materials.append(suelo)
    for poly in plane.data.polygons:
        poly.material_index = 0
    log("Plane: solo material Suelo_Unity")


def build_pasto_alfombra(plane: bpy.types.Object, pasto_mat: bpy.types.Material) -> bpy.types.Object | None:
    vg = plane.vertex_groups.get("Pasto")
    if vg is None:
        for g in plane.vertex_groups:
            if "pasto" in g.name.lower() or "grass" in g.name.lower():
                vg = g
                break
    if vg is None:
        log("No hay vertex group Pasto; no se crea alfombra")
        return None

    weights = {}
    for i, _v in enumerate(plane.data.vertices):
        try:
            weights[i] = vg.weight(i)
        except RuntimeError:
            weights[i] = 0.0

    bm = bmesh.new()
    bm.from_mesh(plane.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    keep_faces = []
    for f in bm.faces:
        avg = sum(weights.get(v.index, 0.0) for v in f.verts) / max(1, len(f.verts))
        if avg >= 0.18:
            keep_faces.append(f)

    if not keep_faces:
        bm.free()
        log("Ninguna cara con peso Pasto; alfombra vacía")
        return None

    bm2 = bmesh.new()
    vert_map = {}
    up = Vector((0, 0, 0.04))
    for f in keep_faces:
        new_verts = []
        for v in f.verts:
            if v.index not in vert_map:
                vert_map[v.index] = bm2.verts.new(v.co + up)
            new_verts.append(vert_map[v.index])
        try:
            bm2.faces.new(new_verts)
        except ValueError:
            pass
    bm.free()

    me = bpy.data.meshes.new("Pasto_Alfombra_Mesh")
    bm2.to_mesh(me)
    bm2.free()
    # UVs básicas
    me.uv_layers.new(name="UVMap")
    obj = bpy.data.objects.new("Pasto_Alfombra", me)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = plane.matrix_world.copy()
    obj.data.materials.append(pasto_mat)
    log(f"Pasto_Alfombra: {len(me.polygons)} caras")
    return obj


def build_pasto_hebras(pasto_edges_obj: bpy.types.Object | None, pasto_mat: bpy.types.Material, max_strands=3000) -> bpy.types.Object | None:
    if pasto_edges_obj is None or pasto_edges_obj.type != "MESH":
        log("No hay Pasto_Volcan de aristas; se omite hebras")
        return None

    mesh = pasto_edges_obj.data
    edges = list(mesh.edges)
    if not edges:
        return None

    random.seed(42)
    if len(edges) > max_strands:
        edges = random.sample(edges, max_strands)

    bm = bmesh.new()
    radius = 0.025
    for e in edges:
        v0 = mesh.vertices[e.vertices[0]].co
        v1 = mesh.vertices[e.vertices[1]].co
        direction = v1 - v0
        if direction.length < 1e-5:
            continue
        direction.normalize()
        axis = direction.cross(Vector((0, 0, 1)))
        if axis.length < 1e-4:
            axis = direction.cross(Vector((0, 1, 0)))
        axis.normalize()
        binormal = direction.cross(axis).normalized()

        ring0, ring1 = [], []
        for k in range(3):
            ang = (k / 3.0) * math.tau
            offset = (axis * math.cos(ang) + binormal * math.sin(ang)) * radius
            ring0.append(bm.verts.new(v0 + offset))
            ring1.append(bm.verts.new(v1 + offset))
        for k in range(3):
            try:
                bm.faces.new([ring0[k], ring0[(k + 1) % 3], ring1[(k + 1) % 3], ring1[k]])
            except ValueError:
                pass

    me = bpy.data.meshes.new("Pasto_Hebras_Mesh")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Pasto_Hebras", me)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = pasto_edges_obj.matrix_world.copy()
    obj.data.materials.append(pasto_mat)
    log(f"Pasto_Hebras: {len(me.polygons)} caras")
    return obj


def remove_old_pasto_edges():
    obj = bpy.data.objects.get("Pasto_Volcan")
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)
        log("Eliminado Pasto_Volcan (solo aristas)")


def quarantine_trees():
    col = bpy.data.collections.get("NO_EXPORT")
    if col is None:
        col = bpy.data.collections.new("NO_EXPORT")
        bpy.context.scene.collection.children.link(col)
    for name in ("Arboles01", "Arboles02"):
        obj = bpy.data.objects.get(name)
        if not obj:
            continue
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        if obj.name not in col.objects:
            col.objects.link(obj)
        obj.hide_render = True


def clear_particle_systems(plane: bpy.types.Object) -> None:
    names = [m.name for m in plane.modifiers if m.type == "PARTICLE_SYSTEM"]
    for name in names:
        mod = plane.modifiers.get(name)
        if mod is not None:
            plane.modifiers.remove(mod)
            log(f"modifier PS quitado: {name}")
    while len(getattr(plane, "particle_systems", [])) > 0:
        plane.particle_systems.remove(plane.particle_systems[0])


def export_selected(out_dir: Path):
    fbx = out_dir / "Escenario_Volcan_TerrenoPasto.fbx"
    glb = out_dir / "Escenario_Volcan_TerrenoPasto.glb"
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
            export_image_format="JPEG",
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


def main():
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    plane = bpy.data.objects.get("Plane")
    if plane is None:
        log("ERROR: no hay Plane")
        return

    clear_particle_systems(plane)

    suelo = ensure_principled_suelo()
    pasto_mat = ensure_pasto_material()
    assign_only_suelo(plane, suelo)

    old_pasto = bpy.data.objects.get("Pasto_Volcan")
    alfombra = build_pasto_alfombra(plane, pasto_mat)
    hebras = build_pasto_hebras(old_pasto, pasto_mat, max_strands=3000)
    remove_old_pasto_edges()
    quarantine_trees()

    for o in (plane, alfombra, hebras):
        if o is None:
            continue
        set_active(o)
        try:
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        except Exception as e:
            log(f"transform_apply {o.name}: {e}")

    bpy.ops.object.select_all(action="DESELECT")
    plane.select_set(True)
    if alfombra:
        alfombra.select_set(True)
    if hebras:
        hebras.select_set(True)
    bpy.context.view_layer.objects.active = plane

    out = repo_root() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan"
    out.mkdir(parents=True, exist_ok=True)
    export_selected(out)

    try:
        bpy.ops.file.pack_all()
    except Exception:
        pass
    if bpy.data.filepath:
        bpy.ops.wm.save_mainfile()
    log("Listo. Reimporta en Unity (FBX o GLB).")


if __name__ == "__main__":
    main()
