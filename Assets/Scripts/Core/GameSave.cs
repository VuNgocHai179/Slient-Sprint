using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilentSprint.Core
{
    /// <summary>Một điểm spawn mảnh đá tự do: còn bật không, và còn mảnh đá ở đó không.</summary>
    [Serializable]
    public class SpawnPointState
    {
        public string name;
        public bool active;
        public bool hasStone;
    }

    /// <summary>
    /// Toàn bộ tiến trình của một ván chơi trong màn Gameplay, ghi xuống file JSON.
    ///
    /// Cố tình chỉ chứa dữ liệu thuần (không tham chiếu scene) để đọc lại được ở lần chạy sau.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public int version = GameSave.Version;
        public string sceneName = SceneNames.Gameplay;

        // Tiến trình
        public int freedomStones;
        public int maxFreedomStones;
        public int questItems;
        public int maxQuestItems;

        /// <summary>Các mini game đã thắng, lưu theo <c>(int)MinigameKind</c>.</summary>
        public int[] completedMinigames = new int[0];

        // Người chơi
        public Vector3 playerPosition;
        public Quaternion playerRotation = Quaternion.identity;

        // Mảnh đá tự do trên map (chỗ nào còn đá, chỗ nào đã nhặt)
        public List<SpawnPointState> spawnPoints = new List<SpawnPointState>();
    }

    /// <summary>
    /// Tầng file của hệ lưu game: đọc/ghi/xoá một file JSON trong <see cref="Application.persistentDataPath"/>.
    ///
    /// Không biết gì về màn chơi. Việc chụp và khôi phục trạng thái là của
    /// <c>SilentSprint.Gameplay.GameplaySaveManager</c>; còn nút Continue ở bảng chính chỉ cần
    /// <see cref="HasSave"/> và <see cref="QueueLoad"/>.
    /// </summary>
    public static class GameSave
    {
        /// <summary>Tăng số này khi đổi cấu trúc <see cref="GameSaveData"/>; bản lưu cũ sẽ bị bỏ qua.</summary>
        public const int Version = 1;

        private const string FileName = "silentsprint-save.json";

        /// <summary>Dữ liệu đang chờ khôi phục khi vào màn chơi (nút Continue đặt vào đây).</summary>
        public static GameSaveData Pending { get; private set; }

        /// <summary>File lưu nằm ở đâu (tiện cho việc xem/sửa tay khi thử nghiệm).</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Đang có ván để chơi tiếp: có file lưu và đọc được.</summary>
        public static bool HasSave
        {
            get
            {
                if (Pending != null) return true;
                return File.Exists(FilePath) && Read() != null;
            }
        }

        /// <summary>Ván này được vào bằng Continue nên phải khôi phục tiến trình cũ.</summary>
        public static bool IsRestoring => Pending != null;

        /// <summary>Đọc file lưu và để dành cho màn chơi khôi phục ở frame đầu.</summary>
        public static void QueueLoad()
        {
            Pending = Read();

            if (Pending == null)
            {
                Debug.LogWarning("[GameSave] Không đọc được file lưu nên màn chơi sẽ bắt đầu lại từ đầu.");
            }
        }

        /// <summary>Quên dữ liệu đang chờ (màn chơi đã khôi phục xong, hoặc ván mới đã bắt đầu).</summary>
        public static void ClearPending()
        {
            Pending = null;
        }

        public static void Write(GameSaveData data)
        {
            if (data == null) return;

            try
            {
                data.version = Version;
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] Không ghi được file lưu '{FilePath}': {exception.Message}");
            }
        }

        /// <summary>Xoá bản lưu (ván mới, hoặc người chơi bị tóm).</summary>
        public static void Delete()
        {
            Pending = null;

            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] Không xoá được file lưu '{FilePath}': {exception.Message}");
            }
        }

        private static GameSaveData Read()
        {
            if (!File.Exists(FilePath)) return null;

            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(FilePath));

                if (data == null)
                {
                    Debug.LogWarning($"[GameSave] File lưu '{FilePath}' rỗng hoặc hỏng.");
                    return null;
                }

                if (data.version != Version)
                {
                    Debug.LogWarning(
                        $"[GameSave] Bản lưu version {data.version} không khớp version {Version} của game nên bị bỏ qua, " +
                        "ván mới sẽ ghi đè lên.");
                    return null;
                }

                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] Không đọc được file lưu '{FilePath}': {exception.Message}");
                return null;
            }
        }
    }
}
