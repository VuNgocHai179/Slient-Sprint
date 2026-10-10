using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Bộ hiệu ứng của chiêu Kamehameha trong đoạn kết: quả cầu tụ ở hai bàn tay, tia bắn tới cổng và
    /// choé sáng ở chỗ tia trúng.
    ///
    /// Quả cầu lấy từ prefab hiệu ứng của project (<c>kamehameha_charging.prefab</c>, phần "Sphere UV 1_0");
    /// tia thì tự dựng bằng một khối trụ, vì prefab chỉ có quả cầu với một quầng năng lượng nằm lệch hẳn ra
    /// ngoài, không có khối tia dài. Vật liệu của tia mượn luôn vật liệu của quả cầu, nhờ vậy lúc chạy không
    /// phải <c>Shader.Find</c> một shader của URP (shader không nằm trong build là ra màu hồng).
    ///
    /// Chỗ trúng chỉ có choé sáng do code tạo, không dùng prefab vụ nổ nào: cổng đổ xuống kèm choé sáng và
    /// camera rung đã đủ đọc được cú trúng, mà lại không phải kéo thêm một asset to vào build.
    ///
    /// Nhịp nhàng do <see cref="KamehamehaEnding"/> lái; lớp này chỉ nhận lệnh và tự giữ trạng thái hình ảnh.
    /// Không dùng DOTween: JumpscareController gọi DOTween.KillAll() mỗi lần chơi lại, tween đang chạy
    /// giữa đoạn kết sẽ chết giữa chừng.
    /// </summary>
    public class KamehamehaBeam : MonoBehaviour
    {
        [Tooltip("Tia ngắn hơn mức này thì kéo dài cho còn nhìn thấy được (m).")]
        public float minBeamLength = 0.5f;

        private Transform anchor;
        private GameObject orbInstance;
        private Transform orbNode;
        private Vector3 orbBaseScale;
        private Vector3 lastAnchorPosition;
        private float orbPeakScale = 0.45f;
        private float chargeLightIntensity = 50f;
        private Light orbLight;

        private GameObject beamObject;
        private float beamRadius = 0.35f;

        private static readonly Color EnergyColor = new Color(0.65f, 0.88f, 1f);

        /// <summary>
        /// Dựng quả cầu tụ ở đúng hai bàn tay. Prefab có thể để trống — lúc đó tự dựng một quả cầu bằng
        /// hình cầu mặc định để đoạn kết vẫn chạy được thay vì lặng lẽ không có gì.
        /// </summary>
        /// <param name="chargePrefab">Prefab hiệu ứng (<c>kamehameha_charging.prefab</c>).</param>
        /// <param name="handsAnchor">Object nằm giữa hai bàn tay người chơi; quả cầu bám theo object này.</param>
        /// <param name="orbPartName">Tên object chứa quả cầu trong prefab.</param>
        /// <param name="auraPartName">Tên object chứa quầng trong prefab (được tắt đi, xem phần giải thích trong code).</param>
        /// <param name="peakScale">Hệ số lớn nhất của quả cầu so với cỡ gốc trong prefab.</param>
        /// <param name="radius">Bán kính tia (m).</param>
        /// <param name="lightIntensity">Cường độ đèn của quả cầu khi tụ xong.</param>
        public void Build(GameObject chargePrefab, Transform handsAnchor, string orbPartName, string auraPartName,
                          float peakScale, float radius, float lightIntensity)
        {
            anchor = handsAnchor;
            orbPeakScale = Mathf.Max(0.05f, peakScale);
            beamRadius = Mathf.Max(0.02f, radius);
            chargeLightIntensity = Mathf.Max(0f, lightIntensity);

            if (chargePrefab != null)
            {
                orbInstance = Instantiate(chargePrefab, transform);
                orbInstance.name = "KamehamehaCharge";
                orbNode = FindDeep(orbInstance.transform, orbPartName);

                // Quầng của prefab nằm lệch hẳn ra ngoài gốc (model gốc dựng cả một cảnh chứ không chỉ
                // quả cầu), để nguyên thì có một đám sáng lơ lửng cách người chơi cả chục mét -> tắt đi.
                Transform aura = FindDeep(orbInstance.transform, auraPartName);
                if (aura != null && aura != orbNode) aura.gameObject.SetActive(false);
            }

            if (orbNode == null)
            {
                orbInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orbInstance.name = "KamehamehaOrb";
                Destroy(orbInstance.GetComponent<Collider>());
                orbInstance.transform.SetParent(transform, false);
                orbNode = orbInstance.transform;
            }

            orbBaseScale = orbNode.localScale;

            // Đặt quả cầu đúng vào hai bàn tay: tâm hình của cả cụm phải trùng mốc bàn tay, không phải gốc prefab.
            if (orbInstance != null && anchor != null) CenterOn(orbInstance, anchor.position);
            lastAnchorPosition = anchor != null ? anchor.position : Vector3.zero;

            if (anchor != null)
            {
                var lightObject = new GameObject("KamehamehaLight");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.position = anchor.position;

                orbLight = lightObject.AddComponent<Light>();
                orbLight.type = LightType.Point;
                orbLight.color = EnergyColor;
                orbLight.range = 20f;
                orbLight.intensity = 0f;
            }

            SetOrbScale(0f);
        }

        private void Update()
        {
            // Quả cầu bám theo bàn tay: gắn clip tư thế tụ chiêu thì tay cử động và quả cầu đi theo,
            // chứ không đứng im một chỗ.
            if (orbInstance == null || anchor == null) return;

            orbInstance.transform.position += anchor.position - lastAnchorPosition;
            lastAnchorPosition = anchor.position;

            if (orbLight != null) orbLight.transform.position = anchor.position;
        }

        /// <summary>Quả cầu to dần theo nhịp tụ (0 → 1), kèm rung nhẹ như đang dồn năng lượng.</summary>
        public void SetOrbScale(float progress)
        {
            float k = Mathf.Clamp01(progress);

            if (orbNode != null)
            {
                float grow = Mathf.Lerp(0.08f, orbPeakScale, k);
                float tremble = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 45f) * k;
                orbNode.localScale = orbBaseScale * (grow * tremble);
            }

            if (orbLight != null) orbLight.intensity = chargeLightIntensity * k;
            if (orbInstance != null && orbInstance.activeSelf != (k > 0.001f)) orbInstance.SetActive(k > 0.001f);
        }

        /// <summary>Bật tia từ hai bàn tay tới chỗ trúng; tia tự dài đúng bằng khoảng cách đó.</summary>
        public void AimBeam(Vector3 from, Vector3 to)
        {
            if (beamObject == null) beamObject = CreateBeam();
            if (beamObject == null) return;

            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.000001f) return;

            Vector3 axis = direction.normalized;
            float length = Mathf.Max(minBeamLength, direction.magnitude);

            beamObject.transform.position = from + axis * (length * 0.5f);
            beamObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            // Khối trụ gốc của Unity cao 2 đơn vị, nên muốn dài `length` thì hệ số theo trục Y là length/2.
            beamObject.transform.localScale = new Vector3(beamRadius * 2f, length * 0.5f, beamRadius * 2f);
            beamObject.SetActive(true);
        }

        /// <summary>Choé sáng ở chỗ tia trúng, rồi tự tắt dần.</summary>
        public void SpawnImpact(Vector3 position, float lightIntensity)
        {
            var lightObject = new GameObject("KamehamehaImpactLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = position;

            Light flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = EnergyColor;
            flash.range = 45f;
            flash.intensity = Mathf.Max(0f, lightIntensity);

            StartCoroutine(FadeLight(flash, 1.1f));
        }

        /// <summary>Tắt hiệu ứng sau khi bắn: quả cầu và tia teo lại rồi biến mất.</summary>
        public IEnumerator FadeOut(float duration)
        {
            float time = Mathf.Max(0.01f, duration);

            Vector3 orbFrom = orbNode != null ? orbNode.localScale : Vector3.zero;
            Vector3 beamFrom = beamObject != null ? beamObject.transform.localScale : Vector3.zero;
            float lightFrom = orbLight != null ? orbLight.intensity : 0f;

            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                float left = 1f - Mathf.Clamp01(elapsed / time);

                if (orbNode != null) orbNode.localScale = orbFrom * left;
                if (beamObject != null)
                    beamObject.transform.localScale = new Vector3(beamFrom.x * left, beamFrom.y, beamFrom.z * left);
                if (orbLight != null) orbLight.intensity = lightFrom * left;

                yield return null;
            }

            if (beamObject != null) beamObject.SetActive(false);
            if (orbInstance != null) orbInstance.SetActive(false);
            if (orbLight != null) orbLight.intensity = 0f;
        }

        /// <summary>Dọn sạch: xoá các object hiệu ứng rồi xoá chính component này.</summary>
        public void Dispose()
        {
            if (orbInstance != null) Destroy(orbInstance);
            if (beamObject != null) Destroy(beamObject);
            if (orbLight != null) Destroy(orbLight.gameObject);

            Destroy(this);
        }

        // ------------------------------------------------------------------ dựng tia

        private GameObject CreateBeam()
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "KamehamehaBeam";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(transform, false);

            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Tia phát sáng: đổ bóng chỉ tổ làm cảnh tối om có thêm một vệt đen.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ResolveBeamMaterial();
            }

            beam.SetActive(false);
            return beam;
        }

        /// <summary>
        /// Vật liệu cho tia: mượn vật liệu của quả cầu trong prefab hiệu ứng (đã chắc chắn có trong build vì
        /// prefab được tham chiếu). Không mượn được thì tự pha một vật liệu phát sáng bằng shader có sẵn
        /// của Unity, chứ không đi tìm shader của URP.
        /// </summary>
        private Material ResolveBeamMaterial()
        {
            if (orbInstance != null)
            {
                Renderer source = orbInstance.GetComponentInChildren<Renderer>(true);
                if (source != null && source.sharedMaterial != null) return source.sharedMaterial;
            }

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var material = new Material(shader);
            material.color = new Color(0.55f, 0.85f, 1f, 1f);
            return material;
        }

        private IEnumerator FadeLight(Light light, float duration)
        {
            float from = light != null ? light.intensity : 0f;
            float time = Mathf.Max(0.01f, duration);

            float elapsed = 0f;
            while (elapsed < time && light != null)
            {
                elapsed += Time.unscaledDeltaTime;
                light.intensity = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / time));
                yield return null;
            }

            if (light != null) Destroy(light.gameObject);
        }

        // ------------------------------------------------------------------ tiện ích

        /// <summary>Tìm object con theo tên, kể cả object đang tắt.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;

            return null;
        }

        /// <summary>Dời một object vừa tạo sao cho tâm hình của nó nằm đúng <paramref name="target"/>.</summary>
        private static void CenterOn(GameObject instance, Vector3 target)
        {
            if (instance == null) return;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            instance.transform.position += target - bounds.center;
        }
    }
}
