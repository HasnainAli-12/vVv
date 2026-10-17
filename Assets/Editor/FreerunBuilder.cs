using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using NasCon.Game;

namespace NasCon.Level.EditorTools
{
    /// <summary>
    /// Generates the freerunning course from LEVEL_DESIGN.md.
    ///
    /// Design rules enforced here:
    ///  - no walkable surface is smaller than 8 x 8 m (catwalk WIDTHS are the only narrow things)
    ///  - gaps are 4-6 m flat, or 6.5-9 m with a 3-5 m drop to extend the airtime
    ///  - every element is a run surface, vault, slide opening, wallrun wall, cover piece or hazard
    ///
    /// The two full-body vaults are built at ~1.25 m rather than the designed 1.8-2.4 m because the
    /// character's 1.30 m jump apex cannot clear the design height and no vault ability exists yet.
    /// Raise them once that ability ships.
    /// </summary>
    public static class FreerunBuilder
    {
        const string ScenePath      = "Assets/Scenes/Freerun.unity";
        const string MaterialFolder = "Assets/Materials/Level";
        const string ProfilePath    = "Assets/Settings/NightParkourProfile.asset";
        const string EdgeShader     = "Custom/EmberEdgeGlow";
        const string ParticleShader = "Custom/EmberParticle";
        const string PlayerPrefab   = "Assets/Prefabs/Character/Prefabs/Player.prefab";
        const string InputAsset     = "Assets/InputSystem_Actions.inputactions";
        const string TorchPrefab    = "Assets/Prefabs/Torch/torch.fbx";
        const string TornadoPrefab  = "Assets/Prefabs/Fire Tornado/an_tornado.fbx";

        public const int WallRunLayer = 6;

        // palette — raised from the original night values so surfaces stay readable
        static readonly Color BaseDark    = new Color(0.130f, 0.115f, 0.120f);
        static readonly Color BaseWarm    = new Color(0.195f, 0.140f, 0.120f);
        static readonly Color EdgeEmber   = new Color(1.00f, 0.32f, 0.06f);
        static readonly Color CornerEmber = new Color(1.00f, 0.62f, 0.18f);
        static readonly Color EdgeDeep    = new Color(0.80f, 0.14f, 0.04f);
        static readonly Color CornerDeep  = new Color(1.00f, 0.30f, 0.07f);
        static readonly Color EdgeHot     = new Color(1.00f, 0.60f, 0.16f);
        static readonly Color CornerHot   = new Color(1.00f, 0.90f, 0.50f);

        static Dictionary<string, Material> materials;
        static Transform root;

        [MenuItem("Tools/Freerun/Rebuild Course")]
        public static void Rebuild()
        {
            if (Shader.Find(EdgeShader) == null) { Debug.LogError("[Freerun] edge shader missing"); return; }

            EditorSceneManager.SaveOpenScenes();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            materials = new Dictionary<string, Material>();
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);
            root = new GameObject("Level").transform;

            SetEnvironment();
            BuildS1();
            BuildS2();
            BuildArena1();
            BuildS4();
            BuildArena2();
            BuildS6();
            BuildBoss();
            Props();
            BuildPlayer();
            BuildHUD();
            BuildVolumes();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }

            Debug.Log("[Freerun] course built -> " + ScenePath);
        }

        // ================================================================ helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material Mat(string preset, Vector3 size)
        {
            string key = preset + "_" + size.x + "_" + size.y + "_" + size.z;
            Material m;
            if (materials.TryGetValue(key, out m)) return m;

            Color baseCol = preset == "hot" ? BaseWarm : BaseDark;
            Color edge    = preset == "hot" ? EdgeHot  : preset == "deep" ? EdgeDeep  : EdgeEmber;
            Color corner  = preset == "hot" ? CornerHot: preset == "deep" ? CornerDeep: CornerEmber;

            m = new Material(Shader.Find(EdgeShader));
            m.SetColor("_BaseColor", baseCol);
            m.SetColor("_EdgeColor", edge);
            m.SetColor("_CornerColor", corner);
            m.SetFloat("_EdgeWidth", 0.05f);
            m.SetFloat("_CornerWidth", 0.09f);
            m.SetFloat("_GlowIntensity", preset == "hot" ? 6.5f : preset == "deep" ? 2.4f : 3.6f);
            m.SetFloat("_CornerIntensity", preset == "hot" ? 10f : preset == "deep" ? 4.5f : 6.5f);
            m.SetVector("_ObjectScale", new Vector4(size.x, size.y, size.z, 0f));
            m.SetFloat("_Smoothness", 0.12f);

            AssetDatabase.CreateAsset(m, MaterialFolder + "/Fs_" + preset + "_" + size.x + "x" + size.y + "x" + size.z + ".mat");
            materials[key] = m;
            return m;
        }

        static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 size, string preset, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(preset, size);
            return go;
        }

        static Transform Area(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        static Light Fire(Transform p, string name, Vector3 pos, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.42f, 0.10f);
            l.intensity = intensity * 1.9f;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.75f;
            return l;
        }

        /// walkable surface. Everything the player stands on goes through here so the 8 m rule is
        /// enforced in one place.
        static GameObject Deck(Transform p, string name, Vector3 centre, float w, float d, float thickness, string preset)
        {
            if (w < 8f || d < 8f) Debug.LogWarning("[Freerun] deck under 8 m: " + name + " " + w + "x" + d);
            return Cube(p, name, centre, new Vector3(w, thickness, d), preset, Vector3.zero);
        }

        /// slide opening: a lintel from 1.0 m up, deliberately wider than the route so there is no
        /// way around it.
        static void SlideGate(Transform p, string name, Vector3 surfaceCentre, float width, Vector3 euler)
        {
            Quaternion rot = Quaternion.Euler(euler);
            Vector3 fwd = rot * Vector3.forward;
            Cube(p, name + "_Lintel", surfaceCentre + Vector3.up * 2.6f, new Vector3(width, 3.2f, 3f), "deep", euler);
            Vector3 side = rot * Vector3.right;
            Cube(p, name + "_P1", surfaceCentre + side * (width * 0.5f + 0.6f) + Vector3.up * 2.4f, new Vector3(1.2f, 4.8f, 3f), "deep", euler);
            Cube(p, name + "_P2", surfaceCentre - side * (width * 0.5f + 0.6f) + Vector3.up * 2.4f, new Vector3(1.2f, 4.8f, 3f), "deep", euler);
        }

        /// half-body vault — railings, container lips, low walls. 1.0 m so the 1.30 m apex clears it.
        static void HalfVault(Transform p, string name, Vector3 surfaceCentre, float width, Vector3 euler)
        {
            Cube(p, name, surfaceCentre + Vector3.up * 0.5f, new Vector3(width, 1.0f, 0.5f), "deep", euler);
        }

        /// full-body vault. Built at 1.25 m for now (see class comment).
        static void FullVault(Transform p, string name, Vector3 surfaceCentre, float width, Vector3 euler)
        {
            Cube(p, name, surfaceCentre + Vector3.up * 0.62f, new Vector3(width, 1.25f, 4f), "deep", euler);
        }

        /// wallrun wall on the WallRun layer, with a lit cap. Base is placed at the given y.
        static void Wall(Transform p, string name, Vector3 a, Vector3 b, float height)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion rot = Quaternion.LookRotation(d.normalized, Vector3.up);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = WallRunLayer;
            go.transform.SetParent(p, false);
            go.transform.position = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
            go.transform.rotation = rot;
            var size = new Vector3(0.7f, height, len);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat("deep", size);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = name + "_Cap";
            cap.transform.SetParent(p, false);
            cap.transform.position = (a + b) * 0.5f + Vector3.up * (height + 0.15f);
            cap.transform.rotation = rot;
            var capSize = new Vector3(1.2f, 0.3f, len);
            cap.transform.localScale = capSize;
            cap.GetComponent<Renderer>().sharedMaterial = Mat("ember", capSize);
        }

        /// horizontal swing pole above a gap. Poles are the intended route; the pit below has a
        /// recovery floor so a miss costs time rather than a restart.
        static void Poles(Transform p, string name, Vector3 from, Vector3 to, int count, float heightAbove)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = Vector3.Lerp(from, to, (i + 1f) / (count + 1f)) + Vector3.up * heightAbove;

                // the bar itself carries the SwingPole marker. The grab volume goes on a separate child
                // at unit scale: a BoxCollider placed on the bar is scaled by the bar's own
                // (0.25, 0.25, 3.2) transform, which turned a 6x4x6 box into a 1.5 x 1.0 x 19.2 m slab -
                // far too flat to grab and reaching 19 m along the bar.
                var bar = Cube(p, name + "_" + i, at, new Vector3(0.25f, 0.25f, 3.2f), "hot", Vector3.zero);
                bar.AddComponent<SwingPole>();

                var grab = new GameObject(name + "_Grab" + i);
                grab.transform.SetParent(bar.transform, false);
                grab.transform.localPosition = Vector3.zero;
                grab.transform.localRotation = Quaternion.identity;
                grab.transform.localScale = Vector3.one;
                var trigger = grab.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(4.5f, 4f, 5f);   // grab volume around the bar, unscaled

                Cube(p, name + "_PostA" + i, at + side * -1.8f, new Vector3(0.18f, heightAbove, 0.18f), "deep", Vector3.zero);
                Cube(p, name + "_PostB" + i, at + side * 1.8f, new Vector3(0.18f, heightAbove, 0.18f), "deep", Vector3.zero);
            }
        }

        static void Checkpoint(Transform p, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8f, 5f, 8f);
            var cp = go.AddComponent<LevelRespawn>();
            var so = new SerializedObject(cp);
            so.FindProperty("mode").enumValueIndex = 1;
            so.ApplyModifiedProperties();
        }

        static GameObject Prop(Transform p, string name, GameObject src, Vector3 pos, float scale, Vector3 euler)
        {
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ================================================================ world

        static void SetEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.14f, 0.20f, 1f);
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.11f, 0.06f, 0.045f, 1f);
            RenderSettings.fogDensity = 0.0075f;

            // Kenney space skybox (already a cubemap with a Skybox/Cubemap material). Ambient is held
            // down deliberately: the course was tuned against a near-black procedural sky, and letting
            // a star field drive ambient would lift the whole level.
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/kenney_skyboxes-space/Skyboxes/Materials/skybox-space-galaxy.mat");
            if (sky != null)
            {
                if (sky.HasProperty("_Rotation")) sky.SetFloat("_Rotation", 35f);
                RenderSettings.skybox = sky;
            }
            else
            {
                var skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    var fallback = new Material(skyShader);
                    fallback.SetColor("_SkyTint", new Color(0.10f, 0.12f, 0.20f, 1f));
                    fallback.SetColor("_GroundColor", new Color(0.05f, 0.045f, 0.05f, 1f));
                    fallback.SetFloat("_Exposure", 0.75f);
                    fallback.SetFloat("_SunDisk", 0f);
                    fallback.SetFloat("_AtmosphereThickness", 0.7f);
                    AssetDatabase.CreateAsset(fallback, MaterialFolder + "/NightSky.mat");
                    RenderSettings.skybox = fallback;
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.09f, 0.10f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.045f);
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            var moon = new GameObject("Moon");
            moon.transform.SetParent(root, false);
            moon.transform.rotation = Quaternion.Euler(30f, 205f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.63f, 0.85f, 1f);
            light.intensity = 0.95f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            RenderSettings.sun = light;

            var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                var atlas = so.FindProperty("m_AdditionalLightsShadowmapResolution");
                if (atlas != null) { atlas.intValue = 4096; so.ApplyModifiedProperties(); EditorUtility.SetDirty(rp); }
            }
        }

        // ================================================================ S1 .. S7

        static void BuildS1()
        {
            var a = Area("S1_DockApproach");
            float y = 10f;

            Deck(a, "StartDeck", new Vector3(0f, y, 0f), 24f, 18f, 0.8f, "ember");
            Checkpoint(a, "CP_Start", new Vector3(-8f, y + 0.6f, -6f));

            // run-up 12 m then slide tunnel
            Deck(a, "RunIn", new Vector3(0f, y, 14f), 16f, 12f, 0.8f, "ember");
            SlideGate(a, "Slide1", new Vector3(0f, y + 0.4f, 22f), 14f, Vector3.zero);

            // landing + three half-vaults 6 m apart
            Deck(a, "VaultRun", new Vector3(0f, y, 36f), 14f, 22f, 0.8f, "ember");
            HalfVault(a, "HV1", new Vector3(0f, y + 0.4f, 30f), 12f, Vector3.zero);
            HalfVault(a, "HV2", new Vector3(0f, y + 0.4f, 36f), 12f, Vector3.zero);
            HalfVault(a, "HV3", new Vector3(0f, y + 0.4f, 42f), 12f, Vector3.zero);

            // 5 m gap with a 2 m drop
            Deck(a, "GapLanding", new Vector3(0f, y - 2f, 68f), 20f, 20f, 0.8f, "ember");

            // wallrun across a pit onto an 8x8 exit ledge
            Wall(a, "S1Wall", new Vector3(6f, y - 2f, 72f), new Vector3(6f, y - 2f, 90f), 7f);
            Deck(a, "S1WallExit", new Vector3(6f, y - 2f, 94f), 9f, 9f, 0.8f, "ember");
            Fire(a, "S1Light", new Vector3(0f, y + 3f, 36f), 5f, 22f, false);

            // pit floor so a miss is recoverable
            Deck(a, "S1PitFloor", new Vector3(0f, y - 12f, 80f), 26f, 26f, 0.8f, "deep");
        }

        static void BuildS2()
        {
            var a = Area("S2_ContainerStack");
            float y = 6f;

            Deck(a, "Entry", new Vector3(0f, y, 118f), 22f, 18f, 0.8f, "ember");

            // five-beat half-vault rhythm
            for (int i = 0; i < 5; i++)
                HalfVault(a, "HV_" + i, new Vector3(0f, y + 0.4f, 130f + i * 6f), 12f, Vector3.zero);

            // full-body vault (built low for now)
            Deck(a, "MidDeck", new Vector3(0f, y, 152f), 16f, 16f, 0.8f, "ember");
            FullVault(a, "FV1", new Vector3(0f, y + 0.4f, 160f), 14f, Vector3.zero);

            Deck(a, "AfterFV", new Vector3(0f, y, 172f), 16f, 14f, 0.8f, "ember");
            SlideGate(a, "Slide2", new Vector3(0f, y + 0.4f, 180f), 16f, Vector3.zero);

            // 6.5 m gap, 3 m drop
            Deck(a, "GapLanding", new Vector3(0f, y - 3f, 202f), 24f, 18f, 0.8f, "ember");

            // poles over an 18 m pit with a recovery floor
            Deck(a, "PoleExit", new Vector3(0f, y - 3f, 236f), 16f, 12f, 0.8f, "ember");
            Poles(a, "Pole", new Vector3(0f, y - 3f, 212f), new Vector3(0f, y - 3f, 234f), 4, 4f);
            Deck(a, "PolePit", new Vector3(0f, y - 14f, 224f), 30f, 30f, 0.8f, "deep");

            Wall(a, "S2Wall", new Vector3(8f, y - 3f, 238f), new Vector3(8f, y - 3f, 262f), 8f);
            Fire(a, "S2Light", new Vector3(0f, y + 3f, 150f), 5f, 22f, false);
            Checkpoint(a, "CP_S2", new Vector3(-5f, y + 0.6f, 118f));
        }

        static void BuildArena1()
        {
            var a = Area("S3_ContainerYard");
            var c = new Vector3(0f, 0f, 300f);
            Deck(a, "Floor", c, 44f, 44f, 0.8f, "ember");

            Cube(a, "StackA", c + new Vector3(-13f, 2.4f, -11f), new Vector3(12f, 4f, 6f), "deep", new Vector3(0f, 12f, 0f));
            Cube(a, "StackB", c + new Vector3(13f, 2.4f, 12f), new Vector3(12f, 4f, 6f), "deep", new Vector3(0f, -15f, 0f));
            Cube(a, "PillarA", c + new Vector3(9f, 4.5f, -13f), new Vector3(3f, 9f, 3f), "deep", Vector3.zero);
            Cube(a, "PillarB", c + new Vector3(-9f, 4.5f, 14f), new Vector3(3f, 9f, 3f), "deep", Vector3.zero);
            Cube(a, "Platform", c + new Vector3(0f, 0.9f, 0f), new Vector3(10f, 1.2f, 10f), "ember", Vector3.zero);

            Fire(a, "LightA", c + new Vector3(-15f, 5f, -15f), 7f, 26f, true);
            Fire(a, "LightB", c + new Vector3(15f, 5f, 15f), 6f, 24f, false);
            Checkpoint(a, "CP_Arena1", c + new Vector3(-16f, 1.2f, 18f));
        }

        static void BuildS4()
        {
            var a = Area("S4_FoundryWall");
            float y = 0f;

            Deck(a, "Entry", new Vector3(0f, y, 350f), 26f, 20f, 0.8f, "ember");

            // 8 m gap with a 4 m drop
            Deck(a, "AfterGap", new Vector3(0f, y - 4f, 379f), 20f, 16f, 0.8f, "ember");

            // wallrun building-to-building, 12 m pit below
            Wall(a, "S4WallA", new Vector3(9f, y - 4f, 384f), new Vector3(9f, y - 4f, 410f), 9f);
            Deck(a, "WallExitA", new Vector3(9f, y - 4f, 416f), 10f, 8f, 0.8f, "ember");
            Deck(a, "S4PitA", new Vector3(0f, y - 16f, 397f), 34f, 34f, 0.8f, "deep");

            // wallrun into a jump
            Wall(a, "S4WallB", new Vector3(-9f, y - 4f, 418f), new Vector3(-9f, y - 4f, 436f), 7f);
            Deck(a, "CatwalkLanding", new Vector3(-9f, y - 4f, 448f), 12f, 10f, 0.8f, "ember");

            // catwalk with railings
            Cube(a, "Catwalk", new Vector3(-9f, y - 4f, 466f), new Vector3(4f, 0.8f, 20f), "ember", Vector3.zero);
            HalfVault(a, "CV1", new Vector3(-9f, y - 3.6f, 462f), 4f, Vector3.zero);
            HalfVault(a, "CV2", new Vector3(-9f, y - 3.6f, 470f), 4f, Vector3.zero);

            // 9 m gap, 5 m drop
            Deck(a, "LongPitLanding", new Vector3(-9f, y - 9f, 494f), 24f, 18f, 0.8f, "ember");
            Deck(a, "LongPitFloor", new Vector3(-9f, y - 20f, 480f), 32f, 32f, 0.8f, "deep");

            // poles over 22 m, recovery floor
            Poles(a, "S4Pole", new Vector3(-9f, y - 9f, 502f), new Vector3(-9f, y - 9f, 524f), 5, 4f);
            Deck(a, "PoleExit", new Vector3(-9f, y - 9f, 534f), 16f, 12f, 0.8f, "ember");
            Deck(a, "PoleFloor", new Vector3(-9f, y - 20f, 518f), 32f, 32f, 0.8f, "deep");

            SlideGate(a, "Slide3", new Vector3(-9f, y - 8.6f, 542f), 14f, Vector3.zero);
            Fire(a, "S4Light", new Vector3(0f, y + 2f, 400f), 6f, 26f, false);
            Checkpoint(a, "CP_S4", new Vector3(0f, y + 0.6f, 350f));
        }

        static void BuildArena2()
        {
            var a = Area("S5_SmelterHall");
            var c = new Vector3(0f, 0f, 590f);
            Deck(a, "Floor", c, 52f, 52f, 0.8f, "ember");

            Cube(a, "FurnaceA", c + new Vector3(-16f, 3f, -12f), new Vector3(10f, 6f, 8f), "deep", Vector3.zero);
            Cube(a, "FurnaceB", c + new Vector3(16f, 3f, 12f), new Vector3(10f, 6f, 8f), "deep", Vector3.zero);
            Cube(a, "PipeA", c + new Vector3(-14f, 6f, 14f), new Vector3(4f, 12f, 4f), "deep", Vector3.zero);
            Cube(a, "PipeB", c + new Vector3(0f, 6f, -18f), new Vector3(4f, 12f, 4f), "deep", Vector3.zero);
            Cube(a, "PipeC", c + new Vector3(14f, 6f, -14f), new Vector3(4f, 12f, 4f), "deep", Vector3.zero);

            // central crucible: a rim around a hazard pit
            Cube(a, "CrucibleRim", c + new Vector3(0f, 0.6f, 0f), new Vector3(14f, 1.2f, 14f), "deep", Vector3.zero);
            Cube(a, "CrucibleGlow", c + new Vector3(0f, 0.3f, 0f), new Vector3(10f, 1.4f, 10f), "hot", Vector3.zero);

            // mezzanine ring on two sides
            Cube(a, "MezzN", c + new Vector3(0f, 6f, 24f), new Vector3(52f, 0.8f, 6f), "ember", Vector3.zero);
            Cube(a, "MezzW", c + new Vector3(-24f, 6f, 0f), new Vector3(6f, 0.8f, 52f), "ember", Vector3.zero);

            Fire(a, "LightA", c + new Vector3(-20f, 6f, -20f), 8f, 30f, true);
            Fire(a, "LightB", c + new Vector3(20f, 6f, 20f), 7f, 28f, false);
            Fire(a, "LightC", c + new Vector3(0f, 8f, 0f), 6f, 24f, false);
            Checkpoint(a, "CP_Arena2", c + new Vector3(-20f, 1.2f, 20f));
        }

        static void BuildS6()
        {
            var a = Area("S6_GantryRun");

            // the course descends towards the foundry floor
            float y = 4f;
            Deck(a, "EntryMezz", new Vector3(0f, y, 640f), 18f, 10f, 0.8f, "ember");

            Wall(a, "S6Wall", new Vector3(8f, y, 646f), new Vector3(8f, y, 666f), 8f);
            Deck(a, "WallExit", new Vector3(8f, y - 1f, 672f), 14f, 10f, 0.8f, "ember");
            Deck(a, "S6Pit", new Vector3(4f, y - 14f, 658f), 30f, 30f, 0.8f, "deep");

            SlideGate(a, "Slide4", new Vector3(8f, y - 0.6f, 680f), 14f, Vector3.zero);

            // longest pole run, slightly downhill
            Poles(a, "S6Pole", new Vector3(8f, y - 1f, 690f), new Vector3(8f, y - 4f, 716f), 6, 4f);
            Deck(a, "PoleExit", new Vector3(8f, y - 4f, 726f), 18f, 14f, 0.8f, "ember");
            Deck(a, "PoleFloor", new Vector3(8f, y - 16f, 704f), 34f, 34f, 0.8f, "deep");

            Cube(a, "Catwalk", new Vector3(8f, y - 4f, 746f), new Vector3(4f, 0.8f, 24f), "ember", Vector3.zero);
            for (int i = 0; i < 4; i++)
                HalfVault(a, "HV_" + i, new Vector3(8f, y - 3.6f, 738f + i * 6f), 4f, Vector3.zero);

            Deck(a, "LastDeck", new Vector3(8f, y - 4f, 766f), 16f, 12f, 0.8f, "ember");
            FullVault(a, "FV2", new Vector3(8f, y - 3.6f, 760f), 10f, Vector3.zero);

            // final 6 m flat sprint jump
            Deck(a, "FinalJump", new Vector3(8f, y - 4f, 788f), 18f, 14f, 0.8f, "ember");

            // descent ramp into the boss approach
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "BossRamp";
            ramp.transform.SetParent(a, false);
            ramp.transform.position = new Vector3(8f, y - 11f, 812f);
            ramp.transform.rotation = Quaternion.Euler(-19f, 0f, 0f);
            var rs = new Vector3(8f, 0.8f, 44f);
            ramp.transform.localScale = rs;
            ramp.GetComponent<Renderer>().sharedMaterial = Mat("ember", rs);

            Fire(a, "S6Light", new Vector3(8f, y + 2f, 700f), 6f, 26f, false);
            Checkpoint(a, "CP_S6", new Vector3(0f, y + 0.6f, 640f));
        }

        static void BuildBoss()
        {
            var a = Area("S7_Crucible");
            var c = new Vector3(8f, -18f, 872f);
            Deck(a, "Floor", c, 90f, 90f, 1f, "ember");

            // six pillars in a broken ring
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad + 0.25f;
                Vector3 p = c + new Vector3(Mathf.Cos(ang) * 20f, 9f, Mathf.Sin(ang) * 20f);
                Cube(a, "BossPillar_" + i, p, new Vector3(4f, 18f, 4f), "deep", Vector3.zero);
            }

            // elevated perimeter ring at +8 m
            Cube(a, "RingN", c + new Vector3(0f, 8f, 42f), new Vector3(90f, 1f, 6f), "ember", Vector3.zero);
            Cube(a, "RingS", c + new Vector3(0f, 8f, -42f), new Vector3(90f, 1f, 6f), "ember", Vector3.zero);
            Cube(a, "RingE", c + new Vector3(42f, 8f, 0f), new Vector3(6f, 1f, 90f), "ember", Vector3.zero);
            Cube(a, "RingW", c + new Vector3(-42f, 8f, 0f), new Vector3(6f, 1f, 90f), "ember", Vector3.zero);

            // fire grates between the pillars
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad + 0.6f;
                Vector3 p = c + new Vector3(Mathf.Cos(ang) * 13f, 0.4f, Mathf.Sin(ang) * 13f);
                Cube(a, "Grate_" + i, p, new Vector3(6f, 0.3f, 6f), "hot", Vector3.zero);
                Fire(a, "BossLight_" + i, p + Vector3.up * 2.5f, 8f, 30f, i == 0);
            }

            Cube(a, "Beacon", c + new Vector3(0f, 6f, -30f), new Vector3(3f, 26f, 3f), "hot", Vector3.zero);
            Fire(a, "BeaconLight", c + new Vector3(0f, 10f, -30f), 9f, 34f, false);

            Checkpoint(a, "CP_Boss", c + new Vector3(2f, 1.2f, -34f));

            var goal = new GameObject("GoalPad");
            goal.transform.SetParent(a, false);
            goal.transform.position = c + new Vector3(2f, 1.6f, -38f);
            var box = goal.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 10f);
            var gs = goal.AddComponent<LevelGoal>();
            var so = new SerializedObject(gs);
            so.FindProperty("beaconRenderer").objectReferenceValue = a.Find("Beacon").GetComponent<Renderer>();
            so.ApplyModifiedProperties();
        }

        // ================================================================ props

        static void Props()
        {
            var tornadoSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TornadoPrefab);
            var torchSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefab);

            var t = Area("Props_Tornadoes");
            var huge = Prop(t, "Tornado_Huge", tornadoSrc, new Vector3(-90f, 0f, 420f), 3.0f, Vector3.zero);
            var mid = Prop(t, "Tornado_Medium", tornadoSrc, new Vector3(80f, 0f, 300f), 1.6f, Vector3.zero);
            var small = Prop(t, "Tornado_Small", tornadoSrc, new Vector3(70f, 6f, 590f), 0.9f, Vector3.zero);
            foreach (var g in new GameObject[] { huge, mid, small })
                if (g != null) g.AddComponent<SlowSpin>();

            // torches march along the route so the way forward is always lit
            var p = Area("Props_Torches");
            Vector3[] spots = {
                new Vector3(-9f, 10.4f, 0f), new Vector3(9f, 10.4f, 0f),
                new Vector3(-6f, 10.4f, 14f), new Vector3(6f, 10.4f, 14f),
                new Vector3(-6f, 10.4f, 36f), new Vector3(6f, 10.4f, 36f),
                new Vector3(-8f, 8.4f, 68f), new Vector3(8f, 8.4f, 68f),
                new Vector3(-8f, 6.4f, 120f), new Vector3(8f, 6.4f, 120f),
                new Vector3(-6f, 3.4f, 200f), new Vector3(6f, 3.4f, 200f),
                new Vector3(-18f, 0.4f, 288f), new Vector3(18f, 0.4f, 312f),
                new Vector3(-9f, 0.4f, 350f), new Vector3(9f, 0.4f, 350f),
                new Vector3(-18f, 0.4f, 574f), new Vector3(18f, 0.4f, 606f),
                new Vector3(0f, 4.4f, 640f), new Vector3(16f, 0.4f, 726f),
                new Vector3(-30f, -17.6f, 860f), new Vector3(34f, -17.6f, 884f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var torch = Prop(p, "Torch_" + i, torchSrc, spots[i], 1f, new Vector3(0f, i * 43f, 0f));
                if (torch != null) Fire(torch.transform, "TorchLight_" + i, spots[i] + Vector3.up * 1.5f, 3.4f, 14f, false);
            }

            // embers over the whole course
            var embers = new GameObject("Embers");
            embers.transform.SetParent(root, false);
            embers.transform.position = new Vector3(0f, 10f, 430f);
            var ps = embers.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = 9f; main.startSpeed = 0.5f; main.startSize = 0.16f;
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.012f;
            main.startColor = new Color(1f, 0.55f, 0.22f);
            var em = ps.emission; em.rateOverTime = 46f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(220f, 46f, 900f);
            var pr = embers.GetComponent<ParticleSystemRenderer>();
            var pmat = new Material(Shader.Find(ParticleShader));
            AssetDatabase.CreateAsset(pmat, MaterialFolder + "/EmberParticle.mat");
            pr.material = pmat;
        }

        // ================================================================ hud

        /// The HUD lives in a prefab and is instantiated here rather than hand-placed in the scene,
        /// because this builder rebuilds the scene from nothing and would otherwise wipe it.
        static void BuildHUD()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/HUD/PlayerHUD.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[Freerun] PlayerHUD.prefab not found - no HUD in this build");
                return;
            }

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hud.name = "HUD";

            // the pause menu owns Escape now; the camera's own cursor toggle would fight it
            foreach (var orbit in UnityEngine.Object.FindObjectsByType<ThirdPersonOrbitCamera>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(orbit);
                var key = so.FindProperty("toggleCursorKey");
                if (key != null) { key.enumValueIndex = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        // ================================================================ volumes

        /// <summary>
        /// The two Global Volumes and their blend driver.
        ///
        /// Built here rather than left to the scene because this builder reconstructs the scene from
        /// nothing - hand-added volumes were being wiped on every rebuild, which twice left the level
        /// running the wrong colour grade with no rewind effect at all.
        /// </summary>
        static void BuildVolumes()
        {
            var normalProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
                "Assets/Settings/Rewind/NormalProfile.asset");
            var rewindProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
                "Assets/Settings/Rewind/RewindProfile.asset");

            if (normalProfile == null || rewindProfile == null)
            {
                Debug.LogWarning("[Freerun] rewind volume profiles not found - scene will have no colour grade");
                return;
            }

            var normalGo = new GameObject("Global Volume");
            var normalVol = normalGo.AddComponent<UnityEngine.Rendering.Volume>();
            normalVol.isGlobal = true;
            normalVol.priority = 0f;
            normalVol.weight = 1f;
            normalVol.profile = normalProfile;

            var rewindGo = new GameObject("Rewind Volume");
            var rewindVol = rewindGo.AddComponent<UnityEngine.Rendering.Volume>();
            rewindVol.isGlobal = true;
            rewindVol.priority = 1f;          // above normal so it wins when weighted in
            rewindVol.weight = 0f;
            rewindVol.profile = rewindProfile;

            var blend = normalGo.AddComponent<NasCon.FX.RewindVolumeBlend>();
            var so = new SerializedObject(blend);
            so.FindProperty("normalVolume").objectReferenceValue = normalVol;
            so.FindProperty("rewindVolume").objectReferenceValue = rewindVol;

            // Props() has already run by this point, so the embers exist and can be wired to run
            // backwards during a rewind
            var embers = new List<ParticleSystem>();
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.gameObject.name.ToLower().Contains("ember")) embers.Add(ps);

            var arr = so.FindProperty("emberSystems");
            arr.arraySize = embers.Count;
            for (int i = 0; i < embers.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = embers[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ player

        static void BuildPlayer()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            camGo.transform.position = new Vector3(0f, 12.5f, -8f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab == null) { Debug.LogError("[Freerun] player prefab missing: " + PlayerPrefab); return; }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, 10.5f, -4f);
            player.transform.rotation = Quaternion.identity;

            var ctrl = player.transform.Find("States").GetComponent("CharacterStateController");
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("movementReferenceParameters.externalReference").objectReferenceValue = camGo.transform;
            cso.ApplyModifiedProperties();

            var orbit = camGo.AddComponent<ThirdPersonOrbitCamera>();
            var oso = new SerializedObject(orbit);
            oso.FindProperty("target").objectReferenceValue = player.transform;
            oso.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputAsset);
            oso.ApplyModifiedProperties();

            var volGo = new GameObject("Global Volume");
            var volume = volGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(ProfilePath);

            var fall = new GameObject("FallRespawnVolume");
            fall.transform.position = new Vector3(0f, -40f, 430f);
            var fb = fall.AddComponent<BoxCollider>();
            fb.isTrigger = true;
            fb.size = new Vector3(400f, 10f, 1000f);
            var fr = fall.AddComponent<LevelRespawn>();
            var fso = new SerializedObject(fr);
            fso.FindProperty("mode").enumValueIndex = 0;
            fso.ApplyModifiedProperties();

            // audio
            const string musicPath = "Assets/Audio/Y2Mate.is - Parkour Theme - No Copyright Music.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
            if (clip != null)
            {
                var music = new GameObject("Music");
                music.transform.SetParent(root, false);
                var src = music.AddComponent<AudioSource>();
                src.clip = clip; src.loop = true; src.playOnAwake = true;
                src.volume = 0.35f; src.spatialBlend = 0f;
            }
        }
    }
}
