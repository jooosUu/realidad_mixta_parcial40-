"""
DINO WEST — Preparar Escenario_Volcan_Mixta para Unity (terreno + pasto, sin árboles).

Qué hace (en la escena ABIERTA en Blender):
  1. Guarda una copia de seguridad .blend junto al archivo actual.
  2. Quita los Particle Systems de árboles (Arboles 01 / Arboles 02, etc.).
  3. Convierte el Particle System de Pasto en malla real y la une en un solo objeto.
  4. Mueve Arboles01/Arboles02 a la colección NO_EXPORT (no se exportan; se recolocan en Unity).
  5. Aplica transforms del terreno.
  6. Exporta FBX + GLB a Assets/Dino/Models/Environment/Volcan/

Cómo usarlo en Blender 4.x / 5.x:
  1. Abre SourceArt/Blender/Escenario_Volcan_Mixta.blend
     (o tu copia en Downloads).
  2. Scripting → Open → Tools/blender_prepare_volcan_export.py
  3. Clic en Run Script (▶)
  4. Mira la consola de Blender (Window → Toggle System Console en Windows)
     para el resumen y las rutas de export.

Si el pasto no es Particle System sino Geometry Nodes, el script lo avisa
y deja el terreno listo igual; el pasto se puede convertir a mano.
"""

from __future__ import annotations

import os
import re
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector


# ---------------------------------------------------------------------------
# Rutas (relativas al .blend o al repo si se detecta)
# ---------------------------------------------------------------------------

TREE_PS_KEYWORDS = ("arbol", "árbol", "tree", "pine", "conifer")
GRASS_PS_KEYWORDS = ("pasto", "grass", "hierba")
TREE_OBJECT_NAMES = ("arboles01", "arboles02", "arboles_01", "arboles_02")
TERRAIN_NAME_HINTS = ("plane", "terreno", "terrain", "suelo", "ground")


def repo_root_from_blend() -> Path | None:
    """Sube carpetas desde el .blend hasta encontrar Assets/ + ProjectSettings/."""
    p = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if p is None:
        return None
    for parent in [p.parent, *p.parents]:
        if (parent / "Assets").is_dir() and (parent / "ProjectSettings").is_dir():
            return parent
    # Fallback: SourceArt/Blender/<file> → repo
    if p.parent.name.lower() == "blender" and p.parent.parent.name.lower() == "sourceart":
        return p.parent.parent.parent
    return None


def export_dir() -> Path:
    root = repo_root_from_blend()
    if root is not None:
        out = root / "Assets" / "Dino" / "Models" / "Environment" / "Volcan"
    else:
        # Si el .blend está en Downloads, exportar al lado
        base = Path(bpy.data.filepath).resolve().parent if bpy.data.filepath else Path.home() / "Downloads"
        out = base / "Volcan_UnityExport"
    out.mkdir(parents=True, exist_ok=True)
    return out


def log(msg: str) -> None:
    print(f"[Volcan→Unity] {msg}")


# ---------------------------------------------------------------------------
# Utilidades de escena
# ---------------------------------------------------------------------------

def ensure_collection(name: str) -> bpy.types.Collection:
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def unlink_from_all_collections(obj: bpy.types.Object) -> None:
    for col in list(obj.users_collection):
        col.objects.unlink(obj)


def move_to_collection(obj: bpy.types.Object, col: bpy.types.Collection) -> None:
    unlink_from_all_collections(obj)
    if obj.name not in col.objects:
        col.objects.link(obj)


def name_matches(name: str, keywords: tuple[str, ...]) -> bool:
    n = name.casefold()
    n = n.replace("á", "a").replace("é", "e").replace("í", "i").replace("ó", "o").replace("ú", "u")
    return any(k in n for k in keywords)


def find_terrain() -> bpy.types.Object | None:
    # Preferir "Plane" exacto (como en tu outliner)
    plane = bpy.data.objects.get("Plane")
    if plane is not None and plane.type == "MESH":
        return plane
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if name_matches(obj.name, TERRAIN_NAME_HINTS):
            return obj
    # Fallback: mesh más grande por bounding box
    best = None
    best_vol = -1.0
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.data is None:
            continue
        dims = obj.dimensions
        vol = float(dims.x * dims.y * dims.z)
        if vol > best_vol:
            best_vol = vol
            best = obj
    return best


def particle_systems_of(obj: bpy.types.Object) -> list:
    return list(getattr(obj, "particle_systems", []))


def remove_particle_system(obj: bpy.types.Object, ps_name: str) -> None:
    """Quita un particle system y su modifier asociado."""
    # Modifier primero
    for mod in list(obj.modifiers):
        if mod.type == "PARTICLE_SYSTEM" and getattr(mod, "particle_system", None):
            if mod.particle_system.name == ps_name:
                obj.modifiers.remove(mod)
                return
    # Por si el nombre del modifier no coincide
    ps = obj.particle_systems.get(ps_name)
    if ps is not None:
        obj.particle_systems.remove(ps)


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.hide_select = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_transforms(obj: bpy.types.Object) -> None:
    set_active(obj)
    try:
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    except Exception as e:
        log(f"No se pudo aplicar transforms en '{obj.name}': {e}")


# ---------------------------------------------------------------------------
# Pasos principales
# ---------------------------------------------------------------------------

def save_backup() -> Path | None:
    if not bpy.data.filepath:
        log("El archivo no está guardado en disco. Guarda el .blend y vuelve a correr el script.")
        return None
    src = Path(bpy.data.filepath)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = src.with_name(f"{src.stem}_BACKUP_{stamp}{src.suffix}")
    bpy.ops.wm.save_as_mainfile(filepath=str(backup), copy=True)
    log(f"Backup: {backup}")
    return backup


def strip_tree_particles(terrain: bpy.types.Object) -> list[str]:
    removed = []
    for ps in list(particle_systems_of(terrain)):
        if name_matches(ps.name, TREE_PS_KEYWORDS) and not name_matches(ps.name, GRASS_PS_KEYWORDS):
            removed.append(ps.name)
            remove_particle_system(terrain, ps.name)
            log(f"Partículas de árbol eliminadas: '{ps.name}'")
    return removed


def convert_grass_particles(terrain: bpy.types.Object) -> bpy.types.Object | None:
    """Make Instances Real del sistema Pasto y une en 'Pasto_Volcan'."""
    grass_systems = [
        ps for ps in particle_systems_of(terrain)
        if name_matches(ps.name, GRASS_PS_KEYWORDS)
    ]
    if not grass_systems:
        # ¿Geometry Nodes con "pasto" en el nombre?
        for mod in terrain.modifiers:
            if mod.type == "NODES" and name_matches(mod.name, GRASS_PS_KEYWORDS):
                log(
                    f"Se encontró Geometry Nodes '{mod.name}' (no Particle System). "
                    "Convierte a malla a mano: aplicar modifier o Realize Instances, luego une."
                )
                return None
        log("No hay Particle System de pasto en el terreno. Se exportará solo el mesh del suelo.")
        return None

    # Asegurar que el pasto renderice como Object (instancias), no Path/Hair sin mesh
    for ps in grass_systems:
        settings = ps.settings
        # En Blender 3+/4+/5+ : type = 'HAIR' o 'EMITTER'
        try:
            if settings.render_type not in {"OBJECT", "COLLECTION"}:
                log(
                    f"Pasto '{ps.name}' tiene render_type={settings.render_type}. "
                    "Para Convertir a malla conviene OBJECT o COLLECTION. "
                    "Se intentará Make Instances Real de todas formas."
                )
        except Exception:
            pass

    before = {o.name for o in bpy.data.objects}

    set_active(terrain)
    # Duplicar instancias reales de TODOS los PS visibles del objeto activo
    # (los de árbol ya se quitaron).
    try:
        bpy.ops.object.duplicates_make_real(use_base_parent=False, use_hierarchy=False)
    except Exception as e:
        log(f"Make Instances Real falló: {e}")
        return None

    new_objs = [
        o for o in bpy.data.objects
        if o.name not in before and o.type == "MESH"
    ]
    if not new_objs:
        log(
            "Make Instances Real no creó meshes. "
            "En Blender: selecciona Plane → Particle Properties → "
            "Modificadores → Convertir (o haz el pasto con Geometry Nodes + Realize)."
        )
        return None

    log(f"Instancias de pasto creadas: {len(new_objs)}")

    # Unir en un solo mesh
    bpy.ops.object.select_all(action="DESELECT")
    for o in new_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = new_objs[0]
    if len(new_objs) > 1:
        bpy.ops.object.join()
    grass = bpy.context.view_layer.objects.active
    grass.name = "Pasto_Volcan"
    apply_transforms(grass)

    # Quitar PS de pasto del terreno (ya está bakeado a mesh)
    for ps in list(particle_systems_of(terrain)):
        if name_matches(ps.name, GRASS_PS_KEYWORDS):
            remove_particle_system(terrain, ps.name)
            log(f"Particle System de pasto eliminado del terreno: '{ps.name}'")

    return grass


def quarantine_tree_prototypes() -> list[str]:
    """Arboles01/02 no se exportan; en Unity se usan packs con TreeReaction."""
    no_export = ensure_collection("NO_EXPORT")
    moved = []
    for obj in list(bpy.data.objects):
        key = obj.name.casefold().replace(" ", "").replace("_", "")
        key = key.replace("á", "a")
        if key in {"arboles01", "arboles02"} or name_matches(obj.name, ("arboles01", "arboles02")):
            move_to_collection(obj, no_export)
            obj.hide_render = True
            moved.append(obj.name)
            log(f"Prototipo de árbol → NO_EXPORT: '{obj.name}'")
    return moved


def select_for_export(terrain: bpy.types.Object, grass: bpy.types.Object | None) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    terrain.hide_set(False)
    terrain.select_set(True)
    if grass is not None:
        grass.hide_set(False)
        grass.select_set(True)
    # Luces opcionales (Sun)
    sun = bpy.data.objects.get("Sun")
    if sun is not None:
        sun.select_set(True)
    bpy.context.view_layer.objects.active = terrain


def export_fbx(path: Path) -> None:
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        use_active_collection=False,
        object_types={"MESH", "EMPTY", "OTHER"},
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        use_space_transform=True,
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


def export_glb(path: Path) -> None:
    # glTF empaqueta PBR mejor que FBX para Unity + glTFast
    kwargs = dict(
        filepath=str(path),
        use_selection=True,
        export_format="GLB",
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_yup=True,
    )
    # Compatibilidad entre versiones de add-on glTF
    try:
        bpy.ops.export_scene.gltf(**kwargs)
    except TypeError:
        # Quitar kwargs no soportados en Blender 5
        for key in ("export_image_format", "export_apply"):
            kwargs.pop(key, None)
        bpy.ops.export_scene.gltf(**kwargs)


def main() -> None:
    log("=== Preparación Escenario Volcán → Unity ===")
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    save_backup()

    terrain = find_terrain()
    if terrain is None:
        log("ERROR: no se encontró el terreno (Plane).")
        return
    log(f"Terreno: '{terrain.name}' ({len(terrain.data.vertices)} verts)")

    removed_trees = strip_tree_particles(terrain)
    grass = convert_grass_particles(terrain)
    moved = quarantine_tree_prototypes()
    apply_transforms(terrain)

    select_for_export(terrain, grass)
    out = export_dir()
    fbx_path = out / "Escenario_Volcan_TerrenoPasto.fbx"
    glb_path = out / "Escenario_Volcan_TerrenoPasto.glb"

    try:
        export_fbx(fbx_path)
        log(f"FBX OK → {fbx_path}")
    except Exception as e:
        log(f"FBX falló: {e}")

    try:
        export_glb(glb_path)
        log(f"GLB OK → {glb_path}")
    except Exception as e:
        log(f"GLB falló: {e}")

    # Guardar el .blend ya limpio (sin árboles particle)
    if bpy.data.filepath:
        bpy.ops.wm.save_mainfile()
        log(f"Blend limpio guardado: {bpy.data.filepath}")

    log("--- Resumen ---")
    log(f"PS árboles quitados: {removed_trees or '(ninguno)'}")
    log(f"Pasto mesh: {grass.name if grass else '(no convertido)'}")
    log(f"Prototipos en NO_EXPORT: {moved or '(ninguno)'}")
    log(f"Carpeta export: {out}")
    log(
        "Siguiente en Unity: importa el FBX o GLB en "
        "Assets/Dino/Models/Environment/Volcan/ y colócalo en Simulation. "
        "Los árboles derribables se recolocan con los prefabs del proyecto."
    )


if __name__ == "__main__":
    main()
