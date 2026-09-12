"""
Exporta "Grieta del Invocador.blend" a un GLB liviano para AR en celular.

Uso (desde la carpeta LOL):
  blender -b "../../Grieta del Invocador.blend" --python Tools/export_map.py -- Assets/Models/Map/summoners_rift.glb

- Quita los objetos cuyo centro queda fuera del cuadrado jugable (acantilados decorativos).
- Reduce todas las texturas a MAX_TEXTURE px.
- Agrega empties "LoLAR_*" al nivel del suelo, que LoLARBuilder usa para escalar el mapa y ubicar los pop-ups.
"""
import statistics
import sys

import bpy
import mathutils

MAX_TEXTURE = 512
MARGIN = 1.5

# Coordenadas de Blender (X, Y). Base azul abajo-izquierda, base roja arriba-derecha.
SQUARE_MIN = (-10.71, -12.42)  # Fuente azul
SQUARE_MAX = (12.49, 11.41)    # Fuente roja
POINTS = {
    "SquareMin": SQUARE_MIN,
    "SquareMax": SQUARE_MAX,
    "Center": (0.89, -0.53),
    "Top": (-10.23, 11.01),
    "Jungla": (-5.91, -2.01),
    "Mid": (0.89, -0.53),
    "Bot": (2.89, -12.34),
    "Soporte": (8.49, -12.34),
}


def ray_z(scene, depsgraph, x, y):
    hit, location, *_ = scene.ray_cast(depsgraph, mathutils.Vector((x, y, 200.0)), mathutils.Vector((0.0, 0.0, -1.0)))
    return location.z if hit else None


def main():
    output = sys.argv[sys.argv.index("--") + 1]
    scene = bpy.context.scene
    depsgraph = bpy.context.evaluated_depsgraph_get()

    samples = []
    for i in range(15):
        for j in range(15):
            x = SQUARE_MIN[0] + (SQUARE_MAX[0] - SQUARE_MIN[0]) * (i + 0.5) / 15
            y = SQUARE_MIN[1] + (SQUARE_MAX[1] - SQUARE_MIN[1]) * (j + 0.5) / 15
            z = ray_z(scene, depsgraph, x, y)
            if z is not None:
                samples.append(z)
    ground = statistics.median(samples)

    heights = {}
    for name, (x, y) in POINTS.items():
        z = ray_z(scene, depsgraph, x, y)
        # Si el rayo cae en un hueco o en la copa de un árbol, usa el suelo mediano.
        heights[name] = ground if z is None or abs(z - ground) > 1.0 else z

    removed = 0
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            continue
        corners = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
        cx = sum(c.x for c in corners) / 8
        cy = sum(c.y for c in corners) / 8
        inside = (SQUARE_MIN[0] - MARGIN <= cx <= SQUARE_MAX[0] + MARGIN and
                  SQUARE_MIN[1] - MARGIN <= cy <= SQUARE_MAX[1] + MARGIN)
        if not inside:
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1

    for name, (x, y) in POINTS.items():
        empty = bpy.data.objects.new("LoLAR_" + name, None)
        empty.location = (x, y, heights[name])
        scene.collection.objects.link(empty)

    resized = 0
    for image in bpy.data.images:
        if image.type == 'IMAGE' and max(image.size) > MAX_TEXTURE:
            w, h = image.size
            s = MAX_TEXTURE / max(w, h)
            image.scale(max(1, int(w * s)), max(1, int(h * s)))
            resized += 1

    bpy.ops.export_scene.gltf(
        filepath=output,
        export_format='GLB',
        export_image_format='AUTO',
        export_apply=True,
        export_yup=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_draco_mesh_compression_enable=False,
    )
    print(f"[export_map] suelo={ground:.2f} eliminados={removed} texturas={resized} -> {output}")


main()
