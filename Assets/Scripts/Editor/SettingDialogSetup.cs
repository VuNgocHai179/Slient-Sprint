using SilentSprint.UI.Dialogs;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Cắm hai ô tick bật/tắt tiếng của bảng cài đặt (<see cref="SettingDialog"/>) vào prefab
    /// <c>Assets/Prefabs/UI/DlgSetting.prefab</c>.
    ///
    /// Cách dùng: menu <b>Tools ▸ Silent Sprint ▸ Set Up Setting Dialog</b>. Tool chạy ngay trong editor
    /// (không cần Play) và KHÔNG đụng vào scene đang mở: nó mở prefab bằng
    /// <see cref="PrefabUtility.LoadPrefabContents"/>, gán rồi lưu lại — cùng cách với <c>LamppostSetup</c>.
    /// Bấm nhiều lần cũng được.
    ///
    /// Tool gán theo CHỮ của hàng, không theo thứ tự hàng:
    ///   row_2 (chữ "Music")        → ô tick nhạc nền, icon đổi giữa MusicOn / MusicOff
    ///   row_1 (chữ "Sound Effect") → ô tick tiếng hiệu ứng, icon đổi giữa SoundOn / SoundOff
    ///
    /// Đồng thời tool <b>sửa lại icon đang lắp lệch</b> trong prefab: bản gốc để hàng chữ "Sound Effect" cắm
    /// icon nốt nhạc (MusicOn) và hàng chữ "Music" cắm icon cái loa (SoundOn). Sau khi chạy tool, icon trong
    /// prefab khớp với chữ, và lúc chạy thì icon đổi qua lại theo ô tick.
    /// </summary>
    public static class SettingDialogSetup
    {
        const string MenuRoot = "Tools/Silent Sprint/";
        const string PrefabPath = "Assets/Prefabs/UI/DlgSetting.prefab";
        const string SpriteFolder = "Assets/Sprites/";

        // Hai hàng nằm trong box_content; tên hàng đặt theo CHỮ hiển thị, xem SettingDialog.
        const string ContentPath = "root/box_content";
        const string MusicRowName = "row_2";
        const string SoundRowName = "row_1";

        const string MusicOnFile = "MusicOn.png";
        const string MusicOffFile = "MusicOff.png";
        const string SoundOnFile = "SoundOn.png";
        const string SoundOffFile = "SoundOff.png";

        [MenuItem(MenuRoot + "Set Up Setting Dialog", false, 230)]
        public static void SetUpFromMenu()
        {
            SetUp();
        }

        [MenuItem(MenuRoot + "Set Up Setting Dialog", true)]
        static bool ValidateSetUpFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        }

        /// <summary>Gán tham chiếu và sprite vào prefab. Trả về true nếu đã lưu prefab.</summary>
        public static bool SetUp()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                SettingDialog dialog = root.GetComponent<SettingDialog>();
                if (dialog == null)
                {
                    Debug.LogError("[SettingDialog] Prefab " + PrefabPath + " không có component SettingDialog.");
                    return false;
                }

                Transform content = root.transform.Find(ContentPath);
                Transform musicRow = content != null ? content.Find(MusicRowName) : null;
                Transform soundRow = content != null ? content.Find(SoundRowName) : null;

                if (musicRow == null || soundRow == null)
                {
                    Debug.LogError("[SettingDialog] Không thấy " + ContentPath + "/" + MusicRowName + " hoặc " +
                                   ContentPath + "/" + SoundRowName + " trong prefab. Cấu trúc prefab đã đổi — " +
                                   "sửa lại hằng số đường dẫn trong SettingDialogSetup.");
                    return false;
                }

                dialog.toggleMusic = FindInRow<Toggle>(musicRow, "check_box");
                dialog.toggleSound = FindInRow<Toggle>(soundRow, "check_box");
                dialog.iconMusic = FindInRow<Image>(musicRow, "icon");
                dialog.iconSound = FindInRow<Image>(soundRow, "icon");

                if (dialog.toggleMusic == null || dialog.toggleSound == null ||
                    dialog.iconMusic == null || dialog.iconSound == null)
                {
                    Debug.LogError("[SettingDialog] Trong mỗi hàng phải có \"check_box\" (Toggle) và \"icon\" (Image). " +
                                   "Kiểm tra lại cấu trúc prefab.");
                    return false;
                }

                dialog.musicOnSprite = LoadSprite(MusicOnFile);
                dialog.musicOffSprite = LoadSprite(MusicOffFile);
                dialog.soundOnSprite = LoadSprite(SoundOnFile);
                dialog.soundOffSprite = LoadSprite(SoundOffFile);

                // Icon lúc chưa chạy game: đúng cặp của từng hàng, để nhìn trong editor đã thấy khớp chữ.
                if (dialog.musicOnSprite != null) dialog.iconMusic.sprite = dialog.musicOnSprite;
                if (dialog.soundOnSprite != null) dialog.iconSound.sprite = dialog.soundOnSprite;

                EditorUtility.SetDirty(dialog);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                Debug.Log("[SettingDialog] Đã gán vào " + PrefabPath + ".\n" +
                          "  • Hàng \"" + RowText(musicRow) + "\" (" + MusicRowName + "): ô tick nhạc nền, icon " +
                          MusicOnFile + " / " + MusicOffFile + "\n" +
                          "  • Hàng \"" + RowText(soundRow) + "\" (" + SoundRowName + "): ô tick tiếng hiệu ứng " +
                          "(kể cả tiếng mưa), icon " + SoundOnFile + " / " + SoundOffFile + "\n" +
                          "  • Trạng thái tắt/bật tiếng nhớ bằng PlayerPrefs nên giữ nguyên qua các lần mở game; " +
                          "lúc chạy, AudioManager đọc lại trạng thái đó nên bấm ở bảng chính là màn chơi cũng im theo.");

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static T FindInRow<T>(Transform row, string childName) where T : Component
        {
            Transform child = row.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        /// <summary>Chữ đang hiển thị trên hàng, để báo lại cho người dùng đối chiếu đã gán đúng hàng chưa.</summary>
        static string RowText(Transform row)
        {
            Transform text = row.Find("text");
            var label = text != null ? text.GetComponent<TMP_Text>() : null;

            return label != null ? label.text : "?";
        }

        static Sprite LoadSprite(string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + fileName);

            if (sprite == null)
                Debug.LogWarning("[SettingDialog] Không thấy " + SpriteFolder + fileName + " nên ô này sẽ để trống.");

            return sprite;
        }
    }
}
