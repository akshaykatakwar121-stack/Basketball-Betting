using System.Collections.Generic;
using System.IO;
using BasketballBetting.Cameras;
using BasketballBetting.Shooting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BasketballBetting.EditorTools
{
    /// <summary>
    /// Builds the whole game scene from code: court, goals (with the physical Hoop), batched stands and
    /// crowd, arena envelope, lighting rig, reflection probe, camera, ball and the game host object.
    /// Every generated mesh/material/texture is saved under Assets/BasketballBetting/Art so the scene is
    /// self-contained and the old per-renderer material soup is gone.
    ///
    /// Menu: Basketball Betting / Build Arena Scene.  Batch: -executeMethod BasketballBetting.EditorTools.ArenaSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class ArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string ArtRoot = "Assets/BasketballBetting/Art";
        const string TexRoot = ArtRoot + "/Textures";
        const string MatRoot = ArtRoot + "/Materials";
        const string MeshRoot = ArtRoot + "/Meshes";
        const string SettingsRoot = "Assets/BasketballBetting/Settings";
        const string LegacyGenerated = "Assets/BasketballBetting/Generated";

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        const string PREF_SYMMETRICAL_COURT = "Hoops_SymmetricalCourt";
        
        public static bool UseSymmetricalCourt
        {
            get => EditorPrefs.GetBool(PREF_SYMMETRICAL_COURT, true);
            set => EditorPrefs.SetBool(PREF_SYMMETRICAL_COURT, value);
        }

        [MenuItem("Basketball Betting/Court/Use Symmetrical Floor (Fixed)")]
        public static void SetSymmetricalCourt()
        {
            UseSymmetricalCourt = true;
            Build(true);
            EditorUtility.DisplayDialog("Court Updated", "Rebuilt arena using the Fixed Symmetrical Court.", "OK");
        }

        [MenuItem("Basketball Betting/Court/Use Original Floor (Asymmetrical)")]
        public static void SetOriginalCourt()
        {
            UseSymmetricalCourt = false;
            Build(true);
            EditorUtility.DisplayDialog("Court Updated", "Rebuilt arena using the Original Asymmetrical Court.", "OK");
        }

        [MenuItem("Basketball Betting/Build Arena Scene (New Voxel Crowd)")]
        public static void BuildMenuVoxel()
        {
            Build(true);
            EditorUtility.DisplayDialog("Arena built", "SampleScene was rebuilt with the New Voxel Crowd.", "OK");
        }

        [MenuItem("Basketball Betting/Build Arena Scene (Revert to Egg Crowd)")]
        public static void BuildMenuClassic()
        {
            Build(false);
            EditorUtility.DisplayDialog("Arena built", "SampleScene was rebuilt with the Classic Egg Crowd.", "OK");
        }

        public static void BuildFromCommandLine()
        {
            Build(true);
        }

        public static void Build(bool useVoxelCrowd = true)
        {
            ConfigureTextureImporters();
            EnsureFolders();
            Mats.Clear();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var arenaGo = new GameObject("Arena");
            var arena = arenaGo.AddComponent<ArenaSceneRoot>();
            Transform arenaT = arenaGo.transform;

            Transform court = Child(arenaT, "Court");
            BuildCourt(court);
            arena.CourtRoot = court;

            Transform goals = Child(arenaT, "Goals");
            Hoop playGoal = BuildGoal(goals, true, out HoopNet playNet);
            BuildGoal(goals, false, out _);
            arena.PlayGoal = playGoal;
            arena.PlayNet = playNet;

            Transform stands = Child(arenaT, "Stands");
            stands.gameObject.AddComponent<CrowdAnimator>();
            BuildStands(stands, useVoxelCrowd);
            arena.CrowdRoot = stands;

            Transform envelope = Child(arenaT, "Envelope");
            BuildEnvelope(envelope);

            Transform lighting = Child(arenaT, "Lighting");
            BuildLighting(lighting);
            arena.LightingRoot = lighting;

            arena.ActorsRoot = Child(arenaT, "Actors");

            Basketball ball = Basketball.Create(arenaT, Mat("Basketball"));
            ball.name = "Basketball";
            ball.Teleport(new Vector3(0.35f, 0.12f, CourtMetrics.FreeThrowZ - 0.6f));
            arena.Ball = ball;

            BuildCamera();
            BuildVolume();
            BuildGameHost();
            ApplyRenderSettings();
            MarkStatic(arenaT);

            if (AssetDatabase.IsValidFolder(LegacyGenerated))
                AssetDatabase.DeleteAsset(LegacyGenerated);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArenaSceneBuilder] Scene built and saved to " + ScenePath);
        }

        // ------------------------------------------------------------------ court

        static void BuildCourt(Transform parent)
        {
            var floor = new GameObject("CourtFloor");
            floor.transform.SetParent(parent, false);
            floor.layer = GameLayers.Arena;
            var mf = floor.AddComponent<MeshFilter>();
            mf.sharedMesh = SaveMesh(QuadMesh(CourtMetrics.Width, CourtMetrics.Length, "CourtFloor"), "CourtFloor");
            var mr = floor.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat("CourtFloor");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            var apron = new GameObject("ArenaFloor");
            apron.transform.SetParent(parent, false);
            apron.transform.position = new Vector3(0f, -0.004f, 0f);
            apron.layer = GameLayers.Arena;
            apron.AddComponent<MeshFilter>().sharedMesh = SaveMesh(QuadMesh(70f, 80f, "ArenaFloor"), "ArenaFloor");
            var ar = apron.AddComponent<MeshRenderer>();
            ar.sharedMaterial = Mat("ArenaFloor");
            ar.shadowCastingMode = ShadowCastingMode.Off;

            var collider = new GameObject("PlayFloor");
            collider.transform.SetParent(parent, false);
            collider.layer = GameLayers.Court;
            var box = collider.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.25f, 0f);
            box.size = new Vector3(70f, 0.5f, 80f);
            box.sharedMaterial = GamePhysicsMaterials.Floor;
        }

        // ------------------------------------------------------------------ goals

        static Hoop BuildGoal(Transform parent, bool playEnd, out HoopNet net)
        {
            float sign = playEnd ? 1f : -1f;
            var root = new GameObject(playEnd ? "GoalPlay" : "GoalFar");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, CourtMetrics.RimHeight, sign * (CourtMetrics.BackboardZ - CourtMetrics.RimFromBackboard));
            root.transform.rotation = playEnd ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            Transform t = root.transform;
            float rimToGlass = CourtMetrics.RimFromBackboard - CourtMetrics.BackboardThickness * 0.5f; // 0.375
            float glassCenterY = -0.15f + CourtMetrics.BackboardHeight * 0.5f;

            // rim iron
            var rim = Visual(t, "RimIron", SaveMesh(TorusMeshBuilder.Build(CourtMetrics.RimRadius + 0.009f, 0.009f, 64, 20, "RimIron"), "RimIron"), Mat("RimIron"));
            rim.transform.localPosition = Vector3.zero;
            // bracket + plate
            var bracket = new MeshBatch();
            bracket.AddBox(new Vector3(0f, -0.018f, CourtMetrics.RimRadius + 0.075f), new Vector3(0.11f, 0.028f, 0.15f), 0);
            bracket.AddBox(new Vector3(0f, -0.06f, rimToGlass - 0.012f), new Vector3(0.14f, 0.13f, 0.024f), 0);
            bracket.AddBox(new Vector3(-0.045f, -0.06f, CourtMetrics.RimRadius + 0.08f), new Vector3(0.018f, 0.07f, 0.13f), 0);
            bracket.AddBox(new Vector3(0.045f, -0.06f, CourtMetrics.RimRadius + 0.08f), new Vector3(0.018f, 0.07f, 0.13f), 0);
            Visual(t, "RimBracket", SaveMesh(bracket.ToMesh("RimBracket"), (playEnd ? "Play" : "Far") + "RimBracket"), Mat("Steel"));

            // backboard glass (both faces) + frame
            var glass = new MeshBatch();
            Vector3 gc = new Vector3(0f, glassCenterY, rimToGlass);
            glass.AddQuad(gc, Vector3.right * (CourtMetrics.BackboardWidth * 0.5f), Vector3.up * (CourtMetrics.BackboardHeight * 0.5f), Vector3.back, 0);
            glass.AddQuad(gc + Vector3.forward * CourtMetrics.BackboardThickness, Vector3.right * (CourtMetrics.BackboardWidth * 0.5f), Vector3.up * (CourtMetrics.BackboardHeight * 0.5f), Vector3.forward, 0);
            var glassGo = Visual(t, "Backboard", SaveMesh(glass.ToMesh("Backboard"), (playEnd ? "Play" : "Far") + "Backboard"), Mat("BackboardGlass"));
            glassGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var frame = new MeshBatch();
            float fw = CourtMetrics.BackboardWidth, fh = CourtMetrics.BackboardHeight, fz = rimToGlass + CourtMetrics.BackboardThickness * 0.5f;
            frame.AddBox(new Vector3(0f, glassCenterY + fh * 0.5f, fz), new Vector3(fw + 0.06f, 0.06f, 0.09f), 0);
            frame.AddBox(new Vector3(0f, glassCenterY - fh * 0.5f, fz), new Vector3(fw + 0.06f, 0.06f, 0.09f), 0);
            frame.AddBox(new Vector3(-fw * 0.5f, glassCenterY, fz), new Vector3(0.06f, fh, 0.09f), 0);
            frame.AddBox(new Vector3(fw * 0.5f, glassCenterY, fz), new Vector3(0.06f, fh, 0.09f), 0);
            // rear support truss
            frame.AddBox(new Vector3(0f, glassCenterY + 0.15f, fz + 0.35f), new Vector3(0.12f, 0.12f, 0.7f), 0);
            frame.AddBox(new Vector3(0f, glassCenterY - 0.3f, fz + 0.35f), new Vector3(0.12f, 0.12f, 0.7f), 0);
            Visual(t, "BackboardFrame", SaveMesh(frame.ToMesh("BackboardFrame"), (playEnd ? "Play" : "Far") + "BackboardFrame"), Mat("Steel"));

            // stanchion: pole, arm, base, padding
            var stanchion = new MeshBatch();
            float poleZ = rimToGlass + 1.85f;
            float floorY = -CourtMetrics.RimHeight;
            stanchion.AddPrism(new Vector3(0f, floorY + 0.35f, poleZ), 0.13f, 2.45f, 12, 0);
            Vector3 armFrom = new Vector3(0f, floorY + 2.8f, poleZ);
            Vector3 armTo = new Vector3(0f, glassCenterY + 0.12f, fz + 0.7f);
            Vector3 armMid = (armFrom + armTo) * 0.5f;
            Quaternion armRot = Quaternion.LookRotation((armTo - armFrom).normalized, Vector3.up);
            stanchion.AddBox(armMid, new Vector3(0.16f, 0.16f, Vector3.Distance(armFrom, armTo)), armRot, 0);
            stanchion.AddBox(new Vector3(0f, floorY + 0.18f, poleZ + 0.45f), new Vector3(1.3f, 0.36f, 1.7f), 1);
            stanchion.AddBox(new Vector3(0f, floorY + 0.95f, poleZ - 0.02f), new Vector3(0.62f, 1.5f, 0.42f), 2);
            stanchion.AddBox(new Vector3(0f, floorY + 0.95f, poleZ + 0.65f), new Vector3(1.0f, 1.5f, 0.3f), 2);
            var stGo = Visual(t, "Stanchion", SaveMesh(stanchion.ToMesh("Stanchion"), (playEnd ? "Play" : "Far") + "Stanchion"),
                Mat("Steel"), Mat("BaseBlack"), Mat("Padding"));

            // net
            var netGo = new GameObject("Net");
            netGo.transform.SetParent(t, false);
            netGo.layer = GameLayers.Arena;
            net = netGo.AddComponent<HoopNet>();
            net.CordMaterial = Mat("NetCord");
            net.Build();

            if (!playEnd)
                return null;

            var hoop = root.AddComponent<Hoop>();
            hoop.Build();
            return hoop;
        }

        // ------------------------------------------------------------------ stands and crowd

        static void BuildStands(Transform parent, bool useVoxelCrowd)
        {
            var rng = new System.Random(2024);
            const int rows = 16;
            const float rowRise = 0.44f, rowDepth = 0.88f;
            float startN = CourtMetrics.HalfLength + 3.3f;
            float startW = CourtMetrics.HalfWidth + 3.6f;

            // side, axis along which seats run, direction away from court, start distance, base row width, widen per row
            (string name, Vector3 along, Vector3 away, float start, float width, float widen)[] sides =
            {
                ("North", Vector3.right, Vector3.forward, startN, CourtMetrics.Width + 9f, 1.5f),
                ("South", Vector3.right, Vector3.back, startN, CourtMetrics.Width + 9f, 1.5f),
                ("East", Vector3.forward, Vector3.right, startW, CourtMetrics.Length * 0.72f, 1.1f),
                ("West", Vector3.forward, Vector3.left, startW, CourtMetrics.Length * 0.72f, 1.1f),
            };

            Material[] seatMats = { Mat("SeatNavy"), Mat("SeatRed"), Mat("SeatCharcoal") };
            Material[] jerseyMats = { Mat("Crowd0"), Mat("Crowd1"), Mat("Crowd2"), Mat("Crowd3"), Mat("Crowd4"), Mat("Crowd5") };
            Material[] skinMats = { Mat("Skin0"), Mat("Skin1"), Mat("Skin2") };

            foreach (var side in sides)
            {
                var steps = new MeshBatch();
                var seats = new MeshBatch();
                const int CHUNKS_X = 8;
                const int CHUNKS_Y = 4;
                MeshBatch[,] crowdChunks = new MeshBatch[CHUNKS_X, CHUNKS_Y];
                for (int x = 0; x < CHUNKS_X; x++)
                for (int y = 0; y < CHUNKS_Y; y++)
                    crowdChunks[x, y] = new MeshBatch();
                for (int r = 0; r < rows; r++)
                {
                    bool concourse = r == 9; // walkway between lower and upper bowl
                    float dist = side.start + r * rowDepth + (r > 9 ? 1.6f : 0f);
                    float y = 0.3f + r * rowRise + (r > 9 ? 0.6f : 0f);
                    float width = side.width + r * side.widen;
                    Vector3 rowCenter = side.away * dist + Vector3.up * y;
                    Quaternion rot = Quaternion.LookRotation(-side.away, Vector3.up);
                    steps.AddBox(rowCenter - Vector3.up * (rowRise * 0.5f), rot * new Vector3(width, rowRise, rowDepth + 0.02f), rot, 0);
                    // riser face is darker: add a thin strip
                    if (concourse)
                        continue;
                    int seatCount = Mathf.FloorToInt(width / 0.56f);
                    int seatSub = r < 4 ? 1 : (r % 5 == 0 ? 2 : 0);
                    for (int i = 0; i < seatCount; i++)
                    {
                        int cx = Mathf.Clamp(Mathf.FloorToInt((float)i / seatCount * CHUNKS_X), 0, CHUNKS_X - 1);
                        int cy = Mathf.Clamp(Mathf.FloorToInt((float)r / rows * CHUNKS_Y), 0, CHUNKS_Y - 1);
                        MeshBatch crowd = crowdChunks[cx, cy];

                        float u = (i + 0.5f) / seatCount - 0.5f;
                        Vector3 sc = rowCenter + side.along * (u * width);
                        seats.AddBox(sc + Vector3.up * 0.20f - side.away * 0.12f, rot * new Vector3(0.48f, 0.08f, 0.44f), rot, seatSub);
                        seats.AddBox(sc + Vector3.up * 0.42f + side.away * 0.12f, rot * new Vector3(0.48f, 0.44f, 0.07f), rot, seatSub);
                        if (rng.NextDouble() < 0.62)
                        {
                            int jersey = rng.Next(jerseyMats.Length);
                            int skin = rng.Next(skinMats.Length);
                            
                            if (useVoxelCrowd)
                            {
                                float leanX = (float)(rng.NextDouble() - 0.5) * 0.1f;
                                float leanZ = (float)(rng.NextDouble() - 0.5) * 0.08f;
                                float heightVar = (float)(rng.NextDouble() - 0.5) * 0.15f;
                                float widthVar = 1f + (float)(rng.NextDouble() - 0.5) * 0.2f;

                                Vector3 bodyBase = sc + Vector3.up * 0.24f - side.away * (0.05f + leanZ) + side.along * leanX;
                                Quaternion charRot = Quaternion.LookRotation(-side.away, Vector3.up);

                                // Torso
                                Vector3 torsoSize = new Vector3(0.32f * widthVar, 0.40f + heightVar, 0.2f);
                                Vector3 torsoCenter = bodyBase + Vector3.up * (torsoSize.y * 0.5f);
                                crowd.AddBox(torsoCenter, torsoSize, charRot, jersey);

                                // Head
                                Vector3 headCenter = bodyBase + Vector3.up * (torsoSize.y + 0.12f);
                                crowd.AddBox(headCenter, new Vector3(0.16f, 0.18f, 0.18f), charRot, jerseyMats.Length + skin);

                                // Hair / Hat
                                if (rng.NextDouble() > 0.2)
                                {
                                    int hairMat = rng.NextDouble() > 0.5 ? jersey : (jerseyMats.Length + rng.Next(skinMats.Length));
                                    crowd.AddBox(headCenter + Vector3.up * 0.09f, new Vector3(0.18f, 0.06f, 0.20f), charRot, hairMat);
                                }

                                // Arms
                                Vector3 armSize = new Vector3(0.08f, torsoSize.y * 0.8f, 0.12f);
                                Vector3 leftArmPos = torsoCenter - charRot * new Vector3(torsoSize.x * 0.5f + armSize.x * 0.5f, 0, 0);
                                Vector3 rightArmPos = torsoCenter + charRot * new Vector3(torsoSize.x * 0.5f + armSize.x * 0.5f, 0, 0);
                                
                                leftArmPos.y -= 0.05f;
                                rightArmPos.y -= 0.05f;

                                bool shortSleeves = rng.NextDouble() > 0.5;
                                if (shortSleeves)
                                {
                                    Vector3 sleeveSize = new Vector3(0.085f, armSize.y * 0.4f, 0.125f);
                                    crowd.AddBox(leftArmPos + Vector3.up * (armSize.y * 0.3f), sleeveSize, charRot, jersey);
                                    crowd.AddBox(rightArmPos + Vector3.up * (armSize.y * 0.3f), sleeveSize, charRot, jersey);
                                }

                                crowd.AddBox(leftArmPos, armSize, charRot, jerseyMats.Length + skin);
                                crowd.AddBox(rightArmPos, armSize, charRot, jerseyMats.Length + skin);
                            }
                            else
                            {
                                float lean = (float)(rng.NextDouble() - 0.5) * 0.06f;
                                Vector3 bodyBase = sc + Vector3.up * 0.24f - side.away * 0.05f + side.along * lean;
                                crowd.AddPrism(bodyBase, 0.19f, 0.56f, 7, jersey, 0.85f);
                                crowd.AddSphere(bodyBase + Vector3.up * 0.7f, 0.115f, jerseyMats.Length + skin);
                            }
                        }
                    }
                }
                var stepsGo = Visual(parent, "Stand" + side.name, SaveMesh(steps.ToMesh("Stand" + side.name), "Stand" + side.name), Mat("Concrete"));
                stepsGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var seatsGo = Visual(parent, "Seats" + side.name, SaveMesh(seats.ToMesh("Seats" + side.name), "Seats" + side.name), seatMats);
                seatsGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var crowdMats = new Material[jerseyMats.Length + skinMats.Length];
                jerseyMats.CopyTo(crowdMats, 0);
                skinMats.CopyTo(crowdMats, jerseyMats.Length);
                
                for (int x = 0; x < CHUNKS_X; x++)
                {
                    for (int y = 0; y < CHUNKS_Y; y++)
                    {
                        if (crowdChunks[x, y].VertexCount == 0) continue;
                        string chunkName = "Crowd" + side.name + "_" + x + "_" + y;
                        var crowdGo = Visual(parent, chunkName, SaveMesh(crowdChunks[x, y].ToMesh(chunkName), chunkName), crowdMats);
                        crowdGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ envelope

        static void BuildEnvelope(Transform parent)
        {
            float wallDist = 34f;
            var walls = new MeshBatch();
            walls.AddBox(new Vector3(0f, 9f, wallDist), new Vector3(90f, 20f, 1f), 0);
            walls.AddBox(new Vector3(0f, 9f, -wallDist), new Vector3(90f, 20f, 1f), 0);
            walls.AddBox(new Vector3(wallDist, 9f, 0f), new Vector3(1f, 20f, 90f), 0);
            walls.AddBox(new Vector3(-wallDist, 9f, 0f), new Vector3(1f, 20f, 90f), 0);
            walls.AddBox(new Vector3(0f, 19f, 0f), new Vector3(90f, 0.5f, 90f), 0);
            // lighting truss under the roof
            for (int i = -2; i <= 2; i++)
            {
                walls.AddBox(new Vector3(i * 7f, 15.5f, 0f), new Vector3(0.5f, 0.5f, 50f), 1);
                walls.AddBox(new Vector3(0f, 15.5f, i * 9f), new Vector3(50f, 0.5f, 0.5f), 1);
            }
            var wallsGo = Visual(parent, "Walls", SaveMesh(walls.ToMesh("Walls"), "Walls"), Mat("Wall"), Mat("Truss"));
            wallsGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // LED ribbon around the top of the lower bowl
            float ribbonY = 0.3f + 9 * 0.44f + 0.55f;
            float rn = CourtMetrics.HalfLength + 3.3f + 9 * 0.88f + 0.3f;
            float rw = CourtMetrics.HalfWidth + 3.6f + 9 * 0.88f + 0.3f;
            var ribbon = new MeshBatch();
            ribbon.AddBox(new Vector3(0f, ribbonY, rn), new Vector3(rw * 2f, 0.42f, 0.1f), 0);
            ribbon.AddBox(new Vector3(0f, ribbonY, -rn), new Vector3(rw * 2f, 0.42f, 0.1f), 0);
            ribbon.AddBox(new Vector3(rw, ribbonY, 0f), new Vector3(0.1f, 0.42f, rn * 2f), 0);
            ribbon.AddBox(new Vector3(-rw, ribbonY, 0f), new Vector3(0.1f, 0.42f, rn * 2f), 0);
            var ribbonGo = Visual(parent, "LedRibbon", SaveMesh(ribbon.ToMesh("LedRibbon"), "LedRibbon"), Mat("LedRibbon"));
            ribbonGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // centre-hung scoreboard
            var board = new MeshBatch();
            board.AddBox(new Vector3(0f, 10.2f, 0f), new Vector3(5.2f, 2.6f, 5.2f), 0);
            board.AddBox(new Vector3(0f, 12.6f, 0f), new Vector3(0.3f, 2.4f, 0.3f), 0);
            float s = 2.62f;
            board.AddQuad(new Vector3(0f, 10.2f, -s), Vector3.right * 2.3f, Vector3.up * 1.1f, Vector3.back, 1);
            board.AddQuad(new Vector3(0f, 10.2f, s), Vector3.left * 2.3f, Vector3.up * 1.1f, Vector3.forward, 1);
            board.AddQuad(new Vector3(-s, 10.2f, 0f), Vector3.back * 2.3f, Vector3.up * 1.1f, Vector3.left, 1);
            board.AddQuad(new Vector3(s, 10.2f, 0f), Vector3.forward * 2.3f, Vector3.up * 1.1f, Vector3.right, 1);
            var boardGo = Visual(parent, "Scoreboard", SaveMesh(board.ToMesh("Scoreboard"), "Scoreboard"), Mat("BaseBlack"), Mat("Screen"));
            boardGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ lighting

        static void BuildLighting(Transform parent)
        {
            var key = MakeLight(parent, "KeyLight", LightType.Directional, new Color(1f, 0.96f, 0.9f), 1.35f);
            key.transform.rotation = Quaternion.Euler(64f, -32f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.82f;
            key.shadowBias = 0.03f;
            key.shadowNormalBias = 0.5f;
            RenderSettings.sun = key;

            // overhead rigs on the court (no shadows; the key light carries them)
            Vector3[] rigs = { new Vector3(-8f, 11.5f, 7f), new Vector3(8f, 11.5f, 7f), new Vector3(-8f, 11.5f, -7f), new Vector3(8f, 11.5f, -7f) };
            foreach (Vector3 p in rigs)
            {
                var spot = MakeLight(parent, "CourtRig", LightType.Spot, new Color(1f, 0.97f, 0.92f), 38f);
                spot.transform.position = p;
                spot.transform.LookAt(new Vector3(p.x * 0.15f, 0f, p.z * 0.45f));
                spot.range = 30f;
                spot.spotAngle = 78f;
                spot.innerSpotAngle = 50f;
            }
            // goal spots: the play end is the hero
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                var spot = MakeLight(parent, "GoalRig", LightType.Spot, new Color(1f, 0.98f, 0.94f), 34f);
                spot.transform.position = new Vector3(x, 10.5f, 8.5f);
                spot.transform.LookAt(CourtMetrics.RimCenter + Vector3.down * 0.5f);
                spot.range = 13f;
                spot.spotAngle = 44f;
                spot.innerSpotAngle = 28f;
            }
            // cool rim light from behind the glass for separation
            var rim = MakeLight(parent, "RimLight", LightType.Spot, new Color(0.62f, 0.76f, 1f), 26f);
            rim.transform.position = new Vector3(0f, 6.5f, CourtMetrics.HalfLength + 3.5f);
            rim.transform.LookAt(new Vector3(0f, 1.6f, CourtMetrics.FreeThrowZ));
            rim.range = 26f;
            rim.spotAngle = 70f;
            rim.innerSpotAngle = 40f;
            // warm fills
            foreach (float z in new[] { 5f, -6f })
            {
                var fill = MakeLight(parent, "Fill", LightType.Point, new Color(1f, 0.9f, 0.78f), 9f);
                fill.transform.position = new Vector3(0f, 6.5f, z);
                fill.range = 24f;
            }
            // crowd wash, blue, dim
            (Vector3 pos, Vector3 target)[] wash =
            {
                (new Vector3(0f, 13f, 9f), new Vector3(0f, 3.5f, 23f)),
                (new Vector3(0f, 13f, -9f), new Vector3(0f, 3.5f, -23f)),
                (new Vector3(6f, 13f, 0f), new Vector3(17f, 3.5f, 0f)),
                (new Vector3(-6f, 13f, 0f), new Vector3(-17f, 3.5f, 0f)),
            };
            foreach (var w in wash)
            {
                var spot = MakeLight(parent, "CrowdWash", LightType.Spot, new Color(0.62f, 0.68f, 0.95f), 16f);
                spot.transform.position = w.pos;
                spot.transform.LookAt(w.target);
                spot.range = 34f;
                spot.spotAngle = 110f;
                spot.innerSpotAngle = 70f;
            }
            // A soft warm bounce under the goal. Kept low, weak and away from every shooting spot:
            // at (0, 1.2, 8) it sat half a metre behind the free-throw shooter and burned them white.
            var bounce = MakeLight(parent, "CourtBounce", LightType.Point, new Color(1f, 0.85f, 0.7f), 2.5f);
            bounce.transform.position = new Vector3(0f, 0.3f, CourtMetrics.RimCenter.z - 1.5f);
            bounce.range = 6f;

            var probeGo = new GameObject("CourtReflectionProbe");
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.position = new Vector3(0f, 2.5f, 6f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.boxProjection = true;
            probe.size = new Vector3(30f, 12f, 44f);
            probe.resolution = 128;
            probe.intensity = 1f;
            probe.cullingMask = ~(GameLayers.Mask(GameLayers.Player) | GameLayers.Mask(GameLayers.Ball) | GameLayers.Mask(GameLayers.Preview));
        }

        static Light MakeLight(Transform parent, string name, LightType type, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = ~GameLayers.Mask(GameLayers.Preview);
            go.AddComponent<UniversalAdditionalLightData>();
            return light;
        }

        static void ApplyRenderSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.27f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.13f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.04f, 0.035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.04f);
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.skybox = null;
        }

        // ------------------------------------------------------------------ camera, volume, host

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(-2f, 3.5f, 4f);
            go.transform.LookAt(CourtMetrics.RimCenter);
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 40f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.018f, 0.03f);
            cam.cullingMask = ~GameLayers.Mask(GameLayers.Preview);
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            go.AddComponent<GameCameraDirector>();
        }

        static void BuildVolume()
        {
            var go = new GameObject("Global Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
                vol.sharedProfile = profile;
        }

        static void BuildGameHost()
        {
            var go = new GameObject("BasketballBettingGame");
            var gm = go.AddComponent<GameManager>();
            string tuningPath = SettingsRoot + "/ShotTuning.asset";
            var tuning = AssetDatabase.LoadAssetAtPath<ShotTuning>(tuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<ShotTuning>();
                AssetDatabase.CreateAsset(tuning, tuningPath);
            }
            var so = new SerializedObject(gm);
            so.FindProperty("_shotTuning").objectReferenceValue = tuning;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ materials

        static Material Mat(string name)
        {
            if (Mats.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            string path = MatRoot + "/" + name + ".mat";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            Configure(mat, name);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(mat);
                existing.shaderKeywords = mat.shaderKeywords;
                existing.renderQueue = mat.renderQueue;
                EditorUtility.SetDirty(existing);
                mat = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            Mats[name] = mat;
            return mat;
        }

        static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "/" + file);

        static void Configure(Material m, string name)
        {
            switch (name)
            {
                case "CourtFloor":
                    string albedo = UseSymmetricalCourt ? "Court_Albedo_Symmetrical.png" : "Court_Albedo.png";
                    string mask = UseSymmetricalCourt ? "Court_Mask_Symmetrical.png" : "Court_Mask.png";
                    m.SetTexture("_BaseMap", Tex(albedo));
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_MetallicGlossMap", Tex(mask));
                    m.SetFloat("_Metallic", 1f);
                    m.SetFloat("_Smoothness", 1f);
                    m.SetFloat("_SmoothnessTextureChannel", 0f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetTexture("_DetailAlbedoMap", Tex("WoodDetail_Albedo.png"));
                    m.SetTexture("_DetailNormalMap", Tex("WoodDetail_Normal.png"));
                    m.SetFloat("_DetailAlbedoMapScale", 0.85f);
                    m.SetFloat("_DetailNormalMapScale", 0.55f);
                    m.SetTextureScale("_DetailAlbedoMap", new Vector2(5f, 9.5f));
                    m.EnableKeyword("_DETAIL_MULX2");
                    m.SetFloat("_EnvironmentReflections", 1f);
                    m.SetFloat("_SpecularHighlights", 1f);
                    break;
                case "ArenaFloor":
                    Solid(m, new Color(0.055f, 0.055f, 0.065f), 0.32f, 0f);
                    break;
                case "Basketball":
                    m.SetTexture("_BaseMap", Tex("Basketball_Albedo.png"));
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BumpMap", Tex("Basketball_Normal.png"));
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                    m.SetFloat("_Smoothness", 0.38f);
                    m.SetFloat("_Metallic", 0f);
                    break;
                case "RimIron":
                    Solid(m, new Color(0.93f, 0.30f, 0.10f), 0.62f, 0.8f);
                    break;
                case "Steel":
                    Solid(m, new Color(0.22f, 0.23f, 0.25f), 0.55f, 0.75f);
                    break;
                case "Truss":
                    Solid(m, new Color(0.10f, 0.10f, 0.11f), 0.4f, 0.6f);
                    break;
                case "BaseBlack":
                    Solid(m, new Color(0.06f, 0.06f, 0.07f), 0.3f, 0.1f);
                    break;
                case "Padding":
                    Solid(m, new Color(0.70f, 0.10f, 0.13f), 0.35f, 0f);
                    break;
                case "NetCord":
                    Solid(m, new Color(0.96f, 0.96f, 0.94f), 0.25f, 0f);
                    break;
                case "BackboardGlass":
                    m.SetTexture("_BaseMap", Tex("Backboard_Decal.png"));
                    m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f));
                    m.SetFloat("_Smoothness", 0.3f);
                    m.SetFloat("_Metallic", 0.0f);
                    m.SetFloat("_EnvironmentReflections", 0f);
                    m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    Transparent(m);
                    break;
                case "Concrete":
                    Solid(m, new Color(0.11f, 0.115f, 0.13f), 0.22f, 0f);
                    break;
                case "SeatNavy": Solid(m, new Color(0.08f, 0.13f, 0.32f), 0.42f, 0f); break;
                case "SeatRed": Solid(m, new Color(0.48f, 0.08f, 0.10f), 0.42f, 0f); break;
                case "SeatCharcoal": Solid(m, new Color(0.14f, 0.14f, 0.16f), 0.42f, 0f); break;
                case "Crowd0": Solid(m, new Color(0.72f, 0.14f, 0.16f), 0.18f, 0f); break;
                case "Crowd1": Solid(m, new Color(0.12f, 0.20f, 0.52f), 0.18f, 0f); break;
                case "Crowd2": Solid(m, new Color(0.62f, 0.62f, 0.66f), 0.18f, 0f); break;
                case "Crowd3": Solid(m, new Color(0.10f, 0.10f, 0.11f), 0.18f, 0f); break;
                case "Crowd4": Solid(m, new Color(0.85f, 0.62f, 0.14f), 0.18f, 0f); break;
                case "Crowd5": Solid(m, new Color(0.14f, 0.42f, 0.28f), 0.18f, 0f); break;
                case "Skin0": Solid(m, new Color(0.35f, 0.23f, 0.15f), 0.3f, 0f); break;
                case "Skin1": Solid(m, new Color(0.56f, 0.38f, 0.25f), 0.3f, 0f); break;
                case "Skin2": Solid(m, new Color(0.80f, 0.62f, 0.48f), 0.3f, 0f); break;
                case "Wall":
                    Solid(m, new Color(0.02f, 0.022f, 0.03f), 0.1f, 0f);
                    break;
                case "LedRibbon":
                    Solid(m, new Color(0.05f, 0.02f, 0.03f), 0.5f, 0f);
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", Tex("LedRibbon_Emissive.png"));
                    m.SetTextureScale("_EmissionMap", new Vector2(24f, 1f));
                    m.SetColor("_EmissionColor", new Color(1f, 0.32f, 0.42f) * 1.5f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    break;
                case "Screen":
                    Solid(m, new Color(0.02f, 0.03f, 0.06f), 0.7f, 0f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.25f, 0.45f, 1f) * 2.2f);
                    break;
                default:
                    Solid(m, Color.magenta, 0.3f, 0f);
                    break;
            }
        }

        static void Solid(Material m, Color c, float smoothness, float metallic)
        {
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            string n = m.name;
            if (n.StartsWith("Seat") || n.StartsWith("Crowd") || n.StartsWith("Skin") || n == "Concrete" || n == "Wall")
            {
                // The court probe must not paint the bowl orange.
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_Smoothness", Mathf.Min(smoothness, 0.2f));
            }
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Visual(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = GameLayers.Arena;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            return go;
        }

        static Mesh QuadMesh(float width, float length, string name)
        {
            var mesh = new Mesh { name = name };
            float hw = width * 0.5f, hl = length * 0.5f;
            mesh.vertices = new[] { new Vector3(-hw, 0f, -hl), new Vector3(hw, 0f, -hl), new Vector3(hw, 0f, hl), new Vector3(-hw, 0f, hl) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshRoot + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.uv = mesh.uv;
                existing.tangents = mesh.tangents;
                existing.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    existing.SetTriangles(mesh.GetTriangles(i), i, true);
                existing.RecalculateBounds();
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { "Assets/BasketballBetting/Art", TexRoot, MatRoot, MeshRoot, SettingsRoot })
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        static void ConfigureTextureImporters()
        {
            AssetDatabase.Refresh();
            SetImporter("Court_Albedo.png", false, true, 2048, false);
            SetImporter("Court_Mask.png", false, false, 2048, false);
            SetImporter("Court_Albedo_Symmetrical.png", false, true, 2048, false);
            SetImporter("Court_Mask_Symmetrical.png", false, false, 2048, false);
            SetImporter("WoodDetail_Albedo.png", false, true, 1024, true);
            SetImporter("WoodDetail_Normal.png", true, false, 1024, true);
            SetImporter("Basketball_Albedo.png", false, true, 1024, false);
            SetImporter("Basketball_Normal.png", true, false, 1024, false);
            SetImporter("Backboard_Decal.png", false, true, 1024, false);
            SetImporter("LedRibbon_Emissive.png", false, true, 1024, true);
        }

        static void SetImporter(string file, bool normal, bool srgb, int maxSize, bool repeat)
        {
            string path = TexRoot + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ArenaSceneBuilder] Missing texture " + path);
                return;
            }
            bool changed = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
            var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (importer.wrapMode != wrap) { importer.wrapMode = wrap; changed = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
            if (importer.anisoLevel < 8) { importer.anisoLevel = 8; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ) { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            if (changed)
                importer.SaveAndReimport();
        }

        static void MarkStatic(Transform root)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponentInParent<Basketball>() != null || mr.GetComponentInParent<HoopNet>() != null || mr.name.StartsWith("Crowd"))
                    continue;
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
