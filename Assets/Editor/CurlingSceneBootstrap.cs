#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Curling.Core;
using Curling.Physics;
using Curling.Input;
using Curling.Match;
using CCore = Curling.Core.Constants;

namespace Curling.EditorTools
{
    public static class CurlingSceneBootstrap
    {
        [MenuItem("Curling/Bootstrap Scene (Step 1 Prototype)")]
        public static void Bootstrap()
        {
            UnityEngine.Physics.gravity = new Vector3(0f, -CCore.Gravity, 0f);

            CleanupExistingScene();

            var root = new GameObject("CurlingScene");

            BuildArenaDecorations(root.transform);
            BuildSheet(root.transform);
            BuildHouse(root.transform);
            BuildLines(root.transform);
            var stones = BuildStonePool(root.transform);
            BuildCameras(root.transform);
            BuildLights(root.transform);
            var input = BuildShotInput(root.transform);
            BuildMatchManager(root.transform, input, stones);

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("Curling",
                "Bootstrap 完了。\n" +
                "Play を押し Game ビューをクリックしてください。\n" +
                "Display 1: ハウス上空の追従カメラ\n" +
                "Display 2: ストーンを斜めから見る追従カメラ\n" +
                "Display 3: 投げたストーン視点\n" +
                "↑↓: 速度  ←→: 角度  R: 回転反転  Space: 投擲",
                "OK");
        }

        static void CleanupExistingScene()
        {
            var existing = GameObject.Find("CurlingScene");
            if (existing != null) Object.DestroyImmediate(existing);
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null) Object.DestroyImmediate(defaultCam);
            var defaultLight = GameObject.Find("Directional Light");
            if (defaultLight != null) Object.DestroyImmediate(defaultLight);
        }

        static void BuildSheet(Transform parent)
        {
            var ice = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ice.name = "IceSheet";
            ice.transform.SetParent(parent);
            ice.transform.localScale = new Vector3(CCore.SheetWidth / 10f, 1f, CCore.SheetLength / 10f);
            ice.transform.position = new Vector3(0f, 0f, CCore.SheetLength * 0.5f);
            ice.transform.rotation = Quaternion.identity;
            ice.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial("IceMat", new Color(0.85f, 0.92f, 0.98f));
        }

        static void BuildHouse(Transform parent)
        {
            var house = new GameObject("House");
            house.transform.SetParent(parent);
            house.transform.position = new Vector3(0f, 0.005f, CCore.HouseCenterY);

            AddHouseDisk(house.transform, "Ring_12ft", CCore.HouseRadius, new Color(0.2f, 0.4f, 0.9f));
            AddHouseDisk(house.transform, "Ring_8ft", CCore.HouseRadius * (1.219f / 1.829f), new Color(0.95f, 0.95f, 0.95f));
            AddHouseDisk(house.transform, "Ring_4ft", CCore.HouseRadius * (0.610f / 1.829f), new Color(0.9f, 0.2f, 0.2f));
            AddHouseDisk(house.transform, "Button",   0.152f, new Color(0.98f, 0.98f, 0.98f));
        }

        static void AddHouseDisk(Transform parent, string name, float radius, Color color)
        {
            var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = name;
            disk.transform.SetParent(parent);
            float h = 0.002f;
            disk.transform.localScale = new Vector3(radius * 2f, h, radius * 2f);
            float layer = parent.childCount * 0.001f;
            disk.transform.localPosition = new Vector3(0f, layer, 0f);
            var col = disk.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            disk.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial($"House_{name}", color);
        }

        static void BuildLines(Transform parent)
        {
            float halfW = CCore.SheetHalfWidth;
            AddLine(parent, "HogLine",   new Vector3(-halfW, 0.01f, CCore.HogLineY),  new Vector3(halfW, 0.01f, CCore.HogLineY),  new Color(0.9f, 0.2f, 0.2f), 0.08f);
            AddLine(parent, "TeeLine",   new Vector3(-halfW, 0.01f, CCore.TeeLineY),  new Vector3(halfW, 0.01f, CCore.TeeLineY),  new Color(0.1f, 0.1f, 0.1f), 0.04f);
            AddLine(parent, "BackLine",  new Vector3(-halfW, 0.01f, CCore.BackLineY), new Vector3(halfW, 0.01f, CCore.BackLineY), new Color(0.1f, 0.1f, 0.1f), 0.04f);
            AddLine(parent, "CenterLine",new Vector3(0f,     0.01f, 0f),              new Vector3(0f,    0.01f, CCore.SheetLength), new Color(0.4f, 0.4f, 0.45f), 0.02f);
            AddLine(parent, "SideLeft",  new Vector3(-halfW, 0.01f, 0f),              new Vector3(-halfW, 0.01f, CCore.SheetLength), new Color(0.4f, 0.4f, 0.45f), 0.02f);
            AddLine(parent, "SideRight", new Vector3(halfW,  0.01f, 0f),              new Vector3(halfW,  0.01f, CCore.SheetLength), new Color(0.4f, 0.4f, 0.45f), 0.02f);
        }

        static void AddLine(Transform parent, string name, Vector3 a, Vector3 b, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.material = MakeColorMaterial($"Line_{name}", c);
        }

        static StoneBody[] BuildStonePool(Transform parent)
        {
            var pool = new GameObject("StonePool");
            pool.transform.SetParent(parent);
            int total = CCore.StonesPerTeamStandard * 2;
            var arr = new StoneBody[total];

            for (int i = 0; i < total; i++)
            {
                bool team0 = i < CCore.StonesPerTeamStandard;
                var s = new GameObject($"Stone_{i}");
                s.transform.SetParent(pool.transform);
                s.transform.position = new Vector3(-3f + (i % 8) * 0.4f, 0f, -2f - (team0 ? 0f : 0.6f));

                // Body (cylinder, height 0.115 m, radius 0.145 m)
                var bodyVis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bodyVis.name = "Body";
                bodyVis.transform.SetParent(s.transform);
                bodyVis.transform.localPosition = new Vector3(0f, 0.11f, 0f);
                bodyVis.transform.localScale = new Vector3(CCore.StoneRadius * 2f, 0.115f, CCore.StoneRadius * 2f);
                var bodyCol = bodyVis.GetComponent<Collider>();
                if (bodyCol != null) Object.DestroyImmediate(bodyCol);
                bodyVis.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial(team0 ? "StoneRedBody" : "StoneYellowBody",
                    team0 ? new Color(0.5f, 0.05f, 0.05f) : new Color(0.55f, 0.45f, 0.05f));

                // Top granite cap (gray)
                var capTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                capTop.name = "CapTop";
                capTop.transform.SetParent(s.transform);
                capTop.transform.localPosition = new Vector3(0f, 0.235f, 0f);
                capTop.transform.localScale = new Vector3(CCore.StoneRadius * 2f, 0.01f, CCore.StoneRadius * 2f);
                var capTopCol = capTop.GetComponent<Collider>();
                if (capTopCol != null) Object.DestroyImmediate(capTopCol);
                capTop.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial("GraniteGray", new Color(0.55f, 0.55f, 0.6f));

                // Handle (small box on top, team-color)
                var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "Handle";
                handle.transform.SetParent(s.transform);
                handle.transform.localPosition = new Vector3(0f, 0.295f, 0f);
                handle.transform.localScale = new Vector3(0.13f, 0.04f, 0.03f);
                var handleCol = handle.GetComponent<Collider>();
                if (handleCol != null) Object.DestroyImmediate(handleCol);
                handle.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial(team0 ? "HandleRed" : "HandleYellow",
                    team0 ? new Color(0.95f, 0.2f, 0.2f) : new Color(1f, 0.9f, 0.15f));

                // Bottom striker ring (dark)
                var bottom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bottom.name = "BottomRing";
                bottom.transform.SetParent(s.transform);
                bottom.transform.localPosition = new Vector3(0f, 0.005f, 0f);
                bottom.transform.localScale = new Vector3(CCore.StoneRadius * 2f * 0.95f, 0.005f, CCore.StoneRadius * 2f * 0.95f);
                var bottomCol = bottom.GetComponent<Collider>();
                if (bottomCol != null) Object.DestroyImmediate(bottomCol);
                bottom.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial("StoneBottom", new Color(0.15f, 0.15f, 0.18f));

                var rb = s.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.mass = 19.96f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var sb = s.AddComponent<StoneBody>();
                sb.team = team0 ? Team.Team0 : Team.Team1;
                sb.stoneIndex = i;

                var col = s.AddComponent<SphereCollider>();
                col.radius = CCore.StoneRadius;
                col.center = new Vector3(0f, 0.11f, 0f);
                var pm = new PhysicsMaterial($"StonePM_{i}")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 1.0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
                col.material = pm;

                var trail = s.AddComponent<TrailRenderer>();
                trail.time = 6f;
                trail.startWidth = 0.2f;
                trail.endWidth = 0.05f;
                trail.minVertexDistance = 0.05f;
                trail.material = MakeColorMaterial(team0 ? "TrailRed" : "TrailYellow",
                    team0 ? new Color(1f, 0.4f, 0.4f, 0.9f) : new Color(1f, 0.95f, 0.4f, 0.9f));
                trail.emitting = false;

                s.SetActive(false);
                arr[i] = sb;
            }
            return arr;
        }

        static Camera[] BuildCameras(Transform parent)
        {
            // メインカメラ (全画面背景): ハウス上空俯瞰
            var camMain = new GameObject("Camera1_Main_Overhead");
            camMain.transform.SetParent(parent);
            camMain.tag = "MainCamera";
            camMain.transform.position = new Vector3(0f, 10f, CCore.HogLineY - 12f);
            camMain.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var c1 = camMain.AddComponent<Camera>();
            ConfigureCam(c1, true);
            c1.targetDisplay = 0;
            c1.rect = new Rect(0f, 0f, 1f, 1f);
            c1.depth = 0f;
            c1.orthographic = true;
            c1.orthographicSize = 4f;
            camMain.AddComponent<AudioListener>();
            var mainFollow = camMain.AddComponent<Curling.Match.CameraFollow>();
            mainFollow.mode = Curling.Match.CameraFollow.FollowMode.OverheadCenterline;
            mainFollow.overheadHeight = 10f;
            mainFollow.smooth = 5f;
            mainFollow.trackShotBeforeOverhead = true;
            mainFollow.overheadSwitchZ = CCore.HogLineY;
            mainFollow.shotTrackOffset = new Vector3(0f, 1.35f, -2.6f);
            mainFollow.shotTrackLookAhead = 2.6f;
            mainFollow.shotTrackLookHeight = 0.18f;
            mainFollow.shotTrackFieldOfView = 60f;
            mainFollow.shotTrackNearClip = 0.03f;
            mainFollow.useHumanAimDuringSetup = true;
            mainFollow.humanAimEyeHeight = 1.22f;
            mainFollow.humanAimBehindStone = 2.05f;
            mainFollow.humanAimFocusDistance = 3.5f;
            mainFollow.humanAimLookHeight = 0.18f;
            mainFollow.humanAimFieldOfView = 58f;
            mainFollow.humanAimNearClip = 0.03f;

            // PiP 右上: 斜め俯瞰 (Oblique)
            var camPipOblique = new GameObject("Camera2_PiP_Oblique");
            camPipOblique.transform.SetParent(parent);
            camPipOblique.transform.position = new Vector3(1.7f, 1.25f, CCore.HouseCenterY - 2.1f);
            camPipOblique.transform.LookAt(new Vector3(0f, 0.12f, CCore.HouseCenterY), Vector3.up);
            var c2 = camPipOblique.AddComponent<Camera>();
            ConfigureCam(c2, true);
            c2.targetDisplay = 0;
            c2.rect = new Rect(0.755f, 0.70f, 0.24f, 0.29f);
            c2.depth = 1f;
            c2.fieldOfView = 58f;
            var obliqueFollow = camPipOblique.AddComponent<Curling.Match.CameraFollow>();
            obliqueFollow.mode = Curling.Match.CameraFollow.FollowMode.ObliqueStone;
            obliqueFollow.obliqueOffset = new Vector3(1.7f, 1.25f, -2.1f);
            obliqueFollow.smooth = 6f;

            // PiP 右下: ストーン視点 (StoneView)
            var camPipStone = new GameObject("Camera3_PiP_StoneView");
            camPipStone.transform.SetParent(parent);
            camPipStone.transform.position = new Vector3(0f, 0.34f, CCore.HogLineY - 11.9f);
            camPipStone.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var c3 = camPipStone.AddComponent<Camera>();
            ConfigureCam(c3, true);
            c3.targetDisplay = 0;
            c3.rect = new Rect(0.755f, 0.40f, 0.24f, 0.29f);
            c3.depth = 1f;
            c3.fieldOfView = 72f;
            c3.nearClipPlane = 0.02f;
            var stoneFollow = camPipStone.AddComponent<Curling.Match.CameraFollow>();
            stoneFollow.mode = Curling.Match.CameraFollow.FollowMode.StoneView;
            stoneFollow.smooth = 12f;

            return new[] { c1, c2, c3 };
        }

        static void ConfigureCam(Camera c, bool enabled)
        {
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.55f, 0.7f, 0.88f);
            c.fieldOfView = 55f;
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 200f;
            c.enabled = enabled;
        }

        static void BuildLights(Transform parent)
        {
            var light = new GameObject("DirectionalLight");
            light.transform.SetParent(parent);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.color = new Color(1f, 0.98f, 0.95f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.35f);
        }

        static ShotInputController BuildShotInput(Transform parent)
        {
            var go = new GameObject("ShotInput");
            go.transform.SetParent(parent);
            return go.AddComponent<ShotInputController>();
        }

        static void BuildMatchManager(Transform parent, ShotInputController input, StoneBody[] stones)
        {
            var go = new GameObject("MatchManager");
            go.transform.SetParent(parent);
            var mgr = go.AddComponent<MatchManager>();
            mgr.humanInput = input;
            mgr.stonePool = stones;
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(parent);
            spawn.transform.position = new Vector3(0f, 0f, CCore.HogLineY - 12f);
            mgr.stoneSpawnPoint = spawn.transform;
            input.previewOrigin = spawn.transform;

            var autoStart = go.AddComponent<MatchAutoStart>();
            autoStart.difficultyOverride = CpuDifficulty.Easy;
            autoStart.endCountOverride = 2;
            go.AddComponent<MultiDisplayActivator>();
        }

        static void BuildArenaDecorations(Transform parent)
        {
            var arena = new GameObject("Arena");
            arena.transform.SetParent(parent);

            float halfW = CCore.SheetHalfWidth;
            float sheetLen = CCore.SheetLength;
            float sheetMidZ = sheetLen * 0.5f;

            // 1. アリーナ床 (シートの周囲、暗色)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(arena.transform);
            floor.transform.localScale = new Vector3(3.0f, 1f, 6.0f); // 30m x 60m
            floor.transform.position = new Vector3(0f, -0.05f, sheetMidZ);
            var floorCol = floor.GetComponent<Collider>();
            if (floorCol != null) Object.DestroyImmediate(floorCol);
            floor.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial("ArenaFloor", new Color(0.13f, 0.14f, 0.18f));

            // 2. シート両側の縁石風サイドボード (低い白い壁)
            float boardX = halfW + 0.12f;
            AddBox(arena.transform, "Sideboard_L", new Vector3(-boardX, 0.08f, sheetMidZ),
                new Vector3(0.2f, 0.15f, sheetLen + 0.4f), new Color(0.92f, 0.92f, 0.94f));
            AddBox(arena.transform, "Sideboard_R", new Vector3(boardX, 0.08f, sheetMidZ),
                new Vector3(0.2f, 0.15f, sheetLen + 0.4f), new Color(0.92f, 0.92f, 0.94f));

            // 3. 広告パネル (サイドボードの外側に色とりどりに並ぶ)
            Color[] palette = {
                new Color(0.85f, 0.18f, 0.20f),
                new Color(0.18f, 0.42f, 0.85f),
                new Color(0.95f, 0.72f, 0.10f),
                new Color(0.14f, 0.68f, 0.42f),
                new Color(0.60f, 0.18f, 0.70f),
            };
            int adCount = 8;
            float adSpan = sheetLen / adCount;
            float adX = boardX + 0.14f;
            for (int i = 0; i < adCount; i++)
            {
                float zPos = adSpan * (i + 0.5f);
                Color c = palette[i % palette.Length];
                AddBox(arena.transform, $"AdL_{i}", new Vector3(-adX, 0.5f, zPos),
                    new Vector3(0.04f, 0.7f, adSpan * 0.85f), c);
                AddBox(arena.transform, $"AdR_{i}", new Vector3(adX, 0.5f, zPos),
                    new Vector3(0.04f, 0.7f, adSpan * 0.85f), c);
            }

            // 4. 両端 (投擲側 / ハウス奥) のバックウォール
            AddBox(arena.transform, "BackWall_Front", new Vector3(0f, 2f, -1.2f),
                new Vector3(18f, 4f, 0.3f), new Color(0.25f, 0.28f, 0.35f));
            AddBox(arena.transform, "BackWall_Behind", new Vector3(0f, 2f, sheetLen + 1.2f),
                new Vector3(18f, 4f, 0.3f), new Color(0.25f, 0.28f, 0.35f));

            // 5. スコアボード (ハウス奥側の壁面に大きな表示板)
            AddBox(arena.transform, "Scoreboard_Bg", new Vector3(0f, 3.0f, sheetLen + 1.04f),
                new Vector3(7.5f, 1.8f, 0.06f), new Color(0.06f, 0.07f, 0.12f));
            // 「Team Red / Team Yellow」のラベル風カラーパネル
            AddBox(arena.transform, "Scoreboard_TeamRed", new Vector3(-2.4f, 3.5f, sheetLen + 1.01f),
                new Vector3(2.4f, 0.55f, 0.02f), new Color(0.92f, 0.18f, 0.18f));
            AddBox(arena.transform, "Scoreboard_TeamYellow", new Vector3(2.4f, 3.5f, sheetLen + 1.01f),
                new Vector3(2.4f, 0.55f, 0.02f), new Color(0.95f, 0.85f, 0.12f));
            // エンドごとのスコアグリッド (10エンド分のセル)
            for (int e = 0; e < 10; e++)
            {
                float cellX = -3.0f + e * 0.65f;
                AddBox(arena.transform, $"Score_E{e}_R", new Vector3(cellX, 3.05f, sheetLen + 1.01f),
                    new Vector3(0.55f, 0.32f, 0.02f), new Color(0.22f, 0.22f, 0.28f));
                AddBox(arena.transform, $"Score_E{e}_Y", new Vector3(cellX, 2.65f, sheetLen + 1.01f),
                    new Vector3(0.55f, 0.32f, 0.02f), new Color(0.18f, 0.18f, 0.24f));
            }

            // 6. ハック (投擲台、シート両端)
            float hackOffset = 0.15f;
            AddBox(arena.transform, "Hack_FrontL", new Vector3(-hackOffset, 0.04f, -0.18f),
                new Vector3(0.14f, 0.08f, 0.18f), new Color(0.05f, 0.05f, 0.07f));
            AddBox(arena.transform, "Hack_FrontR", new Vector3(hackOffset, 0.04f, -0.18f),
                new Vector3(0.14f, 0.08f, 0.18f), new Color(0.05f, 0.05f, 0.07f));
            AddBox(arena.transform, "Hack_BackL", new Vector3(-hackOffset, 0.04f, sheetLen + 0.18f),
                new Vector3(0.14f, 0.08f, 0.18f), new Color(0.05f, 0.05f, 0.07f));
            AddBox(arena.transform, "Hack_BackR", new Vector3(hackOffset, 0.04f, sheetLen + 0.18f),
                new Vector3(0.14f, 0.08f, 0.18f), new Color(0.05f, 0.05f, 0.07f));

            // 7. シート番号プレート (投擲側)
            AddBox(arena.transform, "SheetNumPlate", new Vector3(-(halfW + 1.5f), 1.4f, -0.9f),
                new Vector3(1.2f, 0.9f, 0.05f), new Color(0.95f, 0.95f, 0.98f));
            AddBox(arena.transform, "SheetNumDigit", new Vector3(-(halfW + 1.5f), 1.4f, -0.92f),
                new Vector3(0.55f, 0.65f, 0.02f), new Color(0.1f, 0.1f, 0.15f));

            // 8. 観客席 (両側に4段の階段)
            BuildStands(arena.transform, true);
            BuildStands(arena.transform, false);

            // 9. 天井照明 (吊り下げ風)
            const float lampY = 11.5f;
            const float wireY = 12.5f;
            for (int i = 0; i < 5; i++)
            {
                float zPos = (i + 1) * (sheetLen / 6f);
                AddBox(arena.transform, $"CeilingLamp_{i}", new Vector3(0f, lampY, zPos),
                    new Vector3(2.2f, 0.18f, 0.7f), new Color(0.96f, 0.95f, 0.78f));
                AddBox(arena.transform, $"LampWire_{i}_L", new Vector3(-0.9f, wireY, zPos),
                    new Vector3(0.04f, 2f, 0.04f), new Color(0.2f, 0.2f, 0.22f));
                AddBox(arena.transform, $"LampWire_{i}_R", new Vector3(0.9f, wireY, zPos),
                    new Vector3(0.04f, 2f, 0.04f), new Color(0.2f, 0.2f, 0.22f));
            }
        }

        static void BuildStands(Transform parent, bool left)
        {
            float sign = left ? -1f : 1f;
            float baseX = CCore.SheetHalfWidth + 0.6f;
            float sheetLen = CCore.SheetLength;
            int rows = 4;
            for (int row = 0; row < rows; row++)
            {
                float xPos = sign * (baseX + 1.0f + row * 1.1f);
                float yPos = 0.4f + row * 0.55f;
                float height = 0.6f + row * 0.5f;
                Color rowColor = (row % 2 == 0)
                    ? new Color(0.42f, 0.44f, 0.50f)
                    : new Color(0.32f, 0.34f, 0.40f);
                AddBox(parent, $"Stand_{(left ? "L" : "R")}_Row{row}",
                    new Vector3(xPos, yPos, sheetLen * 0.5f),
                    new Vector3(1.0f, height, sheetLen + 6f),
                    rowColor);
            }
        }

        static GameObject AddBox(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = size;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMaterial($"Arena_{name}", color);
            return go;
        }

        static Material MakeColorMaterial(string name, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        [MenuItem("Curling/Single-Stone Test Shot")]
        public static void TestShot()
        {
            var mgr = Object.FindAnyObjectByType<MatchManager>();
            if (mgr == null)
            {
                EditorUtility.DisplayDialog("Curling", "先に Curling/Bootstrap Scene を実行してください。", "OK");
                return;
            }
            var stones = mgr.stonePool;
            if (stones == null || stones.Length == 0) return;

            stones[0].gameObject.SetActive(true);
            stones[0].Launch(
                new ShotInput(new Vec2(0f, 2.85f), 1.57f),
                new Vector3(0f, 0f, CCore.HogLineY - 12f));
        }
    }
}
#endif
