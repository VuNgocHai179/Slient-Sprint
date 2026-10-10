using UnityEngine;

namespace SilentSprint.Core
{
    /// <summary>
    /// Ép game chạy ở hướng NGANG trên thiết bị di động.
    ///
    /// Vì sao cần cả script này lẫn Player Settings: Player Settings (Default Orientation = <b>Landscape Left</b>,
    /// hai ô Portrait trong "Allowed Orientations for Auto Rotation" đã tắt) mới là thứ Unity ghi vào
    /// AndroidManifest.xml của bản build, tức hướng đúng ngay từ màn hình splash. Nhưng nếu máy đang bật khoá
    /// xoay — hoặc bản build được dựng từ setting cũ — hệ điều hành vẫn có thể giữ nguyên hướng dọc, nên đoạn
    /// dưới đây chốt lại một lần nữa lúc app vừa bật, trước khi scene đầu tiên chạy.
    ///
    /// Không cần gắn vào scene nào: <see cref="RuntimeInitializeOnLoadMethodAttribute"/> tự gọi hàm này đúng một
    /// lần khi app khởi động, dù vào game từ scene nào.
    ///
    /// Muốn cho phép xoay cả hai chiều ngang thay vì khoá đúng một chiều, đổi dòng cuối thành
    /// <c>Screen.orientation = ScreenOrientation.AutoRotation;</c> — hai cờ ngang bên trên đã bật sẵn, và trên
    /// máy đang bật khoá xoay thì Android vẫn cho xoay trong phạm vi hai chiều ngang.
    /// </summary>
    public static class ScreenOrientationLock
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyLandscapeOnly()
        {
            // Bản build trên PC không bị ảnh hưởng, và trong Editor thì đặt hướng chỉ làm nhiễu Game view.
            if (!Application.isMobilePlatform) return;

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
