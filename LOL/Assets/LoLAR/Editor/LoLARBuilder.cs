using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LoLAR.EditorTools
{
    /// <summary>
    /// Menú "LoL AR": configura el proyecto, genera la librería de cartas, los prefabs y la escena AR.
    /// Todo se puede volver a ejecutar; sobrescribe lo generado pero respeta las imágenes de carta existentes.
    /// </summary>
    public static class LoLARBuilder
    {
        const string Root = "Assets/LoLAR";
        const string CardsDir = Root + "/Cards";
        const string PrefabsDir = Root + "/Prefabs";
        const string MaterialsDir = Root + "/Materials";
        const string TexturesDir = Root + "/Textures";
        const string AudioDir = Root + "/Audio";
        const string AnimationsDir = Root + "/Animations";
        const string ScenesDir = Root + "/Scenes";
        const string LibraryPath = CardsDir + "/LoLCardsLibrary.asset";
        const string ScenePath = ScenesDir + "/LoLAR.unity";

        const string MapCardName = "MapCard";
        const string DragonCardName = "DragonCard";

        // Medidas físicas en metros.
        const float CardWidth = 0.09f; // Ancho impreso de las cartas (9 cm); el alto sale de la proporción de la imagen.
        const float MapSize = 0.15f; // Lado del cuadrado jugable de la Grieta.
        const float IconHeight = 0.035f;
        const float DragonHeight = 0.06f;
        const float BattleRadius = 0.11f;

        // Si un modelo aparece de espaldas, cambia estos giros (grados) y regenera.
        const float DragonYaw = 0f;
        const float ChampionYaw = 0f;

        const string DragonModelPath = "Assets/Models/Dragon/elder_dragon.glb";
        const string FlyingDragonModelPath = "Assets/Models/Dragon/dragon_flying.glb";
        // Tramo de vuelo de "Landing" que se repite (segundos); en ambos extremos coinciden altura y aleteo.
        const float FlyingLoopStartSeconds = 0.27f;
        const float FlyingLoopEndSeconds = 2.50f;
        const string MapModelPath = "Assets/Models/Map/summoners_rift.glb";
        // Imagen de relleno debajo del modelo: tapa los huecos sin textura del GLB descargado.
        const string MapFillTexturePath = TexturesDir + "/GrietaDelInvocadorRelleno.jpg";
        const string MarkerPrefix = "LoLAR_";

        // Música de fondo: suena en bucle mientras su contenido está a la vista (ver AutoLoopMusic).
        const string MapMusicPath = AudioDir + "/TalesOfTheRift.mp3";
        const string DragonMusicPath = AudioDir + "/Samira.mp3";
        const string BattleMusicPath = AudioDir + "/LegendsNeverDie.mp3";

        // Panel de texto en pantalla con el estado del tracking (mapa/dragón visibles, distancia entre
        // cartas, batalla activa). Ponlo en false y regenera cuando ya no lo necesites para depurar.
        const bool ShowDebugHud = true;

        static readonly ChampionDef[] Champions =
        {
            new ChampionDef("Ashe", "Assets/Models/Champions/ashe.glb", 0.040f, new Color(0.6f, 0.9f, 1f)),
            new ChampionDef("Braum", "Assets/Models/Champions/braum.glb", 0.044f, new Color(0.5f, 0.75f, 1f)),
            new ChampionDef("DrMundo", "Assets/Models/Champions/dr_mundo.glb", 0.048f, new Color(0.75f, 0.4f, 1f)),
            new ChampionDef("Katarina", "Assets/Models/Champions/katarina.glb", 0.040f, new Color(1f, 0.3f, 0.3f)),
            new ChampionDef("MaestroYi", "Assets/Models/Champions/maestro_yi.glb", 0.040f, new Color(0.7f, 1f, 0.3f)),
        };

        // Posiciones en coordenadas del mapa (-0.5..0.5). Base azul abajo-izquierda, base roja arriba-derecha.
        static readonly RoleDef[] Roles =
        {
            new RoleDef("Top", "Top",
                "Línea solitaria de resistencia. Tanques y luchadores con gran aguante y presión lateral (split-push).",
                "Garen, Darius, Fiora, Mordekaiser, Aatrox",
                IconShape.Shield, new Vector2(-0.4f, 0.4f), new Color(0.35f, 0.6f, 1f)),
            new RoleDef("Jungla", "Jungla",
                "Rol itinerante. Realiza emboscadas, asegura visión y controla objetivos mayores (Dragón/Barón).",
                "Lee Sin, Vi, Warwick, Master Yi, Jarvan IV",
                IconShape.Claw, new Vector2(-0.25f, -0.05f), new Color(0.3f, 0.85f, 0.4f)),
            new RoleDef("Mid", "Mid",
                "Línea corta y neurálgica. Magos de daño en área y asesinos de alta movilidad.",
                "Ahri, Zed, Yasuo, Lux, Syndra, Viktor",
                IconShape.Orb, Vector2.zero, new Color(0.7f, 0.4f, 1f)),
            new RoleDef("Bot", "Bot / ADC",
                "Ataque a distancia con daño sostenido (DPS). Requiere protección inicial.",
                "Jinx, Jhin, Caitlyn, Ashe, Kai'Sa, Ezreal",
                IconShape.Bow, new Vector2(0.25f, -0.4f), new Color(1f, 0.55f, 0.2f)),
            new RoleDef("Soporte", "Soporte",
                "Protege al tirador y al equipo: escudos, curaciones, control de masas y visión.",
                "Thresh, Leona, Lulu, Blitzcrank, Nami, Nautilus",
                IconShape.Amulet, new Vector2(0.4f, -0.25f), new Color(1f, 0.85f, 0.3f)),
        };

        // ------------------------------------------------------------------ Menú

        [MenuItem("LoL AR/Construir todo", priority = 0)]
        public static void BuildAll()
        {
            ConfigureProject();
            CreateCardLibrary();
            BuildPrefabsAndScene();
        }

        [MenuItem("LoL AR/1. Configurar proyecto (Android + ARCore)", priority = 20)]
        public static void ConfigureProject()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                else
                    Debug.LogError("[LoL AR] Falta 'Android Build Support'. Instálalo en Unity Hub > Installs > 6000.5.10f1 > Add modules.");
            }

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lolar.experience");
            PlayerSettings.productName = "LoL AR";

            AssignARCoreLoader();
            AddARBackgroundRendererFeature();
            AssetDatabase.SaveAssets();
            Debug.Log("[LoL AR] Proyecto configurado: Android, IL2CPP, ARM64, ARCore.");
        }

        [MenuItem("LoL AR/2. Generar cartas y Reference Image Library", priority = 21)]
        public static void CreateCardLibrary()
        {
            EnsureFolder(CardsDir);
            var mapTexture = EnsureCardTexture(CardsDir + "/" + MapCardName + ".png", 11,
                new Color32(30, 90, 200, 255), new Color32(220, 180, 60, 255));
            var dragonTexture = EnsureCardTexture(CardsDir + "/" + DragonCardName + ".png", 23,
                new Color32(190, 40, 30, 255), new Color32(255, 140, 20, 255));

            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            while (library.count > 0)
                library.RemoveAt(library.count - 1);

            AddReferenceImage(library, mapTexture, MapCardName);
            AddReferenceImage(library, dragonTexture, DragonCardName);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[LoL AR] Reference Image Library lista: " + LibraryPath);
        }

        [MenuItem("LoL AR/3. Generar prefabs y escena", priority = 22)]
        public static void BuildPrefabsAndScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (!library)
            {
                CreateCardLibrary();
                library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            }

            foreach (var dir in new[] { PrefabsDir, MaterialsDir, TexturesDir, AudioDir, AnimationsDir, ScenesDir })
                EnsureFolder(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = new Palette();

            var map = SavePrefab(BuildMapContent(palette), PrefabsDir + "/MapContent.prefab");
            var dragon = SavePrefab(BuildDragonContent(palette), PrefabsDir + "/DragonContent.prefab");
            var battle = SavePrefab(BuildBattleContent(palette), PrefabsDir + "/BattleContent.prefab");

            // NewScene descarga los assets en memoria: la librería cargada antes queda destruida y se guardaría como null.
            BuildScene(AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath), map, dragon, battle);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[LoL AR] Escena generada: " + ScenePath);
        }

        // ------------------------------------------------------------------ Configuración

        static void AssignARCoreLoader()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!settings || !settings.Manager)
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                Debug.LogWarning("[LoL AR] Abrí XR Plug-in Management. En la pestaña Android marca 'Google ARCore' " +
                                 "(o vuelve a ejecutar 'LoL AR/1. Configurar proyecto').");
                return;
            }

            settings.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
                Debug.LogWarning("[LoL AR] No se pudo activar ARCore automáticamente. Márcalo en Project Settings > XR Plug-in Management > Android.");
            EditorUtility.SetDirty(settings);
        }

        static void AddARBackgroundRendererFeature()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!data || data.rendererFeatures.Any(feature => feature is ARBackgroundRendererFeature))
                    continue;

                var arFeature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                arFeature.name = nameof(ARBackgroundRendererFeature);
                AssetDatabase.AddObjectToAsset(arFeature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(arFeature, out _, out long localId);

                // Mismo procedimiento que el botón "Add Renderer Feature" del inspector de URP.
                var serialized = new SerializedObject(data);
                var features = serialized.FindProperty("m_RendererFeatures");
                var featureMap = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = arFeature;
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        // ------------------------------------------------------------------ Cartas

        static void AddReferenceImage(XRReferenceImageLibrary library, Texture2D texture, string imageName)
        {
            library.Add();
            int index = library.count - 1;
            library.SetName(index, imageName);
            library.SetTexture(index, texture, false);
            library.SetSpecifySize(index, true);
            library.SetSize(index, new Vector2(CardWidth, CardWidth * texture.height / texture.width));
        }

        static Texture2D EnsureCardTexture(string path, int seed, Color32 frame, Color32 accent)
        {
            if (!File.Exists(path))
            {
                var generated = GenerateCardPattern(seed, frame, accent);
                File.WriteAllBytes(path, generated.EncodeToPNG());
                Object.DestroyImmediate(generated);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.npotScale != TextureImporterNPOTScale.None || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Patrón de relleno con muchas esquinas y alto contraste (ARCore necesita puntos característicos).
        /// Reemplaza el PNG por el diseño real de la carta cuando lo tengan.
        /// </summary>
        static Texture2D GenerateCardPattern(int seed, Color32 frame, Color32 accent)
        {
            const int width = 630, height = 880;
            var rng = new System.Random(seed);
            var pixels = new Color32[width * height];
            var background = new Color32(18, 20, 28, 255);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = background;

            Color32 RandomColor()
            {
                switch (rng.Next(4))
                {
                    case 0: return frame;
                    case 1: return accent;
                    case 2: return new Color32(235, 235, 235, 255);
                    default: return new Color32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), 255);
                }
            }

            for (int n = 0; n < 160; n++)
                FillRect(pixels, width, height, rng.Next(width), rng.Next(height), rng.Next(8, 110), rng.Next(8, 110), RandomColor());
            for (int n = 0; n < 110; n++)
                FillCircle(pixels, width, height, rng.Next(width), rng.Next(height), rng.Next(5, 45), RandomColor());
            for (int n = 0; n < 70; n++)
                DrawLine(pixels, width, height, rng.Next(width), rng.Next(height), rng.Next(width), rng.Next(height), rng.Next(2, 6), RandomColor());

            DrawFrame(pixels, width, height, 0, 28, frame);
            DrawFrame(pixels, width, height, 28, 6, accent);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static void FillRect(Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32 color)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(height, y + h); py++)
                for (int px = Mathf.Max(0, x); px < Mathf.Min(width, x + w); px++)
                    pixels[py * width + px] = color;
        }

        static void FillCircle(Color32[] pixels, int width, int height, int cx, int cy, int radius, Color32 color)
        {
            for (int py = Mathf.Max(0, cy - radius); py <= Mathf.Min(height - 1, cy + radius); py++)
                for (int px = Mathf.Max(0, cx - radius); px <= Mathf.Min(width - 1, cx + radius); px++)
                    if ((px - cx) * (px - cx) + (py - cy) * (py - cy) <= radius * radius)
                        pixels[py * width + px] = color;
        }

        static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int s = 0; s <= steps; s += 2)
                FillCircle(pixels, width, height, x0 + (x1 - x0) * s / steps, y0 + (y1 - y0) * s / steps, thickness / 2, color);
        }

        static void DrawFrame(Color32[] pixels, int width, int height, int offset, int thickness, Color32 color)
        {
            for (int py = offset; py < height - offset; py++)
                for (int px = offset; px < width - offset; px++)
                {
                    bool inner = px >= offset + thickness && px < width - offset - thickness &&
                                 py >= offset + thickness && py < height - offset - thickness;
                    if (!inner)
                        pixels[py * width + px] = color;
                }
        }

        // ------------------------------------------------------------------ Carta 1: mapa

        static GameObject BuildMapContent(Palette p)
        {
            var root = new GameObject("MapContent");
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.AddComponent<ModelTouchController>();

            if (!TryBuildRiftModel(pivot.transform, out var markers))
                BuildProceduralRift(pivot.transform, p);

            foreach (var role in Roles)
            {
                var position = markers != null && markers.TryGetValue(role.Id, out var marker)
                    ? marker
                    : new Vector3(role.Position.x * MapSize, 0f, role.Position.y * MapSize);
                BuildLanePopup(pivot.transform, role, position, p);
            }

            AddMusic(root, MapMusicPath);
            return root;
        }

        /// <summary>
        /// Coloca el modelo de la Grieta exportado desde Blender. El GLB trae empties "LoLAR_*" a nivel del suelo:
        /// SquareMin/SquareMax (fuentes) definen la escala, Center el anclaje y Top/Jungla/Mid/Bot/Soporte los pop-ups.
        /// </summary>
        static bool TryBuildRiftModel(Transform pivot, out Dictionary<string, Vector3> markers)
        {
            markers = null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MapModelPath);
            if (!asset)
            {
                Debug.LogWarning($"[LoL AR] No se encontró {MapModelPath}; uso el mapa de figuras simples.");
                return false;
            }

            var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (!instance)
                instance = Object.Instantiate(asset);
            instance.name = "SummonersRift_Model";
            instance.transform.SetParent(pivot, false);

            var children = instance.GetComponentsInChildren<Transform>(true);
            var squareMin = children.FirstOrDefault(t => t.name == MarkerPrefix + "SquareMin");
            var squareMax = children.FirstOrDefault(t => t.name == MarkerPrefix + "SquareMax");
            var center = children.FirstOrDefault(t => t.name == MarkerPrefix + "Center");

            if (!squareMin || !squareMax)
            {
                Debug.LogWarning("[LoL AR] El modelo del mapa no tiene marcadores LoLAR_*; lo ajusto por sus bounds.");
                if (TryGetBounds(instance.transform, out var bounds))
                {
                    instance.transform.localScale *= MapSize * 1.3f / Mathf.Max(bounds.size.x, bounds.size.z);
                    TryGetBounds(instance.transform, out bounds);
                    instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                }
                BuildMapFillPlane(pivot, instance);
                return true;
            }

            var diagonal = squareMax.position - squareMin.position;
            instance.transform.localScale *= MapSize / Mathf.Max(Mathf.Abs(diagonal.x), Mathf.Abs(diagonal.z));

            var anchor = center ? center.position : (squareMin.position + squareMax.position) * 0.5f;
            instance.transform.position -= anchor;

            markers = new Dictionary<string, Vector3>();
            foreach (var child in children)
            {
                if (child.name.StartsWith(MarkerPrefix))
                    markers[child.name.Substring(MarkerPrefix.Length)] = pivot.InverseTransformPoint(child.position);
            }
            BuildMapFillPlane(pivot, instance);
            return true;
        }

        /// <summary>
        /// Plano con la imagen de relleno justo debajo del modelo, para que tape los huecos sin textura
        /// del GLB (algunas zonas no tenían texturas al descargarlo). No hace nada si falta la imagen.
        /// </summary>
        static void BuildMapFillPlane(Transform pivot, GameObject riftInstance)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapFillTexturePath);
            if (!texture)
            {
                Debug.LogWarning($"[LoL AR] No se encontró {MapFillTexturePath}; el mapa puede mostrar huecos sin textura.");
                return;
            }

            if (!TryGetBounds(riftInstance.transform, out var bounds))
                return;

            var fill = Prim(PrimitiveType.Quad, pivot, "MapFill",
                new Vector3(0f, bounds.min.y - 0.0005f, 0f), Vector3.one * (MapSize * 1.05f), MapFillMaterial(texture));
            fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Boca arriba.
        }

        static Material MapFillMaterial(Texture2D texture)
        {
            const string path = MaterialsDir + "/MapFill.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Mapa de figuras simples, usado si no está el modelo de Blender.</summary>
        static void BuildProceduralRift(Transform pivot, Palette p)
        {
            var rift = new GameObject("SummonersRift").transform;
            rift.SetParent(pivot, false);
            rift.localScale = Vector3.one * MapSize;

            Prim(PrimitiveType.Cube, rift, "Base", new Vector3(0f, -0.02f, 0f), new Vector3(1f, 0.04f, 1f), p.Grass);
            Prim(PrimitiveType.Cube, rift, "River", new Vector3(0f, 0.001f, 0f), new Vector3(0.1f, 0.004f, 1.25f), p.River, Quaternion.Euler(0f, -45f, 0f));

            Prim(PrimitiveType.Cube, rift, "TopLane_A", new Vector3(-0.4f, 0.003f, 0.02f), new Vector3(0.07f, 0.004f, 0.82f), p.Lane);
            Prim(PrimitiveType.Cube, rift, "TopLane_B", new Vector3(0.02f, 0.003f, 0.4f), new Vector3(0.82f, 0.004f, 0.07f), p.Lane);
            Prim(PrimitiveType.Cube, rift, "MidLane", new Vector3(0f, 0.003f, 0f), new Vector3(0.07f, 0.004f, 1.1f), p.Lane, Quaternion.Euler(0f, 45f, 0f));
            Prim(PrimitiveType.Cube, rift, "BotLane_A", new Vector3(-0.02f, 0.003f, -0.4f), new Vector3(0.82f, 0.004f, 0.07f), p.Lane);
            Prim(PrimitiveType.Cube, rift, "BotLane_B", new Vector3(0.4f, 0.003f, -0.02f), new Vector3(0.07f, 0.004f, 0.82f), p.Lane);

            Prim(PrimitiveType.Cylinder, rift, "BlueBase", new Vector3(-0.4f, 0.004f, -0.4f), new Vector3(0.26f, 0.003f, 0.26f), p.BlueGround);
            Prim(PrimitiveType.Cylinder, rift, "RedBase", new Vector3(0.4f, 0.004f, 0.4f), new Vector3(0.26f, 0.003f, 0.26f), p.RedGround);
            Prim(PrimitiveType.Cylinder, rift, "BlueNexus", new Vector3(-0.42f, 0.05f, -0.42f), new Vector3(0.07f, 0.05f, 0.07f), p.Blue);
            Prim(PrimitiveType.Cylinder, rift, "RedNexus", new Vector3(0.42f, 0.05f, 0.42f), new Vector3(0.07f, 0.05f, 0.07f), p.Red);

            Prim(PrimitiveType.Cylinder, rift, "DragonPit", new Vector3(0.22f, 0.004f, -0.22f), new Vector3(0.14f, 0.004f, 0.14f), p.Rock);
            Prim(PrimitiveType.Cylinder, rift, "BaronPit", new Vector3(-0.22f, 0.004f, 0.22f), new Vector3(0.14f, 0.004f, 0.14f), p.Rock);

            var blueTurrets = new[] { new Vector2(-0.4f, -0.1f), new Vector2(-0.4f, 0.15f), new Vector2(-0.15f, -0.15f), new Vector2(-0.1f, -0.4f), new Vector2(0.15f, -0.4f) };
            var redTurrets = new[] { new Vector2(-0.1f, 0.4f), new Vector2(0.15f, 0.4f), new Vector2(0.15f, 0.15f), new Vector2(0.4f, -0.15f), new Vector2(0.4f, 0.1f) };
            foreach (var t in blueTurrets)
                Prim(PrimitiveType.Cylinder, rift, "BlueTurret", new Vector3(t.x, 0.05f, t.y), new Vector3(0.03f, 0.05f, 0.03f), p.Blue);
            foreach (var t in redTurrets)
                Prim(PrimitiveType.Cylinder, rift, "RedTurret", new Vector3(t.x, 0.05f, t.y), new Vector3(0.03f, 0.05f, 0.03f), p.Red);

            var jungleCamps = new[] { new Vector2(-0.25f, -0.05f), new Vector2(0.05f, -0.25f), new Vector2(0.25f, 0.05f), new Vector2(-0.05f, 0.25f) };
            foreach (var camp in jungleCamps)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * 120f + camp.x * 300f;
                    var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.06f);
                    Prim(PrimitiveType.Cube, rift, "JungleWall", new Vector3(camp.x + offset.x, 0.02f, camp.y + offset.z),
                        new Vector3(0.07f, 0.04f, 0.035f), p.Jungle, Quaternion.Euler(0f, angle, 0f));
                }
            }

        }

        static void BuildLanePopup(Transform parent, RoleDef role, Vector3 localPosition, Palette p)
        {
            var popupGo = new GameObject("Popup_" + role.Id);
            popupGo.transform.SetParent(parent, false);
            popupGo.transform.localPosition = localPosition;
            var roleMaterial = Palette.Lit("Role_" + role.Id, role.Color, 0.6f, true);

            Prim(PrimitiveType.Cylinder, popupGo.transform, "Pole", new Vector3(0f, IconHeight * 0.5f, 0f), new Vector3(0.0012f, IconHeight * 0.5f, 0.0012f), p.Pole);
            Prim(PrimitiveType.Cylinder, popupGo.transform, "Ring", new Vector3(0f, 0.0015f, 0f), new Vector3(0.014f, 0.0008f, 0.014f), roleMaterial);

            var icon = new GameObject("Icon").transform;
            icon.SetParent(popupGo.transform, false);
            icon.localPosition = Vector3.up * IconHeight;
            BuildIcon(icon, role.Icon, roleMaterial, p);
            icon.gameObject.AddComponent<SphereCollider>().radius = 0.014f;

            var label = CreateWorldCanvas(popupGo.transform, "Label", new Vector2(360f, 80f), Vector3.up * (IconHeight + 0.018f), 0.00012f, new Color(0f, 0f, 0f, 0.6f));
            AddText(label, role.DisplayName, p.Font, 50, role.Color, FontStyle.Bold, TextAnchor.MiddleCenter, 0f, 80f);

            var panel = CreateWorldCanvas(popupGo.transform, "InfoPanel", new Vector2(560f, 420f), Vector3.up * (IconHeight + 0.068f), 0.00016f, new Color(0.04f, 0.05f, 0.09f, 0.88f));
            AddText(panel, role.DisplayName, p.Font, 56, role.Color, FontStyle.Bold, TextAnchor.MiddleLeft, 16f, 70f);
            AddText(panel, "Función táctica", p.Font, 30, p.GoldText, FontStyle.Bold, TextAnchor.UpperLeft, 96f, 40f);
            AddText(panel, role.Function, p.Font, 30, Color.white, FontStyle.Normal, TextAnchor.UpperLeft, 136f, 120f);
            AddText(panel, "Campeones insignia", p.Font, 30, p.GoldText, FontStyle.Bold, TextAnchor.UpperLeft, 262f, 40f);
            AddText(panel, role.Champions, p.Font, 30, Color.white, FontStyle.Normal, TextAnchor.UpperLeft, 302f, 80f);
            AddText(panel, "Toca el ícono para cerrar", p.Font, 22, new Color(0.7f, 0.7f, 0.75f), FontStyle.Italic, TextAnchor.LowerRight, 380f, 32f);

            var popup = popupGo.AddComponent<LanePopup>();
            popup.icon = icon;
            popup.infoPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        static void BuildIcon(Transform holder, IconShape shape, Material roleMaterial, Palette p)
        {
            switch (shape)
            {
                case IconShape.Shield: // Escudo
                    Prim(PrimitiveType.Cylinder, holder, "Shield", Vector3.zero, new Vector3(0.022f, 0.0015f, 0.026f), roleMaterial, Quaternion.Euler(90f, 0f, 0f));
                    Prim(PrimitiveType.Sphere, holder, "Boss", Vector3.zero, Vector3.one * 0.0065f, p.Gold);
                    break;

                case IconShape.Claw: // Garra
                    for (int i = -1; i <= 1; i++)
                        Prim(PrimitiveType.Capsule, holder, "Claw", new Vector3(i * 0.006f, 0f, 0f), new Vector3(0.0035f, 0.009f, 0.0035f), roleMaterial, Quaternion.Euler(0f, 0f, -i * 18f));
                    break;

                case IconShape.Orb: // Orbe
                    Prim(PrimitiveType.Sphere, holder, "Orb", Vector3.zero, Vector3.one * 0.017f, roleMaterial);
                    Prim(PrimitiveType.Cylinder, holder, "OrbRing", Vector3.zero, new Vector3(0.026f, 0.0005f, 0.026f), p.Gold, Quaternion.Euler(70f, 0f, 0f));
                    break;

                case IconShape.Bow: // Arco
                    const float radius = 0.012f;
                    for (int i = 0; i <= 8; i++)
                    {
                        float angle = Mathf.Lerp(-70f, 70f, i / 8f);
                        float rad = angle * Mathf.Deg2Rad;
                        Prim(PrimitiveType.Cube, holder, "BowSegment", new Vector3(Mathf.Cos(rad) * radius - radius * 0.5f, Mathf.Sin(rad) * radius, 0f),
                            new Vector3(0.003f, 0.0045f, 0.003f), roleMaterial, Quaternion.Euler(0f, 0f, angle));
                    }
                    float stringX = Mathf.Cos(70f * Mathf.Deg2Rad) * radius - radius * 0.5f;
                    Prim(PrimitiveType.Cube, holder, "BowString", new Vector3(stringX, 0f, 0f), new Vector3(0.0007f, 2f * Mathf.Sin(70f * Mathf.Deg2Rad) * radius, 0.0007f), p.Pole);
                    Prim(PrimitiveType.Cube, holder, "Arrow", new Vector3(0.001f, 0f, 0f), new Vector3(0.018f, 0.0008f, 0.0008f), p.Gold);
                    break;

                case IconShape.Amulet: // Amuleto
                    Prim(PrimitiveType.Cube, holder, "Gem", Vector3.zero, new Vector3(0.012f, 0.012f, 0.004f), roleMaterial, Quaternion.Euler(0f, 0f, 45f));
                    Prim(PrimitiveType.Sphere, holder, "Loop", new Vector3(0f, 0.011f, 0f), Vector3.one * 0.004f, p.Gold);
                    break;
            }
        }

        // ------------------------------------------------------------------ Carta 2: dragón y batalla

        static GameObject BuildDragonContent(Palette p)
        {
            var root = new GameObject("DragonContent");
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.AddComponent<ModelTouchController>();

            BuildPit(pivot.transform, 0.12f, p);

            // Carta del dragón sola: dragón volando. El Dragón Ancestral con su animación queda para la batalla.
            // Label distinto para no sobrescribir Dragon.controller, que usa BattleContent.
            var modelPath = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingDragonModelPath) ? FlyingDragonModelPath : DragonModelPath;
            var dragon = new GameObject("DragonFlying").transform;
            dragon.SetParent(pivot.transform, false);
            SpawnModel(modelPath, dragon, DragonHeight, DragonYaw, "DragonFlying");
            dragon.localRotation = Quaternion.Euler(0f, 180f, 0f); // Mirando hacia quien sostiene la carta.

            var looper = dragon.gameObject.AddComponent<ModelAnimationLooper>();
            looper.pauseBetweenCycles = Vector2.zero;

            // "Landing" vuela hasta ~4,3 s y luego aterriza. Se repite solo un tramo de vuelo cuyas poses
            // inicial y final coinciden (altura del cuerpo y aleteo), para que se quede volando.
            float clipLength = GetClipLength(modelPath);
            if (modelPath == FlyingDragonModelPath && clipLength > FlyingLoopEndSeconds)
            {
                looper.segmentStart = FlyingLoopStartSeconds / clipLength;
                looper.segmentEnd = FlyingLoopEndSeconds / clipLength;
                looper.segmentBlend = 0.2f;
            }

            BuildDragonInfoPopup(pivot.transform, p);
            AddMusic(root, DragonMusicPath);
            return root;
        }

        /// <summary>Ícono con información del Dragón como objetivo, igual que los pop-ups de rol del mapa.</summary>
        static void BuildDragonInfoPopup(Transform parent, Palette p)
        {
            var popupGo = new GameObject("Popup_Dragon");
            popupGo.transform.SetParent(parent, false);
            popupGo.transform.localPosition = new Vector3(0.07f, 0f, 0f);
            var material = Palette.Lit("DragonObjective", new Color(1f, 0.55f, 0.15f), 0.6f, true);

            Prim(PrimitiveType.Cylinder, popupGo.transform, "Pole", new Vector3(0f, IconHeight * 0.5f, 0f), new Vector3(0.0012f, IconHeight * 0.5f, 0.0012f), p.Pole);
            Prim(PrimitiveType.Cylinder, popupGo.transform, "Ring", new Vector3(0f, 0.0015f, 0f), new Vector3(0.014f, 0.0008f, 0.014f), material);

            var icon = new GameObject("Icon").transform;
            icon.SetParent(popupGo.transform, false);
            icon.localPosition = Vector3.up * IconHeight;
            Prim(PrimitiveType.Sphere, icon, "Orb", Vector3.zero, Vector3.one * 0.017f, material);
            icon.gameObject.AddComponent<SphereCollider>().radius = 0.014f;

            var label = CreateWorldCanvas(popupGo.transform, "Label", new Vector2(360f, 80f), Vector3.up * (IconHeight + 0.018f), 0.00012f, new Color(0f, 0f, 0f, 0.6f));
            AddText(label, "Dragón", p.Font, 50, new Color(1f, 0.55f, 0.15f), FontStyle.Bold, TextAnchor.MiddleCenter, 0f, 80f);

            var panel = CreateWorldCanvas(popupGo.transform, "InfoPanel", new Vector2(560f, 460f), Vector3.up * (IconHeight + 0.07f), 0.00016f, new Color(0.04f, 0.05f, 0.09f, 0.88f));
            AddText(panel, "Dragón Ancestral", p.Font, 56, new Color(1f, 0.55f, 0.15f), FontStyle.Bold, TextAnchor.MiddleLeft, 16f, 70f);
            AddText(panel, "Objetivo neutral", p.Font, 30, p.GoldText, FontStyle.Bold, TextAnchor.UpperLeft, 96f, 40f);
            AddText(panel, "Vive en su fosa desde el minuto 5. Al derrotarlo, tu equipo recibe una mejora permanente; reunir 4 mejoras del mismo tipo otorga el Alma del Dragón.",
                p.Font, 26, Color.white, FontStyle.Normal, TextAnchor.UpperLeft, 136f, 130f);
            AddText(panel, "Quién suele pelearlo", p.Font, 30, p.GoldText, FontStyle.Bold, TextAnchor.UpperLeft, 282f, 40f);
            AddText(panel, "Jungla y Bot/Soporte, por la cercanía de la línea inferior a su fosa.", p.Font, 26, Color.white, FontStyle.Normal, TextAnchor.UpperLeft, 322f, 80f);
            AddText(panel, "Toca el ícono para cerrar", p.Font, 22, new Color(0.7f, 0.7f, 0.75f), FontStyle.Italic, TextAnchor.LowerRight, 410f, 32f);

            var popup = popupGo.AddComponent<LanePopup>();
            popup.icon = icon;
            popup.infoPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        static float GetClipLength(string assetPath)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            return clip ? clip.length : 0f;
        }

        static GameObject BuildBattleContent(Palette p)
        {
            var root = new GameObject("BattleContent");
            var director = root.AddComponent<BattleDirector>();
            var pit = BuildPit(root.transform, BattleRadius * 2.3f, p);

            var dragon = new GameObject("Dragon").transform;
            dragon.SetParent(root.transform, false);
            SpawnModel(DragonModelPath, dragon, DragonHeight, DragonYaw, "Dragon");
            dragon.localRotation = Quaternion.Euler(0f, 180f, 0f);
            dragon.gameObject.AddComponent<ModelAnimationLooper>().pauseBetweenCycles = Vector2.zero;

            var fire = VfxFactory.CreateFire(dragon, p.Particles);
            fire.transform.localPosition = new Vector3(0f, DragonHeight * 0.7f, DragonHeight * 0.6f);
            fire.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

            var roar = VfxFactory.CreateRoarBurst(dragon, p.Particles);
            roar.transform.localPosition = new Vector3(0f, DragonHeight * 0.7f, DragonHeight * 0.4f);

            var hitSparks = VfxFactory.CreateHitSparks(root.transform, p.Particles);

            var champions = new Transform[Champions.Length];
            var flashes = new ParticleSystem[Champions.Length];
            var hitColors = new Color[Champions.Length];
            for (int i = 0; i < Champions.Length; i++)
            {
                var def = Champions[i];
                var holder = new GameObject(def.Name).transform;
                holder.SetParent(root.transform, false);
                SpawnModel(def.ModelPath, holder, def.Height, ChampionYaw, def.Name);

                // Semicírculo del lado -Z (el más cercano a quien sostiene la carta), mirando al dragón.
                float angle = Mathf.Lerp(120f, 240f, i / (float)(Champions.Length - 1)) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * BattleRadius;
                holder.localPosition = position;
                holder.localRotation = Quaternion.LookRotation(-position.normalized, Vector3.up);

                holder.gameObject.AddComponent<ModelAnimationLooper>().pauseBetweenCycles = new Vector2(0.3f, 1.4f);

                var flash = VfxFactory.CreateSpawnFlash(holder, p.Particles);
                flash.transform.localPosition = Vector3.up * 0.01f;

                champions[i] = holder;
                flashes[i] = flash;
                hitColors[i] = def.HitColor;
            }

            director.pit = pit;
            director.dragon = dragon;
            director.champions = champions;
            director.dragonFire = fire;
            director.roarBurst = roar;
            director.hitSparks = hitSparks;
            director.spawnFlashes = flashes;
            director.championHitColors = hitColors;
            director.dragonHeight = DragonHeight;
            AddMusic(root, BattleMusicPath);
            return root;
        }

        /// <summary>Agrega un AudioSource en bucle que suena solo mientras <paramref name="root"/> está activo.</summary>
        static void AddMusic(GameObject root, string clipPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (!clip)
            {
                Debug.LogWarning($"[LoL AR] No se encontró {clipPath}; ese contenido no tendrá música.");
                return;
            }

            var source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // Música 2D: no depende de la posición de la carta.
            root.AddComponent<AutoLoopMusic>();
        }

        static Transform BuildPit(Transform parent, float diameter, Palette p)
        {
            var pit = new GameObject("Pit").transform;
            pit.SetParent(parent, false);
            Prim(PrimitiveType.Cylinder, pit, "PitRim", new Vector3(0f, 0.0006f, 0f), new Vector3(diameter * 1.08f, 0.0006f, diameter * 1.08f), p.Lava);
            Prim(PrimitiveType.Cylinder, pit, "PitFloor", new Vector3(0f, 0.0009f, 0f), new Vector3(diameter, 0.0008f, diameter), p.Rock);
            return pit;
        }

        static GameObject SpawnModel(string assetPath, Transform holder, float height, float yaw, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject instance;
            if (asset)
            {
                instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                if (!instance)
                    instance = Object.Instantiate(asset);
            }
            else
            {
                Debug.LogWarning($"[LoL AR] No se pudo cargar {assetPath}. ¿Terminó de instalarse glTFast? Uso una cápsula temporal.");
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(instance.GetComponent<Collider>());
            }

            instance.name = label + "_Model";
            instance.transform.SetParent(holder, false);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            FitToHeight(instance.transform, holder, height);

            if (asset)
                SetupAnimation(instance, assetPath, label);

            return instance;
        }

        /// <summary>Escala el modelo a la altura pedida y apoya sus pies en el origen del holder.</summary>
        static void FitToHeight(Transform model, Transform holder, float height)
        {
            if (!TryGetBounds(model, out var bounds) || bounds.size.y < 1e-5f)
                return;

            model.localScale *= height / bounds.size.y;
            TryGetBounds(model, out bounds);
            var bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.position += holder.position - bottomCenter;
        }

        static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        /// <summary>
        /// glTFast puede importar las animaciones como Legacy o Mecanim; se soportan ambas.
        /// Para Mecanim se crea un AnimatorController con un único estado "Loop".
        /// </summary>
        static void SetupAnimation(GameObject instance, string assetPath, string label)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!clip)
            {
                Debug.LogWarning($"[LoL AR] {assetPath} no tiene animaciones importadas.");
                return;
            }

            if (clip.legacy)
            {
                var animation = instance.GetComponentInChildren<Animation>(true);
                if (!animation)
                    animation = instance.AddComponent<Animation>();
                if (!animation.GetClip(clip.name))
                    animation.AddClip(clip, clip.name);
                animation.clip = clip;
                animation.playAutomatically = false;
                return;
            }

            var controllerPath = $"{AnimationsDir}/{label}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState(ModelAnimationLooper.LoopStateName);
            state.motion = clip;
            stateMachine.defaultState = state;
            // Copia del estado para mezclar al repetir solo un tramo del clip.
            stateMachine.AddState(ModelAnimationLooper.LoopStateNameB).motion = clip;

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (!animator)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        // ------------------------------------------------------------------ Escena

        static void BuildScene(XRReferenceImageLibrary library, GameObject mapPrefab, GameObject dragonPrefab, GameObject battlePrefab)
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            var originGo = new GameObject("XR Origin (AR)");
            var origin = originGo.AddComponent<XROrigin>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originGo.transform, false);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(cameraOffset.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();

            var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            var poseDriver = cameraGo.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(positionAction);
            poseDriver.rotationInput = new InputActionProperty(rotationAction);

            cameraGo.AddComponent<TapInteractor>();

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = cameraOffset;

            var imageManager = originGo.AddComponent<ARTrackedImageManager>();
            var serializedManager = new SerializedObject(imageManager);
            var libraryProperty = serializedManager.FindProperty("m_SerializedLibrary");
            var movingImagesProperty = serializedManager.FindProperty("m_MaxNumberOfMovingImages");
            if (libraryProperty != null)
                libraryProperty.objectReferenceValue = library;
            else
                Debug.LogWarning("[LoL AR] Asigna LoLCardsLibrary en XR Origin > AR Tracked Image Manager > Serialized Library.");
            if (movingImagesProperty != null)
                movingImagesProperty.intValue = 2;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            var controller = originGo.AddComponent<CardTrackingController>();
            controller.mapCardName = MapCardName;
            controller.dragonCardName = DragonCardName;
            controller.mapContentPrefab = mapPrefab;
            controller.dragonContentPrefab = dragonPrefab;
            controller.battleContentPrefab = battlePrefab;

            if (ShowDebugHud)
                cameraGo.AddComponent<DebugHud>().controller = controller;
        }

        // ------------------------------------------------------------------ Utilidades

        static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider) // El Quad no trae collider por defecto; los demás primitivos sí.
                Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static RectTransform CreateWorldCanvas(Transform parent, string name, Vector2 size, Vector3 localPosition, float scale, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Billboard));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.localPosition = localPosition;
            rect.localScale = Vector3.one * scale;

            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundGo.transform.SetParent(go.transform, false);
            var backgroundRect = (RectTransform)backgroundGo.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var image = backgroundGo.GetComponent<Image>();
            image.color = background;
            image.raycastTarget = false;

            return rect;
        }

        static void AddText(RectTransform parent, string content, Font font, int fontSize, Color color, FontStyle style, TextAnchor anchor, float top, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-40f, height);
            rect.anchoredPosition = new Vector2(0f, -top);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Datos

        enum IconShape { Shield, Claw, Orb, Bow, Amulet }

        readonly struct RoleDef
        {
            public readonly string Id, DisplayName, Function, Champions;
            public readonly IconShape Icon;
            public readonly Vector2 Position;
            public readonly Color Color;

            public RoleDef(string id, string displayName, string function, string champions, IconShape icon, Vector2 position, Color color)
            {
                Id = id;
                DisplayName = displayName;
                Function = function;
                Champions = champions;
                Icon = icon;
                Position = position;
                Color = color;
            }
        }

        readonly struct ChampionDef
        {
            public readonly string Name, ModelPath;
            public readonly float Height;
            public readonly Color HitColor;

            public ChampionDef(string name, string modelPath, float height, Color hitColor)
            {
                Name = name;
                ModelPath = modelPath;
                Height = height;
                HitColor = hitColor;
            }
        }

        sealed class Palette
        {
            public readonly Material Grass, Lane, River, Jungle, Rock, Blue, Red, BlueGround, RedGround, Gold, Pole, Lava, Particles;
            public readonly Font Font;
            public readonly Color GoldText = new Color(0.95f, 0.8f, 0.4f);

            public Palette()
            {
                Grass = Lit("Grass", new Color(0.16f, 0.36f, 0.18f));
                Lane = Lit("Lane", new Color(0.62f, 0.55f, 0.4f));
                River = Lit("River", new Color(0.15f, 0.45f, 0.75f), 0.8f);
                Jungle = Lit("Jungle", new Color(0.08f, 0.22f, 0.1f));
                Rock = Lit("Rock", new Color(0.22f, 0.2f, 0.2f));
                Blue = Lit("TeamBlue", new Color(0.2f, 0.45f, 1f), 0.5f, true);
                Red = Lit("TeamRed", new Color(1f, 0.25f, 0.2f), 0.5f, true);
                BlueGround = Lit("BlueGround", new Color(0.12f, 0.2f, 0.35f));
                RedGround = Lit("RedGround", new Color(0.35f, 0.14f, 0.12f));
                Gold = Lit("Gold", new Color(1f, 0.8f, 0.3f), 0.8f, true);
                Pole = Lit("Pole", new Color(0.9f, 0.92f, 1f), 0.5f, true);
                Lava = Lit("PitRim", new Color(1f, 0.45f, 0.1f), 0.3f, true);
                Particles = ParticleMaterial();
                Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            public static Material Lit(string name, Color color, float smoothness = 0.2f, bool emissive = false)
            {
                var path = $"{MaterialsDir}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.6f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(material);
                return material;
            }

            static Material ParticleMaterial()
            {
                var texturePath = MaterialsDir + "/SoftDot.png";
                if (!File.Exists(texturePath))
                {
                    const int size = 64;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                            float alpha = Mathf.Clamp01(1f - d);
                            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                        }
                    File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                var path = MaterialsDir + "/ParticlesAdditive.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 2f);   // Additive
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
