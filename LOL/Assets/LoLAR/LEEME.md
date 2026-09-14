# LoL AR – Guía rápida

Experiencia AR con AR Foundation 6.6 + ARCore (Android) y dos cartas físicas.

| Carta | Nombre en la librería | Qué aparece |
|-------|----------------------|-------------|
| Personaje / Mapa | `MapCard` | Mapa de la Grieta con 5 íconos (Top, Jungla, Mid, Bot/ADC, Soporte) y música "Tales of the Rift" en bucle. Toca un ícono para ver su info. 1 dedo gira e inclina el mapa (360°), pellizcar con 2 dedos hace zoom. |
| Dragón | `DragonCard` | El dragón volando (`Models/Dragon/dragon_flying.glb`) con música del tema de Samira en bucle, y un ícono con info del Dragón como objetivo. Su clip "Landing" aterriza al final, así que solo se repite el tramo de vuelo de 0,27 s a 2,50 s (`FlyingLoopStartSeconds`/`FlyingLoopEndSeconds` en el builder). Mismos gestos que el mapa (rotar/zoom). |
| Ambas juntas (< 15 cm) | — | Batalla sobre la carta del dragón, con "Legends Never Die" en bucle: el Dragón Ancestral (`elder_dragon.glb`) sale de la fosa, ruge y pelea contra 5 campeones (Ashe, Braum, Dr. Mundo, Katarina, Maestro Yi). Reemplaza al dragón volando solo mientras dura. |

## 1. Primera vez

1. Abre el proyecto en Unity 6000.5.10f1 y espera a que se instalen **AR Foundation**, **ARCore** y **glTFast** (se agregaron a `Packages/manifest.json`).
2. Revisa que Unity Hub tenga el módulo **Android Build Support** (con OpenJDK y SDK/NDK).
3. Menú **LoL AR > Construir todo**. Esto:
   - cambia la plataforma a Android, IL2CPP y ARM64, y activa ARCore;
   - agrega *AR Background Renderer Feature* a los renderers de URP;
   - genera `Cards/MapCard.png`, `Cards/DragonCard.png` y `Cards/LoLCardsLibrary.asset`;
   - crea los prefabs (`Prefabs/`) y la escena `Scenes/LoLAR.unity`.
4. Abre **Project Settings > XR Plug-in Management > Android** y confirma que **Google ARCore** está marcado. Luego abre **Project Validation** y pulsa *Fix All* si aparece algo.

## 2. Cartas

- **Para imprimir:** `Cards/Imprimir/CartaPersonaje.png` (Katarina, Mid Lane) y `Cards/Imprimir/CartaDragon.png` (Dragón Ancestral).
- **Tamaño de impresión: 9 cm de ancho.** La carta del personaje queda de 9 × 13,4 cm y la del dragón de 9 × 12,8 cm. Impriman al 100 % (sin "ajustar a la página"), en papel mate o cartulina, y midan con regla. Si imprimen otro ancho, cambien `CardWidth` en `Editor/LoLARBuilder.cs` y ejecuten **LoL AR > 2**.
- **Lo que ARCore busca:** `Cards/MapCard.png` y `Cards/DragonCard.png` son **solo la ilustración** de cada carta, sin el marco ni el texto. Con la carta completa, la del personaje puntuaba 25/100 en `arcoreimg` (ARCore pide 75 o más). Con la ilustración sola, el personaje saca 95 y el dragón 100. El tamaño físico de la ilustración impresa es 7,27 × 8,91 cm (personaje) y 6,22 × 7,29 cm (dragón). El contenido 3D se centra en la ilustración, un poco más arriba del centro de la carta.
- Para cambiar un diseño: reemplacen la carta en `Imprimir/`, recorten su ilustración en `MapCard.png` o `DragonCard.png`, actualicen los recortes en `LoLARBuilder.cs` y ejecuten **LoL AR > 2. Generar cartas y Reference Image Library**.
- Consejos para ARCore: mucho detalle y contraste, sin patrones repetitivos y sin grandes zonas lisas. Imprimir en mate evita reflejos.

## Mapa (Blender)

`Assets/Models/Map/summoners_rift.glb` se exporta desde `Grieta del Invocador.blend`, que queda fuera del repo porque pesa 186 MB, con el script `LOL/Tools/export_map.py`. El script:
- quita los acantilados que quedan fuera del cuadrado jugable;
- reduce las 99 texturas de 2048 px a 512 px para que el celular aguante;
- agrega empties `LoLAR_*` al nivel del suelo: `SquareMin` y `SquareMax` (las fuentes) fijan la escala, `Center` el anclaje, y `Top`, `Jungla`, `Mid`, `Bot` y `Soporte` la posición de cada pop-up.

Para regenerarlo (desde la carpeta `LOL`):

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b "../../Grieta del Invocador.blend" --python Tools/export_map.py -- Assets/Models/Map/summoners_rift.glb
```

Luego ejecuten **LoL AR > 3. Generar prefabs y escena**. Para mover un pop-up, cambien sus coordenadas en `POINTS` dentro del script.

## 3. Probar en el celular

1. Activa las *Opciones de desarrollador* y la *Depuración USB* en el Android (que sea compatible con ARCore / Google Play Services for AR).
2. **File > Build Profiles > Android > Build And Run**.

## 4. Ajustes rápidos

| Qué | Dónde |
|-----|-------|
| Distancia para la batalla | `XR Origin (AR) > Card Tracking Controller > Battle Enter/Exit Distance` |
| Modelo al revés | `DragonYaw` / `ChampionYaw` en `LoLARBuilder.cs`, y luego **LoL AR > 3** |
| Tamaño del mapa, dragón o campeones | `MapSize` (lado del cuadrado jugable), `DragonHeight` y la altura de cada campeón en `LoLARBuilder.cs` |
| Textos de los roles | arreglo `Roles` en `LoLARBuilder.cs` |
| Ritmo del combate | componente `BattleDirector` del prefab `BattleContent` |
| Sensibilidad de rotación/zoom | `ModelTouchController` (`degreesPerPixel`, `maxTilt`, `zoomRange`) en el `Pivot` del mapa o del dragón |
| Música de cada carta | `MapMusicPath` / `DragonMusicPath` / `BattleMusicPath` en `LoLARBuilder.cs`; los MP3 están en `Assets/LoLAR/Audio/` |

> **LoL AR > 3** sobrescribe los prefabs y la escena. Si los editan a mano, hagan esos cambios después o muévanlos a otra carpeta.

## Si la batalla no arranca al juntar las cartas

Hay un panel de texto en la esquina superior izquierda (componente `DebugHud`, activo por defecto) que muestra si cada carta está visible, la distancia entre ellas y si la batalla está activa. Con eso:

- Si la distancia no baja al acercar las cartas físicamente, lo más probable es que el ancho impreso real no sea 9 cm exactos: ARCore usa ese dato para calcular la escala del mundo, y si está mal, todas las distancias salen infladas o achicadas. Midan con regla y ajusten `CardWidth` si hace falta.
- Si la distancia sí baja pero nunca cruza el umbral, ajusten `Battle Enter/Exit Distance` en el inspector (no hace falta regenerar).
- Cuando ya no lo necesiten, pongan `ShowDebugHud` en `false` en `LoLARBuilder.cs` y ejecuten **LoL AR > 3** para quitar el panel.

## Scripts

- `CardTrackingController`: rastrea las cartas, calcula la distancia y muestra u oculta el mapa, el dragón o la batalla.
- `LanePopup`, `TapInteractor`, `ModelTouchController`, `Billboard`: íconos, toque (tap/arrastre/pellizco), rotación+zoom y paneles.
- `BattleDirector`, `VfxFactory`, `ModelAnimationLooper`: cinemática en 4 fases, partículas y animaciones de los GLB.
- `AutoLoopMusic`: reproduce en bucle la música del contenido mientras está activo.
- `DebugHud`: panel de diagnóstico en pantalla (ver arriba).
