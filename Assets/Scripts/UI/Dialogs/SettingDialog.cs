using SilentSprint.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SilentSprint.UI.Dialogs
{
    /// <summary>
    /// Bảng cài đặt (prefab DlgSetting): nút đóng + hai ô tick bật/tắt tiếng.
    ///
    /// Hai hàng trong prefab đặt tên theo CHỮ trên hàng chứ không theo thứ tự hàng:
    ///  - hàng chữ <b>Music</b> (object <c>row_2</c>)        → nhạc nền
    ///  - hàng chữ <b>Sound Effect</b> (object <c>row_1</c>) → tiếng hiệu ứng (jumpscare, kamehameha, tiếng chạy)
    ///    và tiếng mưa, vì mưa là tiếng nền chứ không phải nhạc.
    /// (Bản gốc của prefab lắp icon lệch so với chữ: hàng chữ "Sound Effect" lại cắm icon nốt nhạc và hàng chữ
    /// "Music" lại cắm icon cái loa. Tool cài đặt sửa lại icon cho khớp chữ.)
    ///
    /// Bảng này chỉ nói chuyện với <see cref="AudioManager"/> qua hàm tĩnh, nên mở được ở cả bảng chính lẫn màn
    /// chơi và cả khi scene chưa có object AudioManager: trạng thái tắt/bật tiếng nằm ở biến tĩnh + PlayerPrefs
    /// (nhớ qua các lần mở game), object AudioManager của scene sau đọc lại lúc chạy.
    ///
    /// Gắn tham chiếu bằng menu <b>Tools ▸ Silent Sprint ▸ Set Up Setting Dialog</b> (xem <c>SettingDialogSetup</c>).
    /// Ô tham chiếu để trống cũng vẫn chạy: <see cref="ResolveReferences"/> tự dò theo đường dẫn trong prefab,
    /// chỉ có sprite là phải có người gán (không nằm trong Resources nên không tự nạp được).
    /// </summary>
    public class SettingDialog : DialogView
    {
        // Đường dẫn trong prefab, dùng khi ô tham chiếu để trống (chưa chạy tool gắn).
        private const string MusicRowPath = "root/box_content/row_2";
        private const string SoundRowPath = "root/box_content/row_1";

        [Header("Nút của bảng")]
        public Button btnClose;

        [Header("Ô tick bật/tắt tiếng")]
        [Tooltip("Ô tick của hàng chữ \"Music\" — tắt thì im nhạc nền.")]
        public Toggle toggleMusic;

        [Tooltip("Ô tick của hàng chữ \"Sound Effect\" — tắt thì im tiếng hiệu ứng và tiếng mưa.")]
        public Toggle toggleSound;

        [Header("Icon đổi theo ô tick")]
        [Tooltip("Icon của hàng chữ \"Music\". Bật tiếng thì hiện Music On Sprite, tắt thì hiện Music Off Sprite.")]
        public Image iconMusic;

        [Tooltip("Icon của hàng chữ \"Sound Effect\". Bật tiếng thì hiện Sound On Sprite, tắt thì hiện Sound Off Sprite.")]
        public Image iconSound;

        [Header("Sprite của hai trạng thái")]
        public Sprite musicOnSprite;
        public Sprite musicOffSprite;
        public Sprite soundOnSprite;
        public Sprite soundOffSprite;

        private void Awake()
        {
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(Hide);
            }
            else
            {
                Debug.LogWarning("[SettingDialog] Chưa gán nút đóng nên bảng sẽ không tắt được.", this);
            }

            ResolveReferences();

            Bind(toggleMusic, OnMusicToggled);
            Bind(toggleSound, OnSoundToggled);
        }

        private void OnEnable()
        {
            // Đổi trạng thái ở đâu (kể cả lần sau mở bảng) cũng vẽ lại icon, nên hai nơi bấm không bao giờ
            // hiển thị lệch nhau.
            AudioManager.MuteChanged += RefreshIcons;
            Refresh();
        }

        private void OnDisable()
        {
            AudioManager.MuteChanged -= RefreshIcons;
        }

        /// <summary>Mở lại bảng sau khi đã tắt thì đọc lại trạng thái đang nhớ, kẻo ô tick còn giá trị cũ.</summary>
        protected override void OnShown()
        {
            Refresh();
        }

        /// <summary>
        /// Đưa hai ô tick và hai icon về đúng trạng thái đang nhớ. Dùng SetIsOnWithoutNotify để không gọi
        /// ngược lại chính việc tắt/bật tiếng vừa đọc ra.
        /// </summary>
        private void Refresh()
        {
            if (toggleMusic != null) toggleMusic.SetIsOnWithoutNotify(!AudioManager.MusicMuted);
            if (toggleSound != null) toggleSound.SetIsOnWithoutNotify(!AudioManager.SoundMuted);

            RefreshIcons();
        }

        private void OnMusicToggled(bool on)
        {
            AudioManager.SetMusicMuted(!on);
        }

        private void OnSoundToggled(bool on)
        {
            AudioManager.SetSoundMuted(!on);
        }

        /// <summary>
        /// Icon của mỗi hàng đổi theo ô tick của hàng đó: bật tiếng thì icon "On", tắt thì icon "Off".
        /// Ô tick ở giữa hàng tự hiện/ẩn dấu tick rồi (Toggle.graphic), phần này chỉ lo icon bên trái.
        /// </summary>
        private void RefreshIcons()
        {
            ApplyIcon(iconMusic, AudioManager.MusicMuted, musicOnSprite, musicOffSprite);
            ApplyIcon(iconSound, AudioManager.SoundMuted, soundOnSprite, soundOffSprite);
        }

        private static void ApplyIcon(Image icon, bool muted, Sprite onSprite, Sprite offSprite)
        {
            if (icon == null) return;

            Sprite wanted = muted ? offSprite : onSprite;
            if (wanted != null) icon.sprite = wanted;
        }

        /// <summary>
        /// Dò ô tick và icon theo đường dẫn trong prefab khi ô tham chiếu để trống — để bảng vẫn chạy được
        /// nếu quên chạy tool gắn.
        /// </summary>
        private void ResolveReferences()
        {
            Transform musicRow = transform.Find(MusicRowPath);
            Transform soundRow = transform.Find(SoundRowPath);

            if (toggleMusic == null) toggleMusic = FindInRow<Toggle>(musicRow, "check_box");
            if (toggleSound == null) toggleSound = FindInRow<Toggle>(soundRow, "check_box");

            if (iconMusic == null) iconMusic = FindInRow<Image>(musicRow, "icon");
            if (iconSound == null) iconSound = FindInRow<Image>(soundRow, "icon");
        }

        private static T FindInRow<T>(Transform row, string childName) where T : Component
        {
            if (row == null) return null;

            Transform child = row.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void Bind(Toggle toggle, UnityAction<bool> action)
        {
            if (toggle == null) return;

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(action);
        }
    }
}
