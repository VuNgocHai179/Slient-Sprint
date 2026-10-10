using SilentSprint.Core;
using SilentSprint.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Cắm đoạn kết Kamehameha (<see cref="KamehamehaEnding"/>) vào scene <b>Gameplay</b>.
    ///
    /// Cách dùng: mở scene Gameplay rồi menu <b>Tools ▸ Silent Sprint ▸ Set Up Kamehameha Gate Ending</b>,
    /// sau đó Ctrl+S để lưu scene. Tool chỉ tạo và gán tham chiếu, không sinh thêm gì khác, nên bấm nhiều
    /// lần cũng được: lần sau dùng lại đúng object cũ chứ không nhân đôi.
    ///
    /// Vì sao làm tool thay vì sửa tay file .unity: đoạn kết cần tham chiếu tới prefab hiệu ứng tụ chiêu.
    /// Gán qua AssetDatabase thì không phải đoán fileID trong YAML, và cũng không đụng vào file scene khi
    /// Editor đang mở (ghi đè file scene lúc này dễ làm mất thay đổi chưa lưu).
    /// </summary>
    public static class KamehamehaEndingSetup
    {
        const string MenuRoot = "Tools/Silent Sprint/";
        const string ObjectName = "KamehamehaEnding";
        const string GameplaySceneName = "Gameplay";
        const string ChargeVfxPath = "Assets/Prefabs/kamehameha_charging.prefab";
        const string GateName = "Gate";

        [MenuItem(MenuRoot + "Set Up Kamehameha Gate Ending", false, 210)]
        public static void SetUpFromMenu()
        {
            SetUp();
        }

        [MenuItem(MenuRoot + "Set Up Kamehameha Gate Ending", true)]
        static bool ValidateSetUpFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && SceneManager.GetActiveScene().name == GameplaySceneName;
        }

        /// <summary>
        /// Tạo (hoặc dùng lại) object đoạn kết trong scene đang mở rồi gán các tham chiếu cần thiết.
        /// Các ô còn lại (tư thế tụ chiêu, góc máy, nhịp cảnh phim, chữ trên bảng phá đảo) đã có giá trị
        /// mặc định trong script, chỉnh tiếp trong Inspector nếu muốn.
        /// </summary>
        /// <returns>Component đoạn kết trong scene, hoặc null nếu scene đang mở không phải Gameplay.</returns>
        public static KamehamehaEnding SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != GameplaySceneName)
            {
                Debug.LogWarning("[KamehamehaEnding] Hãy mở scene \"" + GameplaySceneName + "\" rồi chạy lại tool này " +
                                 "(scene đang mở: \"" + scene.name + "\").");
                return null;
            }

            KamehamehaEnding ending = FindExisting(scene);
            bool created = false;

            if (ending == null)
            {
                var gameObject = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Kamehameha ending");

                ending = Undo.AddComponent<KamehamehaEnding>(gameObject);
                created = true;
            }

            GameObject chargeVfx = AssetDatabase.LoadAssetAtPath<GameObject>(ChargeVfxPath);

            if (chargeVfx == null)
                Debug.LogWarning("[KamehamehaEnding] Không thấy " + ChargeVfxPath +
                                 " nên quả cầu tụ sẽ tạm dùng hình cầu mặc định của Unity.");

            ending.chargeVfxPrefab = chargeVfx;
            ending.gateObjectName = GateName;

            // Cổng nằm trong Map.prefab: gán vào sẽ thành tham chiếu chéo sang một instance của prefab
            // (prefab được sửa lại là tham chiếu hỏng), nên phần lớn trường hợp để trống và script tự dò
            // theo tên lúc chạy. Chỉ gán khi tìm được cổng đứng riêng trong scene.
            ending.gate = FindGateOutsidePrefab();

            ending.endingScene = SceneNames.MainMenu;

            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = ending.gameObject;

            Debug.Log("[KamehamehaEnding] " + (created ? "Đã tạo object" : "Đã cập nhật object") + " \"" + ObjectName +
                      "\" trong scene " + GameplaySceneName + ". Nhớ Ctrl+S để lưu scene.\n" +
                      "  • Cổng: " + (ending.gate != null ? ending.gate.name : "chưa gán — sẽ tự dò object tên \"" + GateName + "\" lúc chạy") + "\n" +
                      "  • Hiệu ứng tụ chiêu: " + (chargeVfx != null ? ChargeVfxPath : "trống") + "\n" +
                      "  • Tư thế nhân vật: ô Charge Pose đang để trống nên code tự dựng dáng (hai nhịp Charge Beat / Thrust Beat); " +
                      "gắn clip tư thế kamehameha vào đó khi có thì clip được ưu tiên.\n" +
                      "  • Góc máy: ô \"Giữ Nguyên Góc Máy\" đang " + (ending.keepCameraStill ? "BẬT" : "TẮT") +
                      " — bật thì camera đứng yên đúng góc nó đang có suốt cảnh phim (không cắt góc, không rung). " +
                      "Tắt ô đó thì cảnh phim chạy ba góc máy: Charge Shot (dùng đúng góc đã ngắm) và Fire Shot " +
                      "(đứng ngang, tia lao ra khỏi khung) lấy theo trục người chơi; Impact Shot (góc cổng đổ) neo " +
                      "vào TRỤC CỔNG — đứng ở đầu cổng rồi nhìn dọc theo cổng xuống chỗ nó sập " +
                      "(Gate Offset / Gate Look Offset).\n" +
                      "  • Cảnh kết luôn đứng trong map: người chơi được dời về cách tâm cổng " + ending.standDistance +
                      "m về phía trong, phía trong lấy theo hướng khu tường bao (object \"" + ending.mapWallsObjectName + "\").\n" +
                      "  • Số trong Inspector còn là bộ CŨ? Unity giữ giá trị ĐÃ GHI của component đang mở, code đổi số " +
                      "mặc định cũng không tự ghi đè: chuột phải vào tiêu đề component \"KamehamehaEnding\" ▸ " +
                      "\"Đặt lại góc cổng đổ theo mặc định\" (chỉ đụng góc cổng đổ) hoặc \"Đặt lại ba góc máy theo mặc định\".");

            return ending;
        }

        /// <summary>Object đoạn kết đã có trong scene chưa (bấm tool lần hai thì dùng lại, không nhân đôi).</summary>
        static KamehamehaEnding FindExisting(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != ObjectName) continue;

                KamehamehaEnding ending = root.GetComponent<KamehamehaEnding>();
                if (ending != null) return ending;
            }

            return Object.FindFirstObjectByType<KamehamehaEnding>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Cổng đứng riêng trong scene (không nằm trong instance của prefab nào). Trả về null khi không có —
        /// lúc đó <see cref="KamehamehaEnding"/> tự dò theo tên.
        /// </summary>
        static Transform FindGateOutsidePrefab()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != GateName) continue;
                    if (PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject) != null) continue;

                    return child;
                }
            }

            return null;
        }
    }
}
