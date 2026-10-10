using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SilentSprint.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Rải đèn đường (<c>lamppost.prefab</c>) NGẪU NHIÊN quanh map rồi ghi thẳng vào
    /// <c>Assets/Prefabs/Map.prefab</c>.
    ///
    /// Cách dùng: mở scene <b>Gameplay</b> (cần collider của map để dò đất), rồi menu
    /// <b>Tools ▸ Silent Sprint ▸ Randomize Lampposts In Map Prefab</b>. Mỗi lần bấm ra một bộ chỗ mới;
    /// muốn dựng lại đúng bộ vừa rồi thì dùng "Randomize Lampposts Again (Same Seed)" — seed đang dùng
    /// lưu trong EditorPrefs và ghi vào report. Muốn xoá sạch thì "Clear Lampposts In Map Prefab".
    ///
    /// Vì sao không còn bảng mốc cố định: bản trước lấy đèn đúng các điểm spawn item, nên player chỉ cần
    /// nhìn thấy đèn là đoán được vật phẩm nằm ở đâu. Bản này rải ngẫu nhiên quanh khu vực chơi — lấy
    /// mẫu quanh 40 điểm spawn item (bán kính 8–40m) nhưng chỉ nhận chỗ dò được đất phẳng cùng cao độ,
    /// trống (không có gì phía trên thân đèn, cách tường/vật cản xa) và cách mọi điểm spawn item ≥8m
    /// (nên vật phẩm không bao giờ mọc trong chân đèn), các đèn cách nhau ≥20m.
    ///
    /// Tool chạy ngay trong editor (không cần Play) và KHÔNG tạo object nào trong scene: nó mở prefab Map
    /// bằng <see cref="PrefabUtility.LoadPrefabContents"/>, thay nhóm đèn dưới object
    /// <see cref="ParentName"/> rồi lưu lại prefab. Cơ chế "tự cắm lại khi compile" của bản cũ đã bỏ
    /// (bảng mốc cố định không còn ý nghĩa khi vị trí là ngẫu nhiên), nên không còn chuyện đèn bị nhân đôi.
    /// </summary>
    public static class LamppostSetup
    {
        // ---------------------------------------------------------------- đường dẫn / hằng số

        public const string PrefabPath = "Assets/Prefabs/lamppost.prefab";
        public const string MapPrefabPath = "Assets/Prefabs/Map.prefab";
        public const string ParentName = "Lampposts";
        public const string GameplaySceneName = "Gameplay";

        const string MenuRoot = "Tools/Silent Sprint/";
        const string ReportRelativePath = "Temp/lamppost-setup-report.txt";
        const string SeedPreferenceKey = "SilentSprint.Lampposts.Seed";

        /// <summary>Số đèn rải quanh map.</summary>
        const int LampCount = 26;

        /// <summary>Bán kính lấy mẫu quanh một điểm spawn item — 8m để không dính chỗ item mọc, 40m là vùng chơi.</summary>
        const float SampleRadiusMin = 8f;
        const float SampleRadiusMax = 40f;

        /// <summary>Chỗ đặt phải cùng cao độ với mốc mồi trong khoảng này (tránh leo lên mái nhà).</summary>
        const float SameLevelTolerance = 4f;

        /// <summary>Mặt đất phải phẳng ít nhất mức này (pháp tuyến hướng lên) mới coi là chỗ cắm được.</summary>
        const float MinGroundNormalY = 0.95f;

        /// <summary>Đèn phải cách mọi điểm spawn item ít nhất ngần này để vật phẩm khỏi mọc trong chân đèn.</summary>
        const float MinSpawnPointDistance = 8f;

        /// <summary>Đèn phải cách chỗ đứng của nhân vật (player, monster) ít nhất ngần này.</summary>
        const float MinCharacterDistance = 6f;

        /// <summary>Hai đèn phải cách nhau ít nhất ngần này; map chật thì nới dần xuống <see cref="MinLampSpacingFloor"/>.</summary>
        const float MinLampSpacing = 20f;
        const float MinLampSpacingFloor = 12f;

        /// <summary>Bán kính dò vật cản quanh thân đèn và tầm dò khoảng trống phía trên thân đèn.</summary>
        const float ClearanceRadius = 0.9f;
        const float ClearanceAbove = 4.5f;

        const float ProbeAboveAnchor = 4f;
        const float ProbeBelowAnchor = 40f;
        const float ProbeFromHighUp = 80f;

        const int MaxAttemptsPerLamp = 3000;

        static string ProjectRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        // ---------------------------------------------------------------- menu

        [MenuItem(MenuRoot + "Randomize Lampposts In Map Prefab", false, 200)]
        public static void RandomizeFromMenu()
        {
            SetUp(true);
        }

        [MenuItem(MenuRoot + "Randomize Lampposts In Map Prefab", true)]
        static bool ValidateRandomizeFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath) != null;
        }

        [MenuItem(MenuRoot + "Randomize Lampposts Again (Same Seed)", false, 201)]
        public static void RandomizeAgainFromMenu()
        {
            SetUp(false);
        }

        [MenuItem(MenuRoot + "Randomize Lampposts Again (Same Seed)", true)]
        static bool ValidateRandomizeAgainFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath) != null
                   && EditorPrefs.HasKey(SeedPreferenceKey);
        }

        [MenuItem(MenuRoot + "Clear Lampposts In Map Prefab", false, 202)]
        public static void ClearFromMenu()
        {
            Clear();
        }

        [MenuItem(MenuRoot + "Clear Lampposts In Map Prefab", true)]
        static bool ValidateClearFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath) != null;
        }

        // ---------------------------------------------------------------- luồng chính

        /// <summary>
        /// Rải lại đèn trong <c>Map.prefab</c> theo một bộ vị trí ngẫu nhiên mới.
        /// </summary>
        /// <param name="newSeed">true = seed mới; false = dùng lại seed lần trước (ra đúng bộ cũ).</param>
        /// <returns>true nếu mọi bước chạy xong không lỗi.</returns>
        public static bool SetUp(bool newSeed)
        {
            var report = new StringBuilder();
            report.AppendLine("=== Lamppost setup ===");
            report.AppendLine("Thời điểm      : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            report.AppendLine("Chạy từ menu   : " + (newSeed ? "seed mới" : "dùng lại seed"));
            report.AppendLine("Unity          : " + Application.unityVersion);
            report.AppendLine("Scene đang mở  : " + SceneManager.GetActiveScene().name);

            int seed = newSeed ? UnityEngine.Random.Range(1, int.MaxValue) : EditorPrefs.GetInt(SeedPreferenceKey, 1);
            EditorPrefs.SetInt(SeedPreferenceKey, seed);
            report.AppendLine("Seed           : " + seed.ToString(CultureInfo.InvariantCulture));
            report.AppendLine();

            bool ok = PlaceAll(new System.Random(seed), seed, report);

            report.AppendLine();
            report.AppendLine(ok ? "KẾT QUẢ: xong." : "KẾT QUẢ: có bước lỗi, xem ở trên.");
            WriteReport(report);

            if (ok)
                Debug.Log("[Lamppost] Đã rải đèn vào " + MapPrefabPath + " (seed "
                          + seed.ToString(CultureInfo.InvariantCulture) + "). Chi tiết: " + ReportRelativePath);
            else
                Debug.LogWarning("[Lamppost] Setup chưa hoàn tất, xem " + ReportRelativePath + " hoặc Console này.");

            return ok;
        }

        /// <summary>Rải đèn với một seed chỉ định (dùng khi cần dựng lại đúng một bộ vị trí).</summary>
        public static bool RandomizeWithSeed(int seed)
        {
            EditorPrefs.SetInt(SeedPreferenceKey, seed);
            var report = new StringBuilder();
            report.AppendLine("=== Lamppost setup (seed chỉ định) ===");
            report.AppendLine("Seed           : " + seed.ToString(CultureInfo.InvariantCulture));
            bool ok = PlaceAll(new System.Random(seed), seed, report);
            report.AppendLine(ok ? "KẾT QUẢ: xong." : "KẾT QUẢ: lỗi.");
            WriteReport(report);
            return ok;
        }

        /// <summary>Xoá toàn bộ đèn khỏi nhóm <see cref="ParentName"/> trong <c>Map.prefab</c>.</summary>
        public static bool Clear()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath) == null)
            {
                Debug.LogWarning("[Lamppost] Không tìm thấy " + MapPrefabPath);
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
            try
            {
                var container = FindContainer(root);
                if (container == null)
                {
                    Debug.Log("[Lamppost] Không có nhóm \"" + ParentName + "\" nào để xoá.");
                    return true;
                }

                int count = container.transform.childCount;
                if (count == 0)
                {
                    Debug.Log("[Lamppost] Nhóm \"" + ParentName + "\" đang trống.");
                    return true;
                }

                for (int i = count - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(container.transform.GetChild(i).gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
                Debug.Log("[Lamppost] Đã xoá " + count + " đèn khỏi " + MapPrefabPath + ".");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- cắm đèn

        static bool PlaceAll(System.Random rng, int seed, StringBuilder report)
        {
            var lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (lampPrefab == null)
            {
                report.AppendLine("!! Không tìm thấy prefab " + PrefabPath);
                return false;
            }

            var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            if (mapPrefab == null)
            {
                report.AppendLine("!! Không tìm thấy prefab " + MapPrefabPath);
                return false;
            }

            float baseOffset = ComputeBaseOffset(lampPrefab);
            report.AppendLine("Prefab đèn: scale=" + lampPrefab.transform.localScale.x
                              + " | đáy đèn cách pivot " + baseOffset.ToString("0.###", CultureInfo.InvariantCulture) + "m");
            report.AppendLine("Tham số: " + LampCount + " đèn, mẫu cách điểm spawn "
                              + SampleRadiusMin.ToString("0.#", CultureInfo.InvariantCulture) + "–"
                              + SampleRadiusMax.ToString("0.#", CultureInfo.InvariantCulture)
                              + "m, cách điểm spawn ≥" + MinSpawnPointDistance.ToString("0.#", CultureInfo.InvariantCulture)
                              + "m, hai đèn cách nhau ≥" + MinLampSpacing.ToString("0.#", CultureInfo.InvariantCulture) + "m");

            // 1. mốc mồi: các điểm spawn item trong scene, và mặt đất dưới từng mốc.
            var spawnPoints = CollectSpawnPoints();
            if (spawnPoints.Count == 0)
            {
                report.AppendLine("!! Không thấy điểm spawn item nào. Mở scene \"" + GameplaySceneName
                                  + "\" (có instance Map) rồi chạy lại.");
                return false;
            }

            Physics.SyncTransforms();
            var characters = CollectCharacterPositions();
            var seedGrounds = new List<Vector3>();
            foreach (var p in spawnPoints)
            {
                Vector3 ground;
                if (TryFindGround(p, out ground))
                    seedGrounds.Add(ground);
            }
            report.AppendLine();
            report.AppendLine("Mốc mồi: " + seedGrounds.Count + "/" + spawnPoints.Count + " điểm spawn item dò được mặt đất"
                              + " | nhân vật có CharacterController: " + characters.Count);
            if (seedGrounds.Count == 0)
            {
                report.AppendLine("!! Không dò được mặt đất nào — collider của map chưa có trong scene đang mở. "
                                  + "Mở scene \"" + GameplaySceneName + "\" rồi chạy lại.");
                return false;
            }

            // 2. bốc ngẫu nhiên từng chỗ đặt đèn.
            var spots = new List<Spot>();
            float spacing = MinLampSpacing;
            for (int i = 0; i < LampCount; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < MaxAttemptsPerLamp && !found; attempt++)
                {
                    if (attempt > 0 && attempt % 500 == 0 && spacing > MinLampSpacingFloor)
                        spacing -= 2f;

                    var origin = seedGrounds[rng.Next(seedGrounds.Count)];
                    double angle = rng.NextDouble() * Math.PI * 2.0;
                    float radius = (float)(SampleRadiusMin + rng.NextDouble() * (SampleRadiusMax - SampleRadiusMin));
                    float x = origin.x + radius * (float)Math.Cos(angle);
                    float z = origin.z + radius * (float)Math.Sin(angle);

                    Vector3 ground;
                    if (!TrySampleSpot(x, z, origin.y, out ground))
                        continue;

                    if (MinDistanceToSpawnPoints(ground, spawnPoints) < MinSpawnPointDistance)
                        continue;

                    if (MinDistanceToCharacters(ground, characters) < MinCharacterDistance)
                        continue;

                    float nearest = float.MaxValue;
                    foreach (var s in spots)
                        nearest = Mathf.Min(nearest, XZDistance(ground, s.ground));
                    if (nearest < spacing)
                        continue;

                    spots.Add(new Spot(ground, nearest, rng.NextDouble() * 360.0));
                    found = true;
                }

                if (!found)
                    report.AppendLine("  #" + (i + 1).ToString("00", CultureInfo.InvariantCulture)
                                      + " BỎ QUA: không tìm được chỗ hợp lệ sau "
                                      + MaxAttemptsPerLamp.ToString(CultureInfo.InvariantCulture) + " lần thử");
            }

            if (spots.Count == 0)
            {
                report.AppendLine("!! Không đặt được đèn nào.");
                return false;
            }

            // 3. ghi vào Map.prefab (không tạo object nào trong scene).
            var sceneMap = FindMapInstanceInScene(mapPrefab);
            if (sceneMap == null)
            {
                report.AppendLine("!! Không thấy instance nào của " + MapPrefabPath + " trong scene đang mở, nên không "
                                  + "quy đổi được toạ độ về prefab. Mở scene \"" + GameplaySceneName + "\" rồi chạy lại.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
            int placed = 0;
            try
            {
                // Nhóm đèn trong scene chính là khung quy chiếu: scene dựng map bằng chuỗi transform này,
                // nên ghi localPosition theo khung đó là đèn luôn nằm đúng chỗ đã dò, bất kể instance Map
                // trong scene có override transform gì.
                var sceneContainer = FindContainer(sceneMap);
                var container = FindOrCreateContainer(
                    root, Quaternion.Inverse(sceneMap.transform.rotation));

                report.AppendLine();
                report.AppendLine("Vị trí cắm đèn:");
                for (int i = container.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(container.transform.GetChild(i).gameObject);

                if (sceneContainer == null)
                    report.AppendLine("  (prefab chưa có nhóm \"" + ParentName + "\", tool tạo mới)");

                for (int i = 0; i < spots.Count; i++)
                {
                    var spot = spots[i];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, root.scene);
                    instance.name = string.Format(CultureInfo.InvariantCulture, "Lamppost_{0:00}", i + 1);
                    instance.transform.SetParent(container.transform, false);

                    var pivot = spot.ground + Vector3.up * baseOffset;
                    var worldRotation = Quaternion.Euler(0f, (float)spot.yaw, 0f) * lampPrefab.transform.rotation;
                    if (sceneContainer != null)
                    {
                        instance.transform.localPosition = sceneContainer.transform.InverseTransformPoint(pivot);
                        instance.transform.localRotation =
                            Quaternion.Inverse(sceneContainer.transform.rotation) * worldRotation;
                    }
                    else
                    {
                        instance.transform.localPosition = pivot - sceneMap.transform.position;
                        instance.transform.localRotation = worldRotation;
                    }

                    placed++;
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  #{0:00} ({1}, {2})  mặt đất y={3}  yaw={4}  → pivot ({5}, {6}, {7})  cách điểm spawn gần nhất {8}  đèn gần nhất {9}",
                        i + 1, pivot.x, pivot.z, spot.ground.y, spot.yaw.ToString("0.#", CultureInfo.InvariantCulture),
                        pivot.x, pivot.y, pivot.z,
                        MinDistanceToSpawnPoints(spot.ground, spawnPoints).ToString("0.0", CultureInfo.InvariantCulture),
                        (spot.nearestLamp < float.MaxValue ? spot.nearestLamp.ToString("0.0", CultureInfo.InvariantCulture) : "—")));
                }

                PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            report.AppendLine();
            report.AppendLine("Đã đặt " + placed + "/" + LampCount + " đèn dưới object cha \"" + ParentName
                              + "\" trong " + MapPrefabPath + " (seed " + seed.ToString(CultureInfo.InvariantCulture) + ").");
            return true;
        }

        struct Spot
        {
            public readonly Vector3 ground;
            public readonly float nearestLamp;
            public readonly double yaw;

            public Spot(Vector3 ground, float nearestLamp, double yaw)
            {
                this.ground = ground;
                this.nearestLamp = nearestLamp;
                this.yaw = yaw;
            }
        }

        // ---------------------------------------------------------------- dò chỗ đặt

        /// <summary>Một chỗ đặt hợp lệ: đất phẳng cùng cao độ, thân đèn trống cả trên lẫn quanh.</summary>
        static bool TrySampleSpot(float x, float z, float seedGroundY, out Vector3 ground)
        {
            ground = Vector3.zero;
            RaycastHit hit;
            var origin = new Vector3(x, seedGroundY + ProbeFromHighUp, z);
            if (!Physics.Raycast(origin, Vector3.down, out hit, ProbeFromHighUp + ProbeBelowAnchor,
                                 ~0, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.normal.y < MinGroundNormalY)
                return false;

            if (Mathf.Abs(hit.point.y - seedGroundY) > SameLevelTolerance)
                return false;

            // Không có gì chắn phía trên thân đèn (trần nhà, mái, cầu thang...).
            var aboveOrigin = new Vector3(x, hit.point.y + ClearanceAbove, z);
            if (Physics.Raycast(aboveOrigin, Vector3.down, ClearanceAbove - 0.2f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            // Thân đèn không đụng tường / vật cản.
            if (Physics.OverlapSphere(hit.point + Vector3.up * 1.2f, ClearanceRadius, ~0,
                                      QueryTriggerInteraction.Ignore).Length > 0)
                return false;

            ground = hit.point;
            return true;
        }

        /// <summary>Dò mặt đất dưới một mốc: ưu tiên dò quanh độ cao mong đợi, dự phòng dò từ trên cao.</summary>
        static bool TryFindGround(Vector3 anchor, out Vector3 ground)
        {
            ground = Vector3.zero;
            RaycastHit hit;

            var origin = new Vector3(anchor.x, anchor.y + ProbeAboveAnchor, anchor.z);
            if (Physics.Raycast(origin, Vector3.down, out hit, ProbeAboveAnchor + ProbeBelowAnchor, ~0, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= MinGroundNormalY)
            {
                ground = hit.point;
                return true;
            }

            origin = new Vector3(anchor.x, anchor.y + ProbeFromHighUp, anchor.z);
            if (Physics.Raycast(origin, Vector3.down, out hit, ProbeFromHighUp + ProbeBelowAnchor, ~0, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= MinGroundNormalY)
            {
                ground = hit.point;
                return true;
            }

            return false;
        }

        static float MinDistanceToSpawnPoints(Vector3 ground, List<Vector3> spawnPoints)
        {
            float best = float.MaxValue;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                float dx = spawnPoints[i].x - ground.x;
                float dz = spawnPoints[i].z - ground.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best)
                    best = d;
            }
            return best;
        }

        /// <summary>Chỗ đứng của các nhân vật có CharacterController trong scene (player, monster).</summary>
        static List<Vector3> CollectCharacterPositions()
        {
            var positions = new List<Vector3>();
            foreach (var controller in UnityEngine.Object.FindObjectsByType<CharacterController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (controller != null)
                    positions.Add(controller.transform.position);
            }
            return positions;
        }

        static float MinDistanceToCharacters(Vector3 ground, List<Vector3> characters)
        {
            if (characters == null || characters.Count == 0)
                return float.MaxValue;
            return MinDistanceToSpawnPoints(ground, characters);
        }

        static float XZDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------------------------------------------------------------- scene / prefab

        /// <summary>Các điểm spawn item của <c>ItemManager</c>; nếu không thấy thì quét theo tên PointN.</summary>
        static List<Vector3> CollectSpawnPoints()
        {
            var points = new List<Vector3>();

            var manager = UnityEngine.Object.FindFirstObjectByType<ItemManager>(FindObjectsInactive.Include);
            if (manager != null && manager.spawnPoints != null)
            {
                foreach (var point in manager.spawnPoints)
                    if (point != null)
                        points.Add(point.position);
            }

            if (points.Count > 0)
                return points;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (IsSpawnPointName(t.name))
                            points.Add(t.position);
                }
            }

            return points;
        }

        static bool IsSpawnPointName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Point", StringComparison.Ordinal) || name.Length <= 5)
                return false;

            for (int i = 5; i < name.Length; i++)
                if (!char.IsDigit(name[i]))
                    return false;

            return true;
        }

        /// <summary>Instance của prefab Map trong scene đang mở (dùng để quy đổi toạ độ thế giới ↔ prefab).</summary>
        static GameObject FindMapInstanceInScene(GameObject mapPrefab)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetPrefabInstanceStatus(root) != PrefabInstanceStatus.Connected)
                    continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (source == mapPrefab)
                    return root;

                if (source != null && AssetDatabase.GetAssetPath(source) == MapPrefabPath)
                    return root;
            }

            return null;
        }

        static GameObject FindContainer(GameObject mapRoot)
        {
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == ParentName)
                    return t.gameObject;

            return null;
        }

        /// <summary>
        /// Nhóm đèn trong prefab; chưa có thì tạo mới và xoay sao cho trong scene nhóm này trùng khung
        /// thế giới (<paramref name="fallbackLocalRotation"/> = nghịch đảo hướng của instance Map).
        /// </summary>
        static GameObject FindOrCreateContainer(GameObject mapRoot, Quaternion fallbackLocalRotation)
        {
            var found = FindContainer(mapRoot);
            if (found != null)
                return found;

            var created = new GameObject(ParentName);
            created.transform.SetParent(mapRoot.transform, false);
            created.transform.localRotation = fallbackLocalRotation;
            return created;
        }

        /// <summary>Khoảng cách từ pivot của prefab xuống điểm thấp nhất của model (đáy đèn).</summary>
        static float ComputeBaseOffset(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return 0f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return prefab.transform.position.y - bounds.min.y;
        }

        static void WriteReport(StringBuilder report)
        {
            try
            {
                var path = Path.Combine(ProjectRoot, ReportRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Lamppost] Không ghi được report: " + e.Message);
            }
        }
    }
}
