import bpy
from pathlib import Path

root = Path(bpy.data.filepath).resolve()
for p in [root.parent, *root.parents]:
    if (p / "ProjectSettings").is_dir() and (p / "Assets" / "Dino").is_dir():
        repo = p
        break
else:
    raise SystemExit("no repo")

out = repo / "Assets" / "Dino" / "Models" / "Environment" / "Volcan" / "Textures"
out.mkdir(parents=True, exist_ok=True)

print("IMAGES", len(bpy.data.images))
for img in list(bpy.data.images):
    if img.size[0] <= 0 or img.size[1] <= 0:
        print("SKIP empty", img.name)
        continue
    safe = "".join(c if c.isalnum() or c in "-_." else "_" for c in img.name)
    if not safe.lower().endswith((".png", ".jpg", ".jpeg")):
        safe += ".png"
    path = out / safe
    # Guardar píxeles a disco (funciona aunque el filepath original falte, si hay datos en memoria/packed)
    try:
        img.filepath_raw = str(path)
        img.file_format = "PNG"
        img.save()
        print("SAVED", path, "from", img.name, "size", img.size[:])
    except Exception as e:
        print("FAIL", img.name, e)

print("DONE", out)
