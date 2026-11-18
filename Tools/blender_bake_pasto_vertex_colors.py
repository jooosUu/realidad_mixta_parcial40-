"""
Bake del vertex group 'Pasto' → Vertex Colors (COLOR) en el Plane
y reexporta el FBX del terreno para que Unity sepa dónde hay más/menos pasto.
"""
from __future__ import annotations

from pathlib import Path
import bpy


def log(m): print(f"[PastoVC] {m}")


def repo() -> Path:
    return Path(r"C:\Users\david\OneDrive\Documentos\Octavo semestre\realidad_mixta_parcial40-")


def main():
    # Preferir backup original (tiene el VG Pasto intacto)
    plane = bpy.data.objects.get("Plane")
    if plane is None:
        log("ERROR: no Plane")
        return

    vg = plane.vertex_groups.get("Pasto")
    if vg is None:
        for g in plane.vertex_groups:
            if "pasto" in g.name.lower():
                vg = g
                break
    if vg is None:
        log("ERROR: no vertex group Pasto")
        return

    mesh = plane.data
    # Color attribute (Blender 3.2+ / 4 / 5)
    attr_name = "Col"
    if mesh.color_attributes.get(attr_name):
        mesh.color_attributes.remove(mesh.color_attributes[attr_name])
    attr = mesh.color_attributes.new(name=attr_name, type="BYTE_COLOR", domain="POINT")

    # Pesos
    weights = [0.0] * len(mesh.vertices)
    for i in range(len(mesh.vertices)):
        try:
            weights[i] = vg.weight(i)
        except RuntimeError:
            weights[i] = 0.0

    # Escribir colores: R=G=B = peso Pasto, A=1
    # En domain POINT, cada vértice un color
    for i, w in enumerate(weights):
        w = max(0.0, min(1.0, w))
        attr.data[i].color = (w, w, w, 1.0)

    log(f"Vertex colors '{attr_name}' desde VG '{vg.name}' (verts={len(weights)}, max={max(weights):.2f})")

    # Quitar partículas
    names = [m.name for m in plane.modifiers if m.type == "PARTICLE_SYSTEM"]
    for n in names:
        m = plane.modifiers.get(n)
        if m:
            plane.modifiers.remove(m)

    # Material suelo simple si existe Suelo_Unity / texturas
    # (no tocar materiales en detalle; Unity ya tiene M_Volcan_Suelo)

    bpy.ops.object.select_all(action="DESELECT")
    plane.hide_set(False)
    plane.select_set(True)
    bpy.context.view_layer.objects.active = plane

    out = repo() / "Assets" / "Dino" / "Models" / "Environment" / "Volcan"
    out.mkdir(parents=True, exist_ok=True)
    fbx = out / "Escenario_Volcan_TerrenoPasto.fbx"

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
        colors_type="SRGB",
    )
    log(f"FBX → {fbx} ({fbx.stat().st_size} bytes)")
    log("Listo. En Unity: DINO WEST → Poblar Pasto Volcán (usa densidad del vertex color).")


if __name__ == "__main__":
    main()
