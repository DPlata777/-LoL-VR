# LoL AR – Guía rápida

Experiencia AR con AR Foundation 6.6 + ARCore (Android) y dos cartas físicas.

| Carta | Nombre en la librería | Qué aparece |
|-------|----------------------|-------------|
| Personaje / Mapa | `MapCard` | Mapa de la Grieta con 5 íconos (Top, Jungla, Mid, Bot/ADC, Soporte). Toca un ícono para ver su info. Desliza el dedo para girar el mapa. |
| Dragón | `DragonCard` | El Dragón Ancestral animado. |
| Ambas juntas (< 15 cm) | — | Batalla sobre la carta del dragón: fosa, rugido y 5 campeones (Ashe, Braum, Dr. Mundo, Katarina, Maestro Yi) atacando. |

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

- Las imágenes generadas son **provisionales**: patrones de alto contraste que ARCore rastrea bien.
- Para usar el diseño real, reemplaza `MapCard.png` o `DragonCard.png` **con el mismo nombre** y ejecuta **LoL AR > 2. Generar cartas y Reference Image Library**.
- Tamaño físico configurado: **6,3 cm de ancho** (carta estándar). Si imprimen otro tamaño, cambia `CardWidth` en `Editor/LoLARBuilder.cs`.
- Consejos para ARCore: mucho detalle y contraste, sin patrones repetitivos y sin grandes zonas lisas. Imprimir en mate evita reflejos.

## 3. Probar en el celular

1. Activa las *Opciones de desarrollador* y la *Depuración USB* en el Android (que sea compatible con ARCore / Google Play Services for AR).
2. **File > Build Profiles > Android > Build And Run**.

## 4. Ajustes rápidos

| Qué | Dónde |
|-----|-------|
| Distancia para la batalla | `XR Origin (AR) > Card Tracking Controller > Battle Enter/Exit Distance` |
| Modelo al revés | `DragonYaw` / `ChampionYaw` en `LoLARBuilder.cs`, y luego **LoL AR > 3** |
| Tamaño del mapa, dragón o campeones | `MapSize`, `DragonHeight` y la altura de cada campeón en `LoLARBuilder.cs` |
| Textos de los roles | arreglo `Roles` en `LoLARBuilder.cs` |
| Ritmo del combate | componente `BattleDirector` del prefab `BattleContent` |

> **LoL AR > 3** sobrescribe los prefabs y la escena. Si los editan a mano, hagan esos cambios después o muévanlos a otra carpeta.

## Scripts

- `CardTrackingController`: rastrea las cartas, calcula la distancia y muestra u oculta el mapa, el dragón o la batalla.
- `LanePopup`, `TapInteractor`, `MapRotator`, `Billboard`: íconos, toque, rotación y paneles.
- `BattleDirector`, `VfxFactory`, `ModelAnimationLooper`: cinemática en 4 fases, partículas y animaciones de los GLB.
