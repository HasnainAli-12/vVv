using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using NasCon.Game;

namespace NasCon.Level.EditorTools
{
    /// <summary>
    /// Generates the "Ember Ascent" night level.
    ///
    /// LAYOUT: one linear route, start to finish, that turns six times and climbs from y=0 to y=21.
    /// Stages connect in sequence only - no loops, no revisiting. Three large open arenas sit along
    /// it for enemy fights, and the decorative tornadoes are parked well off the route as landmarks.
    ///
    /// Re-runnable via Tools > Ember Ascent > Rebuild Level. The layout tables below are the single
    /// source of truth, so tweak numbers and rebuild rather than nudging objects by hand.
    /// </summary>
    public static class EmberAscentBuilder
    {
        const string ScenePath      = "Assets/Scenes/NightParkour.unity";
        const string MaterialFolder = "Assets/Materials/Level";
        const string ProfilePath    = "Assets/Settings/NightParkourProfile.asset";
        const string EdgeShader     = "Custom/EmberEdgeGlow";
        const string ParticleShader = "Custom/EmberParticle";
        const string PlayerPrefab   = "Assets/Prefabs/Character/Prefabs/Player.prefab";
        const string InputAsset     = "Assets/InputSystem_Actions.inputactions";
        const string TorchPrefab    = "Assets/Prefabs/Torch/torch.fbx";
        const string TornadoPrefab  = "Assets/Prefabs/Fire Tornado/an_tornado.fbx";

        // ---- cube sizes. A small set, so we only ever need a small set of materials.
        static readonly Vector3 S_PAD       = new Vector3(8f, 0.5f, 8f);
        static readonly Vector3 S_PAD_S     = new Vector3(4f, 0.5f, 4f);     // compact pad for obstacle chains
        static readonly Vector3 S_PLAT      = new Vector3(5f, 0.5f, 5f);
        static readonly Vector3 S_PLAT_S    = new Vector3(3.5f, 0.4f, 3.5f);
        static readonly Vector3 S_LEDGE     = new Vector3(2.5f, 0.4f, 1.8f);
        static readonly Vector3 S_STEP      = new Vector3(2f, 0.6f, 2f);
        static readonly Vector3 S_COVER     = new Vector3(1f, 1f, 1f);
        static readonly Vector3 S_COVER_W   = new Vector3(2f, 1f, 1f);
        static readonly Vector3 S_BEAM      = new Vector3(0.5f, 0.3f, 8f);
        static readonly Vector3 S_WALL_LONG = new Vector3(0.5f, 6f, 26f);
        static readonly Vector3 S_WALL_S    = new Vector3(0.5f, 4f, 7f);
        static readonly Vector3 S_PILLAR_S  = new Vector3(1f, 4f, 1f);
        static readonly Vector3 S_PILLAR    = new Vector3(3f, 14f, 3f);
        static readonly Vector3 S_BEACON    = new Vector3(1f, 10f, 1f);
        static readonly Vector3 S_BRAZIER   = new Vector3(2f, 1.2f, 2f);
        static readonly Vector3 S_LANTERN   = new Vector3(0.7f, 0.7f, 0.7f);
        static readonly Vector3 S_LEDGE_LONG= new Vector3(3f, 0.3f, 7f);
        static readonly Vector3 S_ARENA_A   = new Vector3(34f, 0.5f, 34f);   // Cinder Yard
        static readonly Vector3 S_ARENA_B   = new Vector3(40f, 0.5f, 40f);   // Ember Arena
        static readonly Vector3 S_ARENA_C   = new Vector3(30f, 0.5f, 30f);   // Kiln Hollow

        // ---- free-running scale. Mirror's Edge reads as momentum across big surfaces, not precision
        //      hops between small pads, so the route is built from decks and wide walkways rather than
        //      3.5m stepping stones. Obstacles sit ON those surfaces instead of replacing them.
        static readonly Vector3 S_DECK      = new Vector3(22f, 0.6f, 22f);
        static readonly Vector3 S_DECK_MID  = new Vector3(16f, 0.6f, 16f);
        static readonly Vector3 S_LANDING   = new Vector3(10f, 0.5f, 10f);
        const float WalkWidth    = 9f;      // wide enough to sprint along without clipping the edge
        const float WalkThick    = 0.6f;

        // ---- palette
        static readonly Color BaseDark    = new Color(0.085f, 0.072f, 0.078f);
        static readonly Color BaseWarm    = new Color(0.135f, 0.095f, 0.080f);
        static readonly Color EdgeEmber   = new Color(1.00f, 0.32f, 0.06f);
        static readonly Color CornerEmber = new Color(1.00f, 0.62f, 0.18f);
        static readonly Color EdgeDeep    = new Color(0.80f, 0.14f, 0.04f);
        static readonly Color CornerDeep  = new Color(1.00f, 0.30f, 0.07f);
        static readonly Color EdgeHot     = new Color(1.00f, 0.60f, 0.16f);
        static readonly Color CornerHot   = new Color(1.00f, 0.90f, 0.50f);

        static Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Transform root;
        static GameObject torchSource;
        static GameObject tornadoSource;

        // Where torches go. y is the surface they stand on, so these track the walkways and decks
        // above rather than floating in space.
        static readonly Vector3[] TorchSpots = {
            new Vector3(-8f, 0.3f, -45f), new Vector3(8f, 0.3f, -45f),          // start deck
            new Vector3(-8f, 0.3f, -35f), new Vector3(8f, 0.3f, -35f),
            new Vector3(-5f, 1.04f, -27.6f), new Vector3(5f, 1.04f, -27.6f),   // rising runway
            new Vector3(-5f, 2.05f, -22f),   new Vector3(5f, 2.05f, -22f),
            new Vector3(-5f, 3.07f, -16.4f), new Vector3(5f, 3.07f, -16.4f),
            new Vector3(-1.5f, 3.65f, -11f), new Vector3(5.5f, 3.65f, -11f),   // turn landing
            new Vector3(14.5f, 4.25f, -10f), new Vector3(21.5f, 4.25f, -10f),
            new Vector3(26f, 5.06f, -9.3f),  new Vector3(31f, 5.66f, -8.3f),   // walkway east
            new Vector3(31f, 6.25f, -6f),    new Vector3(46f, 6.25f, 9f),      // Cinder Yard
            new Vector3(49.8f, 7.25f, 13f),  new Vector3(59.5f, 7.3f, 15f),    // rift
            new Vector3(44f, 9.64f, 40.5f),  new Vector3(26f, 11.26f, 39.5f),  // low walkway
            new Vector3(21f, 13.25f, 30f),   new Vector3(2f, 13.25f, 13f),     // Ember Arena
            new Vector3(-16f, 14.22f, 9.5f), new Vector3(-20f, 15.78f, -0.5f), // rising walkway
            new Vector3(-17f, 16.88f, -0.5f),new Vector3(-6f, 17.83f, -0.5f),  // high walkway
            new Vector3(3f, 20.25f, -6f),    new Vector3(16f, 20.25f, -19f),   // Kiln Hollow
            new Vector3(16f, 20.9f, -29f),   new Vector3(21f, 20.9f, -29f),    // finish
        };

        [MenuItem("Tools/Ember Ascent/Rebuild Level")]
        public static void Rebuild()
        {
            if (Shader.Find(EdgeShader) == null)
            {
                Debug.LogError("[EmberAscent] Shader '" + EdgeShader + "' not found - aborting.");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            materials = new Dictionary<string, Material>();
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);

            root = new GameObject("Level").transform;
            torchSource = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefab);
            tornadoSource = AssetDatabase.LoadAssetAtPath<GameObject>(TornadoPrefab);
            if (torchSource == null) Debug.LogWarning("[EmberAscent] torch source missing: " + TorchPrefab);
            if (tornadoSource == null) Debug.LogWarning("[EmberAscent] tornado source missing: " + TornadoPrefab);

            SetNightEnvironment();
            BuildRoute();
            BuildTornadoes();
            BuildTorches();
            BuildAtmosphere();
            BuildAudio();
            BuildPlayerAndCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AddToBuildSettings();

            Debug.Log("[EmberAscent] Linear route rebuilt -> " + ScenePath);
        }

        // ==================================================================== helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Material MaterialFor(string preset, Vector3 size)
        {
            string key = preset + "_" + size.x + "_" + size.y + "_" + size.z;
            Material cached;
            if (materials.TryGetValue(key, out cached)) return cached;

            Color baseCol = preset == "hot" ? BaseWarm : BaseDark;
            Color edge    = preset == "hot" ? EdgeHot  : preset == "deep" ? EdgeDeep  : EdgeEmber;
            Color corner  = preset == "hot" ? CornerHot: preset == "deep" ? CornerDeep: CornerEmber;
            float glow       = preset == "hot" ? 6.5f : preset == "deep" ? 2.4f : 3.6f;
            float cornerGlow = preset == "hot" ? 10f  : preset == "deep" ? 4.5f : 6.5f;

            var mat = new Material(Shader.Find(EdgeShader));
            mat.SetColor("_BaseColor", baseCol);
            mat.SetColor("_EdgeColor", edge);
            mat.SetColor("_CornerColor", corner);
            mat.SetFloat("_EdgeWidth", 0.045f);
            mat.SetFloat("_CornerWidth", 0.085f);
            mat.SetFloat("_GlowIntensity", glow);
            mat.SetFloat("_CornerIntensity", cornerGlow);
            mat.SetVector("_ObjectScale", new Vector4(size.x, size.y, size.z, 0f));
            mat.SetFloat("_Smoothness", 0.12f);

            AssetDatabase.CreateAsset(mat, MaterialFolder + "/Ember_" + preset + "_" + size.x + "x" + size.y + "x" + size.z + ".mat");
            materials[key] = mat;
            return mat;
        }

        static GameObject CubeUnder(Transform parent, string name, Vector3 pos, Vector3 size, string preset, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(preset, size);
            return go;
        }

        static Transform Area(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        static Light FireLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity * 1.9f;     // night fire reads far dimmer than the numbers suggest
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.75f;
            l.bounceIntensity = 0.6f;
            return l;
        }

        /// Chest-high block. In a Mirror's Edge style course the obstacle has to READ as a move:
        /// this one says "tuck or vault". 1.0m tall so the 1.30m jump apex clears it with margin.
        static void Tuck(Transform parent, string name, Vector3 surfaceCentre, float yaw)
        {
            CubeUnder(parent, name, surfaceCentre + new Vector3(0f, 0.5f, 0f),
                      new Vector3(2.6f, 1.0f, 1.2f), "deep", new Vector3(0f, yaw, 0f));
        }

        /// A low bar with exactly 1.0m of clearance, which is what the slide capsule (0.85m) needs.
        /// The frame reads as "slide under", and the posts stop the player going around it.
        static void SlideGate(Transform parent, string name, Vector3 surfaceCentre, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rot * Vector3.right;
            CubeUnder(parent, name + "_L", surfaceCentre + right * -2f + Vector3.up * 2f, S_PILLAR_S, "deep", new Vector3(0f, yaw, 0f));
            CubeUnder(parent, name + "_R", surfaceCentre + right * 2f + Vector3.up * 2f, S_PILLAR_S, "deep", new Vector3(0f, yaw, 0f));
            CubeUnder(parent, name + "_Bar", surfaceCentre + Vector3.up * 1.2f, new Vector3(4.4f, 0.4f, 0.6f), "deep", new Vector3(0f, yaw, 0f));
        }

        /// Wide pad used as a landing / run-out. Drops land into these: rolling to keep momentum
        /// only works if there is open ground in front of you.
        static void RunOut(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            CubeUnder(parent, name, pos, size, "ember", Vector3.zero);
        }

        /// A long slab spanning a to b. Because it follows its endpoints it doubles as a ramp - put b
        /// higher than a and you get a runnable slope. This is how the route gains height, rather than
        /// asking the player to hop up a staircase of small ledges.
        static GameObject Walkway(Transform parent, string name, Vector3 a, Vector3 b, float width, string preset)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var size = new Vector3(width, WalkThick, len);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(preset, size);
            return go;
        }

        /// Surface point of a walkway at fraction t - use to stand obstacles on a sloped walkway.
        static Vector3 OnWalk(Vector3 a, Vector3 b, float t)
        {
            return Vector3.Lerp(a, b, t) + Vector3.up * (WalkThick * 0.5f);
        }

        /// Layer that wall-run walls live on. The wall-run probe only looks at this layer, so random
        /// geometry elsewhere in the level does not trigger a run.
        public const int WallRunLayer = 6;

        /// Wall-run line: a tall slab beside the route with a lit cap. Placed along a gap so the fast
        /// line is to run the wall, while the slow line is to jump or take the lower ledge.
        static void RunWall(Transform parent, string name, Vector3 a, Vector3 b, float height, string preset)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = WallRunLayer;                 // only these walls are runnable
            go.transform.SetParent(parent, false);
            go.transform.position = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var size = new Vector3(0.6f, height, len);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(preset, size);

            // the cap stays on Default - it is trim, not a surface you should be able to run
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = name + "_Cap";
            cap.transform.SetParent(parent, false);
            cap.transform.position = (a + b) * 0.5f + Vector3.up * (height + 0.15f);
            cap.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var capSize = new Vector3(1.0f, 0.28f, len);
            cap.transform.localScale = capSize;
            cap.GetComponent<Renderer>().sharedMaterial = MaterialFor("ember", capSize);
        }

        /// Scattered run of platforms from a to b. `spacing` is the EDGE-to-edge gap, which is what
        /// matters for jumping - centre spacing has to account for the platform's own width, or the
        /// platforms overlap and the run becomes a continuous walkway instead of a jump sequence.
        /// Arced upward and offset side to side so it never reads as a straight corridor.
        static void StoneRun(Transform parent, string name, Vector3 a, Vector3 b, float spacing, Vector3 size, float lateral, string preset)
        {
            float dist = Vector3.Distance(a, b);
            float centreSpacing = size.x + spacing;

            // platforms alternate side to side, so the straight-line distance between neighbours is
            // longer than the distance along the run. Solve for the along-run step that still lands
            // the real 3D gap on the requested value, or the zigzag quietly makes jumps too far.
            float lateralSpread = 2f * lateral;
            float alongStep = Mathf.Sqrt(Mathf.Max(1f, centreSpacing * centreSpacing - lateralSpread * lateralSpread));
            int steps = Mathf.Max(1, Mathf.RoundToInt(dist / Mathf.Max(1.5f, alongStep)) - 1);
            Vector3 dir = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)(steps + 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p += right * ((i % 2 == 0) ? lateral : -lateral) * (0.6f + 0.4f * Mathf.Sin(i * 1.7f));
                p.y += Mathf.Sin(t * Mathf.PI) * 1.0f;
                CubeUnder(parent, name + "_" + i, p, size, preset, new Vector3(0f, i * 11f, 0f));
            }
        }

        /// Cover placed around a radius, never in the middle - the arena centre stays open to fight in.
        static void RingOfCover(Transform parent, string name, Vector3 centre, float radius, int count, float y)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f + Mathf.Sin(i * 3.1f) * 0.35f;
                float r = radius + Mathf.Sin(i * 2.3f) * 1.8f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                bool wide = (i % 3) == 0;
                CubeUnder(parent, name + "_" + i, p, wide ? S_COVER_W : S_COVER, "deep", new Vector3(0f, i * 37f, 0f));
            }
        }

        static GameObject Prop(Transform parent, string name, GameObject source, Vector3 pos, float scale, Vector3 euler)
        {
            if (source == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ==================================================================== world setup

        static void SetNightEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.10f, 1f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.09f, 0.045f, 0.03f, 1f);
            RenderSettings.fogDensity = 0.011f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetColor("_SkyTint", new Color(0.05f, 0.06f, 0.10f, 1f));
                sky.SetColor("_GroundColor", new Color(0.03f, 0.025f, 0.03f, 1f));
                sky.SetFloat("_Exposure", 0.35f);
                sky.SetFloat("_SunDisk", 0f);
                sky.SetFloat("_AtmosphereThickness", 0.7f);
                AssetDatabase.CreateAsset(sky, MaterialFolder + "/NightSky.mat");
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();

            var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp != null)
            {
                var rpso = new SerializedObject(rp);
                var atlas = rpso.FindProperty("m_AdditionalLightsShadowmapResolution");
                if (atlas != null) { atlas.intValue = 4096; rpso.ApplyModifiedProperties(); EditorUtility.SetDirty(rp); }
            }

            var moon = new GameObject("Moon");
            moon.transform.SetParent(root, false);
            moon.transform.rotation = Quaternion.Euler(28f, 205f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.42f, 0.52f, 0.80f, 1f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            light.shadowBias = 0.08f;
            RenderSettings.sun = light;
        }

        // ==================================================================== the route

        static void BuildRoute()
        {
            // ---------- 1. start pad, heading +Z
            var s1 = Area("S01_Start");
            CubeUnder(s1, "StartPad", new Vector3(0f, 0f, -40f), S_DECK, "ember", Vector3.zero);

            var startPoint = new GameObject("StartPoint");
            startPoint.transform.SetParent(s1, false);
            startPoint.transform.position = new Vector3(0f, 0.4f, -40f);
            var respawn = s1.Find("StartPad").gameObject.AddComponent<LevelRespawn>();
            var rso = new SerializedObject(respawn);
            rso.FindProperty("mode").enumValueIndex = 1;
            rso.FindProperty("claimOnStart").boolValue = true;
            rso.FindProperty("startPoint").objectReferenceValue = startPoint.transform;
            rso.ApplyModifiedProperties();

            CubeUnder(s1, "Step1", new Vector3(-1.6f, 0.3f, -35.5f), S_STEP, "ember", Vector3.zero);
            CubeUnder(s1, "Step2", new Vector3(1.2f, 0.9f, -33.5f), S_STEP, "ember", Vector3.zero);
            CubeUnder(s1, "Step3", new Vector3(-0.9f, 1.5f, -31.5f), S_STEP, "ember", Vector3.zero);

            // slide gate: 1.0m of clearance under the bar
            var landingY = 2.4f;
            CubeUnder(s1, "GatePad", new Vector3(0f, landingY, -29f), S_PLAT, "ember", Vector3.zero);
            CubeUnder(s1, "GatePostL", new Vector3(-1.7f, 3.6f, -29f), S_PILLAR_S, "deep", Vector3.zero);
            CubeUnder(s1, "GatePostR", new Vector3(1.7f, 3.6f, -29f), S_PILLAR_S, "deep", Vector3.zero);
            CubeUnder(s1, "GateBar", new Vector3(0f, 3.55f, -29f), new Vector3(3.8f, 0.4f, 0.6f), "deep", Vector3.zero);
            FireLight(s1, "GateLight", new Vector3(0f, 4.6f, -29f), new Color(1f, 0.36f, 0.08f), 3.2f, 12f, false);

            // checkpoint after the gate
            Checkpoint(s1, "CP_Start", new Vector3(0f, 2.65f, -29f));

            // ---------- 2. the long run. A wide RISING WALKWAY with obstacles standing on it - not a
            //             chain of islands. The player should be able to hold sprint the whole way.
            var s2 = Area("S02_LongRun");

            Vector3 runA = new Vector3(0f, 0.3f, -30f);
            Vector3 runB = new Vector3(0f, 3.2f, -14f);
            Walkway(s2, "Runway", runA, runB, WalkWidth, "ember");

            Tuck(s2, "TuckA", OnWalk(runA, runB, 0.28f), 0f);         // vault without breaking stride
            SlideGate(s2, "SlideA", OnWalk(runA, runB, 0.55f), 0f);   // then slide under
            Tuck(s2, "TuckB", OnWalk(runA, runB, 0.8f), 0f);          // then vault again

            // ---------- 3. turn right -> +X. First set-piece: a 6m gap (past the 5.2m jump) with a
            //             wall-run line along it, and a lower ledge line as the safe route.
            var s3 = Area("S03_TurnEast");

            Vector3 landA = new Vector3(2f, 3.4f, -11f);
            RunOut(s3, "TurnLanding", landA, S_LANDING);
            Tuck(s3, "TuckTurn", landA + Vector3.up * 0.25f, 0f);

            Vector3 landB = new Vector3(18f, 4f, -10f);
            RunOut(s3, "TurnLanding2", landB, S_LANDING);

            // base sits below the landing surfaces so the wall rises out of the ground, not out of thin air
            RunWall(s3, "TurnWall", new Vector3(5f, 3f, -13.5f), new Vector3(17f, 3f, -13.5f), 6f, "deep");

            // safe alternative: two low ledges you can hop across instead of committing to the gap
            CubeUnder(s3, "LowerLedge_0", new Vector3(7f, 3.4f, -10f), S_LEDGE_LONG, "ember", Vector3.zero);
            CubeUnder(s3, "LowerLedge_1", new Vector3(11f, 3.4f, -10f), S_LEDGE_LONG, "ember", Vector3.zero);

            // wide walkway east into the arena
            Vector3 eastA = new Vector3(24f, 4.4f, -9f);
            Vector3 eastB = new Vector3(33f, 5.6f, -8f);
            Walkway(s3, "EastWalk", eastA, eastB, WalkWidth, "ember");
            SlideGate(s3, "SlideEast", OnWalk(eastA, eastB, 0.5f), 0f);

            // ---------- 4. ARENA 1 - Cinder Yard (34x34)
            var s4 = Area("S04_CinderYard");
            var a1 = new Vector3(46f, 6f, -6f);
            CubeUnder(s4, "YardFloor", a1, S_ARENA_A, "ember", Vector3.zero);
            CubeUnder(s4, "YardPlinth", a1 + new Vector3(0f, 1.2f, 0f), S_PLAT_S, "ember", new Vector3(0f, 22f, 0f));
            RingOfCover(s4, "YardCover", a1, 12.5f, 14, 0.5f);
            FireLight(s4, "YardLightA", a1 + new Vector3(-11f, 3.4f, -10f), new Color(1f, 0.30f, 0.05f), 6f, 20f, false);
            FireLight(s4, "YardLightB", a1 + new Vector3(11.5f, 3.2f, 10.5f), new Color(1f, 0.48f, 0.16f), 4.5f, 17f, false);
            FireLight(s4, "YardLightC", a1 + new Vector3(-11f, 3.2f, 11f), new Color(1f, 0.34f, 0.08f), 4f, 16f, false);
            Checkpoint(s4, "CP_Yard", a1 + new Vector3(-13f, 1.3f, 0f));

            // ---------- 5 & 6. turn left -> +Z, through the wall-run rift
            var s5 = Area("S05_Rift");
            CubeUnder(s5, "RiftEntry", new Vector3(49.8f, 7f, 13f), S_PLAT_S, "ember", Vector3.zero);

            // Corridor A: 3.5m apart, 6m tall, 26m long, with a ~9m break that cannot be jumped
            CubeUnder(s5, "WallA_West", new Vector3(48f, 11f, 26f), S_WALL_LONG, "deep", Vector3.zero);
            CubeUnder(s5, "WallA_East", new Vector3(51.5f, 11f, 26f), S_WALL_LONG, "deep", Vector3.zero);
            CubeUnder(s5, "WallACapW", new Vector3(48f, 14.05f, 26f), new Vector3(0.9f, 0.25f, 26f), "ember", Vector3.zero);
            CubeUnder(s5, "WallACapE", new Vector3(51.5f, 14.05f, 26f), new Vector3(0.9f, 0.25f, 26f), "ember", Vector3.zero);
            CubeUnder(s5, "RunA_In", new Vector3(49.75f, 8.15f, 17f), S_LEDGE_LONG, "ember", Vector3.zero);
            CubeUnder(s5, "RunA_Out", new Vector3(49.75f, 8.15f, 33f), S_LEDGE_LONG, "ember", Vector3.zero);

            // over-the-top detour keeps the level completable before the wall-run system exists
            StoneRun(s5, "Detour", new Vector3(54.5f, 8.6f, 15f), new Vector3(54.5f, 8.6f, 35f), 3.4f, S_PLAT_S, 1.9f, "ember");
            FireLight(s5, "RiftLight", new Vector3(49.75f, 9.4f, 17f), new Color(1f, 0.30f, 0.05f), 3.6f, 12f, false);
            Checkpoint(s5, "CP_Rift", new Vector3(49.75f, 8.5f, 33f));

            // Corridor B: practice run
            CubeUnder(s5, "WallB_N", new Vector3(58f, 9f, 20f), S_WALL_S, "deep", Vector3.zero);
            CubeUnder(s5, "WallB_S", new Vector3(61f, 9f, 20f), S_WALL_S, "deep", Vector3.zero);
            CubeUnder(s5, "RunB", new Vector3(59.5f, 7.15f, 20f), new Vector3(3f, 0.3f, 7f), "ember", Vector3.zero);
            CubeUnder(s5, "EntryB", new Vector3(59.5f, 6.3f, 14f), S_PLAT_S, "ember", Vector3.zero);

            // ---------- 7. turn left -> -X. TWO full routes through the same space, Mirror's Edge
            //             style: a wide low walkway, or a higher roof line with a wall-run gap.
            var s6 = Area("S06_RiseWest");

            // LOW route - one continuous wide walkway, always completable
            Vector3 lowA = new Vector3(47f, 8.8f, 36f);
            Vector3 lowB = new Vector3(23f, 11.5f, 35f);
            Walkway(s6, "LowWalk", lowA, lowB, WalkWidth, "ember");
            Tuck(s6, "TuckG", OnWalk(lowA, lowB, 0.25f), 0f);
            SlideGate(s6, "SlideH", OnWalk(lowA, lowB, 0.52f), 0f);
            Tuck(s6, "TuckI", OnWalk(lowA, lowB, 0.78f), 0f);

            // HIGH route - ramp up onto a roof deck, then a gap that is past jump range, so the fast
            // line is to wall-run it. Optional; the low walkway is the fallback.
            Vector3 highDeck = new Vector3(40f, 13f, 28f);
            RunOut(s6, "HighDeck", highDeck, S_DECK_MID);
            Walkway(s6, "HighRamp", new Vector3(45f, 11.4f, 33f), new Vector3(48f, 13.3f, 30f), 6f, "ember");
            RunWall(s6, "HighWall", new Vector3(44f, 13.3f, 24f), new Vector3(28f, 13.3f, 24f), 6f, "deep");
            RunOut(s6, "HighDeck2", new Vector3(22f, 13.8f, 26f), S_LANDING);
            FireLight(s6, "HighLight", highDeck + new Vector3(0f, 3f, 0f), new Color(1f, 0.30f, 0.05f), 4.5f, 20f, false);

            // ---------- 8. ARENA 2 - Ember Arena (40x40), the largest
            var s7 = Area("S07_EmberArena");
            var a2 = new Vector3(2f, 13f, 30f);
            CubeUnder(s7, "ArenaFloor", a2, S_ARENA_B, "ember", Vector3.zero);
            CubeUnder(s7, "ArenaPlinth", a2 + new Vector3(0f, 1.2f, 0f), S_PLAT, "ember", new Vector3(0f, 15f, 0f));
            RingOfCover(s7, "ArenaCover", a2, 15f, 20, 0.5f);
            FireLight(s7, "ArenaLightA", a2 + new Vector3(-13f, 4f, -13f), new Color(1f, 0.32f, 0.06f), 7f, 24f, true);
            FireLight(s7, "ArenaLightB", a2 + new Vector3(13.5f, 3.8f, 12.5f), new Color(1f, 0.50f, 0.18f), 5f, 20f, false);
            FireLight(s7, "ArenaLightC", a2 + new Vector3(0f, 4.5f, 0f), new Color(1f, 0.26f, 0.05f), 4f, 16f, false);
            Checkpoint(s7, "CP_Arena", new Vector3(-14f, 13.4f, 34f));

            // ---------- 9. turn -> -Z. Wide rising walkway with obstacles on it.
            var s8 = Area("S08_Rise");

            Vector3 riseA = new Vector3(-13f, 13.4f, 13f);
            Vector3 riseB = new Vector3(-22f, 16f, -3f);
            Walkway(s8, "RiseWalk", riseA, riseB, WalkWidth, "ember");
            Tuck(s8, "TuckK", OnWalk(riseA, riseB, 0.3f), 0f);
            SlideGate(s8, "SlideL", OnWalk(riseA, riseB, 0.62f), 0f);

            // ---------- 10. turn -> +X. Wide walkway (the route) with narrow beams overhead as a
            //              visible high line. The walkway is the free-running path; the beams read as
            //              an optional precision line once the movement set supports it.
            var s9 = Area("S09_Beams");

            Vector3 beamA = new Vector3(-22f, 16.1f, -4f);
            Vector3 beamB = new Vector3(-1f, 18f, -4f);
            Walkway(s9, "HighWalk", beamA, beamB, WalkWidth, "ember");
            Tuck(s9, "TuckBeam", OnWalk(beamA, beamB, 0.32f), 0f);
            SlideGate(s9, "SlideBeam", OnWalk(beamA, beamB, 0.68f), 0f);

            // overhead beam line - set dressing that frames the walkway and hints at a harder route
            for (int i = 0; i < 4; i++)
                CubeUnder(s9, "Beam_" + i, new Vector3(-17f + i * 5.4f, 20.4f + i * 0.4f, -7.5f), S_BEAM, "ember", new Vector3(0f, 90f, 0f));

            // wall-run line beside the walkway, for the fast version of this stretch
            RunWall(s9, "BeamWall", new Vector3(-19f, 17f, -8.5f), new Vector3(-3f, 17f, -8.5f), 5f, "deep");

            FireLight(s9, "BeamLight", new Vector3(-11f, 19.6f, -5f), new Color(1f, 0.40f, 0.11f), 3.4f, 16f, false);
            Checkpoint(s9, "CP_Beams", new Vector3(-7f, 18.4f, -4f));

            // ---------- 11. ARENA 3 - Kiln Hollow (30x30), the final fight
            var s10 = Area("S10_KilnHollow");
            var a3 = new Vector3(16f, 20f, -6f);
            CubeUnder(s10, "HollowFloor", a3, S_ARENA_C, "ember", Vector3.zero);
            RingOfCover(s10, "HollowCover", a3, 11f, 16, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                CubeUnder(s10, "HollowPillar_" + i, a3 + new Vector3(Mathf.Cos(ang) * 13.5f, 4f, Mathf.Sin(ang) * 13.5f), S_PILLAR_S, "deep", Vector3.zero);
            }
            CubeUnder(s10, "HollowBrazier", a3 + new Vector3(0f, 0.8f, 9f), S_BRAZIER, "hot", new Vector3(0f, 12f, 0f));
            FireLight(s10, "HollowLight", a3 + new Vector3(0f, 3.2f, 9f), new Color(1f, 0.30f, 0.05f), 7f, 26f, true);
            FireLight(s10, "HollowLightB", a3 + new Vector3(-11f, 3.4f, -9f), new Color(1f, 0.26f, 0.05f), 4f, 16f, false);
            Checkpoint(s10, "CP_Hollow", new Vector3(a3.x - 11f, 20.4f, a3.z + 3f));

            // ---------- 12. exit south to the finish
            var s11 = Area("S11_Finish");
            StoneRun(s11, "ExitRun", new Vector3(16f, 20f, -20f), new Vector3(16f, 20.6f, -25f), 3.2f, S_PLAT_S, 0f, "ember");
            CubeUnder(s11, "FinishPad", new Vector3(16f, 20.6f, -29f), S_PAD, "ember", Vector3.zero);
            CubeUnder(s11, "Beacon", new Vector3(19f, 25.9f, -29f), S_BEACON, "hot", Vector3.zero);
            CubeUnder(s11, "BeaconCap", new Vector3(19f, 31.2f, -29f), S_BRAZIER, "hot", new Vector3(0f, 45f, 0f));
            FireLight(s11, "BeaconLight", new Vector3(19f, 27f, -29f), new Color(1f, 0.45f, 0.12f), 6f, 26f, false);

            var goal = new GameObject("GoalPad");
            goal.transform.SetParent(s11, false);
            goal.transform.position = new Vector3(16f, 21.4f, -29f);
            var goalBox = goal.AddComponent<BoxCollider>();
            goalBox.isTrigger = true;
            goalBox.size = new Vector3(5f, 3f, 5f);
            var goalScript = goal.AddComponent<LevelGoal>();
            var gso = new SerializedObject(goalScript);
            gso.FindProperty("beaconRenderer").objectReferenceValue = s11.Find("Beacon").GetComponent<Renderer>();
            gso.ApplyModifiedProperties();
        }

        static void Checkpoint(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 4f, 6f);
            var cp = go.AddComponent<LevelRespawn>();
            var so = new SerializedObject(cp);
            so.FindProperty("mode").enumValueIndex = 1;      // Checkpoint
            so.ApplyModifiedProperties();
        }

        // ==================================================================== decoration

        static void BuildTornadoes()
        {
            var a = Area("Props_Tornadoes");
            if (tornadoSource == null) return;

            var huge = Prop(a, "Tornado_Huge", tornadoSource, new Vector3(-70f, 2f, 60f), 3.0f, Vector3.zero);
            var medium = Prop(a, "Tornado_Medium", tornadoSource, new Vector3(72f, 2f, 8f), 1.6f, Vector3.zero);
            var small = Prop(a, "Tornado_Small", tornadoSource, new Vector3(68f, 8f, 26f), 0.9f, Vector3.zero);

            foreach (var t in new GameObject[] { huge, medium, small })
            {
                if (t == null) continue;
                t.AddComponent<SlowSpin>();
                FireLight(t.transform, t.name + "_Light", t.transform.position + Vector3.up * 3f,
                          new Color(1f, 0.42f, 0.10f), 5f, 34f, false);
            }
        }

        static void BuildTorches()
        {
            var a = Area("Props_Torches");
            if (torchSource == null) return;

            for (int i = 0; i < TorchSpots.Length; i++)
            {
                var p = TorchSpots[i];
                var torch = Prop(a, "Torch_" + i, torchSource, p, 1f, new Vector3(0f, i * 47f, 0f));
                if (torch == null) continue;
                FireLight(torch.transform, "TorchLight_" + i, p + Vector3.up * 1.5f,
                          new Color(1f, 0.52f, 0.20f), 3.2f, 13f, false);
            }
        }

        static void BuildAtmosphere()
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(8f, 12f, -4f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.55f;
            main.startSize = 0.14f;
            main.maxParticles = 420;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.012f;
            main.startColor = new Color(1f, 0.55f, 0.22f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(140f, 34f, 110f);      // spans the whole route

            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.75f, 0.35f), 0f),
                    new GradientColorKey(new Color(1f, 0.38f, 0.08f), 0.45f),
                    new GradientColorKey(new Color(0.55f, 0.10f, 0.03f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0.55f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            var particleMat = new Material(Shader.Find(ParticleShader));
            AssetDatabase.CreateAsset(particleMat, MaterialFolder + "/EmberParticle.mat");
            pr.material = particleMat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sortingFudge = -1f;
        }

        static void BuildAudio()
        {
            const string musicPath = "Assets/Audio/Y2Mate.is - Parkour Theme - No Copyright Music.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
            if (clip == null)
            {
                Debug.LogWarning("[EmberAscent] Background music not found at " + musicPath);
                return;
            }

            var go = new GameObject("Music");
            go.transform.SetParent(root, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.volume = 0.35f;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
        }

        static void BuildPlayerAndCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 700f;
            camGo.transform.position = new Vector3(0f, 2.4f, -45f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab == null)
            {
                Debug.LogError("[EmberAscent] Player prefab not found at " + PlayerPrefab);
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, 0.4f, -40f);
            player.transform.rotation = Quaternion.identity;

            var controller = player.transform.Find("States").GetComponent("CharacterStateController");
            var cso = new SerializedObject(controller);
            cso.FindProperty("movementReferenceParameters.externalReference").objectReferenceValue = camGo.transform;
            cso.ApplyModifiedProperties();

            var orbit = camGo.AddComponent<ThirdPersonOrbitCamera>();
            var oso = new SerializedObject(orbit);
            oso.FindProperty("target").objectReferenceValue = player.transform;
            oso.FindProperty("actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputAsset);
            oso.ApplyModifiedProperties();

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.weight = 1f;
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(ProfilePath);
            if (profile != null) volume.sharedProfile = profile;

            var fall = new GameObject("FallRespawnVolume");
            fall.transform.position = new Vector3(10f, -18f, -4f);
            var fallBox = fall.AddComponent<BoxCollider>();
            fallBox.isTrigger = true;
            fallBox.size = new Vector3(320f, 8f, 260f);
            var fallScript = fall.AddComponent<LevelRespawn>();
            var fso = new SerializedObject(fallScript);
            fso.FindProperty("mode").enumValueIndex = 0;
            fso.ApplyModifiedProperties();
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath))
            {
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }
    }
}
