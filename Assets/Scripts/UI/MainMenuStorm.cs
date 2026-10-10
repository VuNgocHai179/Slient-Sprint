using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SilentSprint.UI
{
    /// <summary>
    /// Mưa xối xả kèm sấm chớp cho bảng chính. Toàn bộ hiệu ứng dựng bằng UI trên Canvas sẵn có
    /// của scene: không cần camera riêng, không ParticleSystem, không thêm material.
    ///
    /// Vì sao không dùng prefab "Rain Particles" có sẵn trong project: prefab đó là ParticleSystem
    /// đặt trong không gian 3D, mà bảng chính là Canvas Screen Space - Overlay nên hạt mưa sẽ bị
    /// tấm nền che mất; bộ texture đi kèm cũng chỉ có sprite tròn (giọt nước) chứ không có vệt mưa
    /// dọc. Ở đây vệt mưa được sinh ngay trong code: một texture dọc mờ dần hai đầu, rồi nhân bản
    /// thành nhiều Image rơi xuống với tốc độ / độ dài / độ mờ ngẫu nhiên nên mắt không thấy lặp.
    ///
    /// Cách gắn: đặt component này lên một GameObject là con CUỐI của bảng chính (nằm trên các nút)
    /// để mưa phủ kín màn hình và tia chớp loé sáng cả bảng — xem scene Assets/Scenes/MainMenu.unity,
    /// object "StormEffect". Các vệt mưa đều để raycastTarget = false nên không chắn chuột của nút.
    /// </summary>
    [AddComponentMenu("Silent Sprint/Main Menu Storm")]
    [DisallowMultipleComponent]
    public sealed class MainMenuStorm : MonoBehaviour
    {
        private const int StreakTextureWidth = 16;
        private const int StreakTextureHeight = 160;

        [Header("Mưa")]
        [Tooltip("Số vệt mưa cùng lúc. Tăng cho mưa dày hơn; mỗi vệt là một Image nên 200 vệt vẫn nhẹ.")]
        [Min(0)] public int dropCount = 170;

        [Tooltip("Độ nghiêng của mưa (độ). 0 là rơi thẳng đứng, 12-18 cho cảm giác mưa gió.")]
        [Range(0f, 30f)] public float slantDegrees = 14f;

        [Tooltip("Chiều dài vệt mưa: x = ngắn nhất, y = dài nhất (pixel theo mốc 1080p).")]
        public Vector2 lengthRange = new Vector2(90f, 280f);

        [Tooltip("Bề ngang vệt mưa: x = mảnh nhất, y = dày nhất (pixel theo mốc 1080p).")]
        public Vector2 widthRange = new Vector2(2f, 5f);

        [Tooltip("Tốc độ rơi: x = chậm nhất, y = nhanh nhất (pixel mỗi giây).")]
        public Vector2 speedRange = new Vector2(1100f, 2100f);

        [Tooltip("Độ mờ của từng vệt: x = mờ nhất, y = rõ nhất. Hạ xuống nếu chữ trên bảng khó đọc.")]
        public Vector2 opacityRange = new Vector2(0.1f, 0.4f);

        [Tooltip("Màu vệt mưa. Ngả xanh lạnh cho giống mưa đêm.")]
        public Color dropColor = new Color(0.73f, 0.83f, 1f, 1f);

        [Header("Sấm chớp")]
        [Tooltip("Tắt đi thì chỉ còn mưa, không có tia chớp nào.")]
        public bool lightningEnabled = true;

        [Tooltip("Khoảng cách giữa hai tia chớp: x = ngắn nhất, y = dài nhất (giây).")]
        public Vector2 lightningIntervalRange = new Vector2(6f, 18f);

        [Tooltip("Độ sáng mạnh nhất của tia chớp (0-1).")]
        [Range(0f, 1f)] public float flashPeakAlpha = 0.5f;

        [Tooltip("Thời gian loé lên (giây). Để thật ngắn mới ra cảm giác chớp.")]
        [Min(0.01f)] public float flashRiseTime = 0.04f;

        [Tooltip("Thời gian tắt dần sau khi loé (giây).")]
        [Min(0.01f)] public float flashFallTime = 0.45f;

        [Tooltip("Xác suất mỗi lần chớp có thêm một nháy phụ ngay sau đó (kiểu sấm nhiều hồi).")]
        [Range(0f, 1f)] public float doubleFlashChance = 0.4f;

        [Tooltip("Màu tia chớp.")]
        public Color flashColor = new Color(0.82f, 0.88f, 1f, 1f);

        private Sprite streakSprite;
        private RectTransform panel;
        private RectTransform rainLayer;
        private RainDrop[] drops;
        private Image flash;
        private Coroutine lightningRoutine;

        /// <summary>
        /// Một vệt mưa đang rơi. x/y tính theo tỉ lệ 0-1 của màn hình (1 = mép trên), còn length /
        /// width / speed tính theo pixel của mốc 1080p.
        /// </summary>
        private struct RainDrop
        {
            public RectTransform rect;
            public Image image;
            public float x;
            public float y;
            public float speed;
            public float length;
            public float width;
            public float opacity;
        }

        private void Awake()
        {
            if (!(transform is RectTransform self))
            {
                Debug.LogWarning("[MainMenuStorm] Component này phải nằm trên một object UI (có RectTransform).", this);
                enabled = false;
                return;
            }

            // Tự giãn kín màn hình để không phụ thuộc vào cách kéo thả trong Editor.
            panel = self;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;

            streakSprite = CreateStreakSprite();

            BuildRainLayer();
            BuildFlash();

            if (lightningEnabled)
            {
                lightningRoutine = StartCoroutine(LightningLoop());
            }
        }

        private void OnDestroy()
        {
            if (lightningRoutine != null)
            {
                StopCoroutine(lightningRoutine);
                lightningRoutine = null;
            }

            if (streakSprite != null)
            {
                // Texture sinh trong code thì phải tự dọn, không thì nó ở lại suốt phiên chơi.
                if (streakSprite.texture != null) Destroy(streakSprite.texture);
                Destroy(streakSprite);
                streakSprite = null;
            }
        }

        private void Update()
        {
            if (drops == null || drops.Length == 0 || rainLayer == null) return;

            // Dùng thời gian thật: bảng tạm dừng có thể đang để timeScale = 0.
            float deltaTime = Time.unscaledDeltaTime;
            float height = Mathf.Max(1f, panel.rect.height);
            float width = Mathf.Max(1f, panel.rect.width);

            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].y -= drops[i].speed * deltaTime / height;

                // Rơi hết màn hình thì đưa lên trên mép và bốc lại thông số, để mưa không bao giờ cạn.
                if (drops[i].y < -drops[i].length / height)
                {
                    ConfigureDrop(ref drops[i], 1f + drops[i].length / height + Random.Range(0f, 0.4f));
                }

                drops[i].rect.anchoredPosition = new Vector2(
                    (drops[i].x - 0.5f) * width,
                    (drops[i].y - 0.5f) * height);
            }
        }

        private void BuildRainLayer()
        {
            var layerObject = new GameObject("RainLayer", typeof(RectTransform));
            rainLayer = (RectTransform)layerObject.transform;
            rainLayer.SetParent(transform, false);
            rainLayer.anchorMin = Vector2.zero;
            rainLayer.anchorMax = Vector2.one;
            rainLayer.anchoredPosition = Vector2.zero;
            rainLayer.sizeDelta = Vector2.zero;

            // Nghiêng cả lớp mưa: từng vệt vẫn rơi thẳng trong hệ trục của lớp nên nhìn ra thành mưa xiên.
            rainLayer.localRotation = Quaternion.Euler(0f, 0f, -slantDegrees);

            drops = new RainDrop[Mathf.Max(0, dropCount)];

            for (int i = 0; i < drops.Length; i++)
            {
                var dropObject = new GameObject("drop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)dropObject.transform;
                rect.SetParent(rainLayer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.localRotation = Quaternion.identity;

                var image = dropObject.GetComponent<Image>();
                image.sprite = streakSprite;
                image.raycastTarget = false; // mưa không được chắn chuột của các nút
                image.type = Image.Type.Simple;

                drops[i].rect = rect;
                drops[i].image = image;

                // Rải sẵn khắp màn hình ngay frame đầu để không phải chờ mưa "rơi vào".
                ConfigureDrop(ref drops[i], Random.Range(-0.05f, 1.15f));
            }
        }

        private void ConfigureDrop(ref RainDrop drop, float startY)
        {
            drop.x = Random.Range(0f, 1f);
            drop.y = startY;
            drop.speed = Pick(speedRange);
            drop.length = Pick(lengthRange);
            drop.width = Pick(widthRange);
            drop.opacity = Pick(opacityRange);

            drop.rect.sizeDelta = new Vector2(drop.width, drop.length);
            drop.image.color = new Color(dropColor.r, dropColor.g, dropColor.b, drop.opacity);
        }

        private void BuildFlash()
        {
            var flashObject = new GameObject("LightningFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)flashObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling(); // tia chớp phải phủ lên trên cả mưa

            flash = flashObject.GetComponent<Image>();
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            flash.raycastTarget = false;

            flashObject.SetActive(false);
        }

        private IEnumerator LightningLoop()
        {
            // Tia đầu tiên đến sớm để vừa vào bảng là thấy hiệu ứng, sau đó mới thưa dần.
            yield return new WaitForSecondsRealtime(Random.Range(1.5f, 3.5f));

            while (true)
            {
                yield return Strike(flashRiseTime, flashFallTime, flashPeakAlpha);

                if (Random.value < doubleFlashChance)
                {
                    yield return new WaitForSecondsRealtime(Random.Range(0.06f, 0.18f));
                    yield return Strike(flashRiseTime, flashFallTime * 0.6f, flashPeakAlpha * 0.75f);
                }

                yield return new WaitForSecondsRealtime(Pick(lightningIntervalRange));
            }
        }

        private IEnumerator Strike(float riseTime, float fallTime, float peakAlpha)
        {
            if (flash == null) yield break;

            flash.gameObject.SetActive(true);

            yield return FadeFlash(0f, peakAlpha, riseTime);
            yield return FadeFlash(peakAlpha, 0f, fallTime);

            flash.gameObject.SetActive(false);
        }

        private IEnumerator FadeFlash(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetFlashAlpha(to);
                yield break;
            }

            SetFlashAlpha(from);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFlashAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetFlashAlpha(to);
        }

        private void SetFlashAlpha(float alpha)
        {
            if (flash == null) return;

            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
        }

        /// <summary>
        /// Sinh texture cho một vệt mưa: dọc, sáng nhất ở giữa và mờ dần về hai đầu, hai bên hông
        /// cũng mờ dần nên không thấy cạnh. Chỉ sinh một lần rồi mọi vệt dùng chung (một draw call).
        /// </summary>
        private static Sprite CreateStreakSprite()
        {
            var texture = new Texture2D(StreakTextureWidth, StreakTextureHeight, TextureFormat.RGBA32, false)
            {
                name = "RainStreak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[StreakTextureWidth * StreakTextureHeight];

            for (int y = 0; y < StreakTextureHeight; y++)
            {
                float vertical = Mathf.Sin(y / (StreakTextureHeight - 1f) * Mathf.PI);
                vertical = Mathf.Pow(vertical, 0.55f);

                for (int x = 0; x < StreakTextureWidth; x++)
                {
                    float offset = (x + 0.5f) / StreakTextureWidth * 2f - 1f;
                    float horizontal = Mathf.Exp(-(offset * offset) * 8f);

                    byte alpha = (byte)(Mathf.Clamp01(horizontal * vertical) * 255f);
                    pixels[y * StreakTextureWidth + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, StreakTextureWidth, StreakTextureHeight),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }

        /// <summary>Bốc một giá trị ngẫu nhiên trong khoảng (x, y); nhập ngược thứ tự vẫn chạy đúng.</summary>
        private static float Pick(Vector2 range)
        {
            return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
