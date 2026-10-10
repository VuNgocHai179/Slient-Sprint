using System.Collections.Generic;
using System.Text;
using SilentSprint.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Cửa sổ tinh chỉnh ánh sáng ban đêm cho scene <b>Gameplay</b>.
    ///
    /// Vì sao cần: map là thị trấn về đêm nhưng scene chỉ có 1 đèn Directional bị đặt màu ĐEN
    /// (intensity 0.1) nên không chiếu gì cả; ambient lại cắm cứng 1 màu xám (mode Flat) nên bầu trời
    /// sao không đóng góp gì; sương mù màu gần đen, dày (0.06) nên nhìn xa là mất hút; và không có
    /// bước color grading nào. Kết quả: bản đồ tối, khó nhìn, lại không nhất quán về ánh sáng.
    ///
    /// Cửa sổ này giữ nguyên chất kinh dị nhưng chia sáng thành 3 tầng: trăng (key) + ambient xanh lạnh
    /// (fill) + vũng sáng đèn đường/cửa sổ (neo định hướng), rồi mở vùng tối bằng post-exposure.
    ///
    /// Cách dùng: mở scene Gameplay, vào menu <b>Tools ▸ Silent Sprint ▸ Night Lighting Tuner</b>.
    /// Bấm "Đọc lại từ scene" để lấy giá trị hiện tại, kéo slider (xem được ngay cả khi đang Play), rồi
    /// "Áp vào scene" và "Lưu". Nút "Khớp hướng trăng" chỉnh đèn trăng trùng hướng mặt trăng đang vẽ
    /// trong skybox, còn "Gán kính cho Map.prefab" tìm đúng mesh kính của Map.glb và gán
    /// <c>Mat_Glass_Night</c>.
    ///
    /// Lưu ý: nếu bấm "Áp vào scene" rồi mà không thấy đổi gì, kiểm tra xem object <c>Scene Fog</c> có
    /// component <see cref="SceneFog"/> không — component này tự ghi đè RenderSettings trong Awake(),
    /// nên cửa sổ này luôn cập nhật cả hai nơi cho khớp.
    /// </summary>
    public class NightLightingTuner : EditorWindow
    {
        // ------------------------------------------------------------------ đường dẫn cố định

        const string MenuPath = "Tools/Silent Sprint/Night Lighting Tuner";
        const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string LamppostPath = "Assets/Prefabs/lamppost.prefab";
        const string MapPrefabPath = "Assets/Prefabs/Map.prefab";
        const string GlassMaterialPath = "Assets/Material/Mat_Glass_Night.mat";
        const string SsaoFeatureName = "ScreenSpaceAmbientOcclusion";

        // ------------------------------------------------------------------ preset A "đêm có trăng"

        static readonly Color PresetMoonColor = new Color(0.62f, 0.72f, 1f, 1f);
        const float PresetMoonIntensity = 0.35f;

        const int PresetAmbientMode = 1; // 1 = Trilight/Gradient
        static readonly Color PresetAmbientSky = new Color(0.16f, 0.17f, 0.22f, 1f);
        static readonly Color PresetAmbientEquator = new Color(0.1f, 0.11f, 0.14f, 1f);
        static readonly Color PresetAmbientGround = new Color(0.05f, 0.05f, 0.06f, 1f);
        const float PresetAmbientIntensity = 1f;

        static readonly Color PresetFogColor = new Color(0.1f, 0.13f, 0.18f, 1f);
        const float PresetFogDensity = 0.022f;
        const int PresetFogMode = (int)FogMode.ExponentialSquared;

        const float PresetPostExposure = 0.35f;
        const float PresetContrast = 6f;
        const float PresetSaturation = 10f;
        const float PresetShadowLift = 0.012f;
        const float PresetVignette = 0.12f;
        const float PresetBloomThreshold = 0.85f;
        const float PresetBloomIntensity = 0.4f;
        const float PresetSsaoIntensity = 0.3f;
        const float PresetReflectionIntensity = 0.55f;

        static readonly Color PresetLampColor = new Color(1f, 0.87f, 0.7f, 1f);
        const float PresetLampIntensity = 3f;
        const float PresetLampRange = 14f;

        // ------------------------------------------------------------------ bảng chọn enum (YAML lưu số, UI hiện chữ)

        static readonly int[] AmbientModeValues = { 0, 1, 3 }; // Skybox, Trilight/Gradient, Flat
        static readonly string[] AmbientModeLabels = { "Skybox", "Gradient (Trilight)", "Flat (1 màu)" };
        static readonly int[] FogModeValues = { 1, 2, 3 }; // Linear, Exponential, ExponentialSquared
        static readonly string[] FogModeLabels = { "Linear", "Exponential", "Exponential Squared" };

        static int ModeToIndex(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return i;
            }

            return values.Length - 1;
        }

        // ------------------------------------------------------------------ trạng thái trên UI

        Light moonLight;
        SceneFog sceneFog;

        Color moonColor = PresetMoonColor;
        float moonIntensity = PresetMoonIntensity;

        int ambientMode = PresetAmbientMode;
        Color ambientSky = PresetAmbientSky;
        Color ambientEquator = PresetAmbientEquator;
        Color ambientGround = PresetAmbientGround;
        float ambientIntensity = PresetAmbientIntensity;

        bool fogEnabled = true;
        int fogMode = PresetFogMode;
        Color fogColor = PresetFogColor;
        float fogDensity = PresetFogDensity;

        float postExposure = PresetPostExposure;
        float contrast = PresetContrast;
        float saturation = PresetSaturation;
        float shadowLift = PresetShadowLift;
        float vignette = PresetVignette;
        float bloomThreshold = PresetBloomThreshold;
        float bloomIntensity = PresetBloomIntensity;
        float ssaoIntensity = PresetSsaoIntensity;
        float reflectionIntensity = PresetReflectionIntensity;

        Color lampColor = PresetLampColor;
        float lampIntensity = PresetLampIntensity;
        float lampRange = PresetLampRange;

        Vector2 scroll;
        string status = "Bấm \"Đọc lại từ scene\" hoặc \"Đặt về preset A\" để bắt đầu.";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<NightLightingTuner>("Night Lighting");
            window.minSize = new Vector2(400f, 560f);
            window.ReadFromScene();
            window.Show();
        }

        void OnEnable()
        {
            ReadFromScene();
        }

        // ------------------------------------------------------------------ đọc / ghi scene

        /// <summary>Tìm đèn Directional đang dùng làm mặt trời (fallback: đèn Directional đầu tiên).</summary>
        static Light FindMoonLight()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            return null;
        }

        static SceneFog FindSceneFog()
        {
            foreach (var fog in Object.FindObjectsByType<SceneFog>(FindObjectsSortMode.None))
                return fog;

            return null;
        }

        /// <summary>Lấy toàn bộ giá trị đang có trong scene lên UI (không sửa gì).</summary>
        public void ReadFromScene()
        {
            moonLight = FindMoonLight();
            if (moonLight != null)
            {
                moonColor = moonLight.color;
                moonIntensity = moonLight.intensity;
            }

            ambientMode = (int)RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity;

            fogEnabled = RenderSettings.fog;
            fogMode = (int)RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;

            reflectionIntensity = RenderSettings.reflectionIntensity;

            sceneFog = FindSceneFog();
            if (sceneFog != null)
            {
                // Component SceneFog ghi đè RenderSettings trong Awake() nên nó mới là "nguồn sự thật".
                fogEnabled = sceneFog.fogEnabled;
                fogMode = (int)sceneFog.mode;
                fogColor = sceneFog.color;
                fogDensity = sceneFog.density;
            }

            ReadPostProcessing();
            ReadSsao();
            ReadLamp();

            status = moonLight == null
                ? "CẢNH BÁO: scene không có đèn Directional nào → không có key light."
                : $"Đã đọc từ scene. Đèn trăng: {moonLight.name} (intensity {moonIntensity:0.###}).";
        }

        void ReadPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
                return;

            if (profile.TryGet<ColorAdjustments>(out var color))
            {
                postExposure = color.postExposure.value;
                contrast = color.contrast.value;
                saturation = color.saturation.value;
            }

            if (profile.TryGet<LiftGammaGain>(out var liftGammaGain))
                shadowLift = liftGammaGain.lift.value.w;

            if (profile.TryGet<Vignette>(out var vignetteComponent))
                vignette = vignetteComponent.intensity.value;

            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloomThreshold = bloom.threshold.value;
                bloomIntensity = bloom.intensity.value;
            }
        }

        float ReadSsao()
        {
            var feature = FindSsaoFeature();
            var prop = FindSsaoIntensityProperty(feature);
            if (prop != null)
                ssaoIntensity = prop.floatValue;

            return ssaoIntensity;
        }

        void ReadLamp()
        {
            var root = PrefabUtility.LoadPrefabContents(LamppostPath);
            if (root == null)
                return;

            try
            {
                var light = root.GetComponentInChildren<Light>(true);
                if (light != null)
                {
                    lampColor = light.color;
                    lampIntensity = light.intensity;
                    lampRange = light.range;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static ScreenSpaceAmbientOcclusion FindSsaoFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
                return null;

            foreach (var feature in data.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion ssao && feature.name == SsaoFeatureName)
                    return ssao;
            }

            foreach (var feature in data.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion ssao)
                    return ssao;
            }

            return null;
        }

        /// <summary>Lấy property "Intensity" trong struct m_Settings (field private) qua SerializedObject.</summary>
        static SerializedProperty FindSsaoIntensityProperty(ScreenSpaceAmbientOcclusion feature)
        {
            if (feature == null)
                return null;

            var settings = new SerializedObject(feature).FindProperty("m_Settings");
            return settings?.FindPropertyRelative("Intensity");
        }

        // ------------------------------------------------------------------ áp giá trị

        void ApplyMoon()
        {
            moonLight = FindMoonLight();
            if (moonLight == null)
            {
                status = "Không có đèn Directional trong scene → bỏ qua phần trăng.";
                return;
            }

            Undo.RecordObject(moonLight, "Night Lighting");
            moonLight.color = moonColor;
            moonLight.intensity = moonIntensity;
            moonLight.useColorTemperature = false; // tắt để màu trăng không bị trộn thêm nhiệt độ màu
            EditorUtility.SetDirty(moonLight);

            RenderSettings.sun = moonLight;
        }

        void ApplyAmbient()
        {
            RenderSettings.ambientMode = (AmbientMode)ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.reflectionIntensity = reflectionIntensity;
        }

        void ApplyFog()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = (FogMode)fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            sceneFog = FindSceneFog();
            if (sceneFog == null)
                return;

            Undo.RecordObject(sceneFog, "Night Lighting");
            sceneFog.fogEnabled = fogEnabled;
            sceneFog.mode = (FogMode)fogMode;
            sceneFog.color = fogColor;
            sceneFog.density = fogDensity;
            sceneFog.Apply(); // ghi lại xuống RenderSettings, khớp đúng thứ Awake() sẽ làm lúc chạy
            EditorUtility.SetDirty(sceneFog);
        }

        void ApplyPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                status = $"Không thấy {ProfilePath} → không áp được post-processing.";
                return;
            }

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.overrideState = true;
            color.postExposure.value = postExposure;
            color.contrast.overrideState = true;
            color.contrast.value = contrast;
            color.saturation.overrideState = true;
            color.saturation.value = saturation;

            var liftGammaGain = GetOrAdd<LiftGammaGain>(profile);
            liftGammaGain.active = true;
            liftGammaGain.lift.overrideState = true;
            liftGammaGain.lift.value = new Vector4(1f, 1f, 1f, shadowLift);

            var vignetteComponent = GetOrAdd<Vignette>(profile);
            vignetteComponent.active = true;
            vignetteComponent.intensity.overrideState = true;
            vignetteComponent.intensity.value = vignette;

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = bloomIntensity;

            EditorUtility.SetDirty(profile);
        }

        void ApplySsao()
        {
            var feature = FindSsaoFeature();
            var prop = FindSsaoIntensityProperty(feature);
            if (prop == null)
            {
                status = "Không tìm thấy feature ScreenSpaceAmbientOcclusion trong PC_Renderer.";
                return;
            }

            var so = prop.serializedObject;
            prop.floatValue = ssaoIntensity;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data != null)
                EditorUtility.SetDirty(data);
        }

        void ApplyLamp()
        {
            var root = PrefabUtility.LoadPrefabContents(LamppostPath);
            if (root == null)
            {
                status = $"Không mở được {LamppostPath}.";
                return;
            }

            try
            {
                var light = root.GetComponentInChildren<Light>(true);
                if (light == null)
                {
                    status = "lamppost.prefab không có Light nào.";
                    return;
                }

                light.color = lampColor;
                light.intensity = lampIntensity;
                light.range = lampRange;
                PrefabUtility.SaveAsPrefabAsset(root, LamppostPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);
        }

        // ------------------------------------------------------------------ nút chức năng

        void ApplyAll()
        {
            ApplyMoon();
            ApplyAmbient();
            ApplyFog();
            ApplyPostProcessing();
            ApplySsao();
            ApplyLamp();

            AssetDatabase.SaveAssets();
            MarkSceneDirty();
            Repaint();
            status = "Đã áp toàn bộ thông số vào scene/prefab. Nhớ Ctrl+S để Unity lưu scene, hoặc bấm \"Lưu Assets\".";
        }

        void ApplyPresetA()
        {
            moonColor = PresetMoonColor;
            moonIntensity = PresetMoonIntensity;

            ambientMode = PresetAmbientMode;
            ambientSky = PresetAmbientSky;
            ambientEquator = PresetAmbientEquator;
            ambientGround = PresetAmbientGround;
            ambientIntensity = PresetAmbientIntensity;

            fogEnabled = true;
            fogMode = PresetFogMode;
            fogColor = PresetFogColor;
            fogDensity = PresetFogDensity;

            postExposure = PresetPostExposure;
            contrast = PresetContrast;
            saturation = PresetSaturation;
            shadowLift = PresetShadowLift;
            vignette = PresetVignette;
            bloomThreshold = PresetBloomThreshold;
            bloomIntensity = PresetBloomIntensity;
            ssaoIntensity = PresetSsaoIntensity;
            reflectionIntensity = PresetReflectionIntensity;

            lampColor = PresetLampColor;
            lampIntensity = PresetLampIntensity;
            lampRange = PresetLampRange;

            ApplyAll();
            MatchMoonDirection();
            status = "Đã đặt về preset A (đêm có trăng — nhìn rõ đường) và khớp hướng trăng với skybox.";
        }

        /// <summary>
        /// Xoay đèn Directional trùng hướng mặt trăng mà <c>StarrySky.shader</c> đang vẽ.
        /// Công thức hướng trăng trong shader: (cos(el)·sin(az), sin(el), cos(el)·cos(az)) — đèn phải
        /// chiếu ngược lại hướng đó, nên forward của đèn = -moonDir.
        /// </summary>
        void MatchMoonDirection()
        {
            float azimuth = 200f;
            float elevation = 40f;

            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (sky.HasProperty("_MoonAzimuth"))
                    azimuth = sky.GetFloat("_MoonAzimuth");
                if (sky.HasProperty("_MoonElevation"))
                    elevation = sky.GetFloat("_MoonElevation");
            }

            float el = elevation * Mathf.Deg2Rad;
            float az = azimuth * Mathf.Deg2Rad;
            var moonDir = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
            var forward = -moonDir;

            float pitch = -Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            moonLight = FindMoonLight();
            if (moonLight == null)
            {
                status = "Không thấy đèn Directional để khớp hướng trăng.";
                return;
            }

            Undo.RecordObject(moonLight.transform, "Match moon direction");
            moonLight.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            EditorUtility.SetDirty(moonLight.transform);
            MarkSceneDirty();

            status = $"Đèn trăng đã khớp skybox: azimuth {azimuth:0.#}°, elevation {elevation:0.#}° → euler ({pitch:0.#}, {yaw:0.#}, 0).";
        }

        /// <summary>Gán Mat_Glass_Night cho mọi renderer đang dùng material có tên chứa "glass".</summary>
        void FixGlassOnMapPrefab()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (glass == null)
            {
                status = $"Không thấy {GlassMaterialPath}.";
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
            if (root == null)
            {
                status = $"Không mở được {MapPrefabPath}.";
                return;
            }

            try
            {
                int count = AssignGlass(root, glass);
                PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
                status = count > 0
                    ? $"Đã gán Mat_Glass_Night cho {count} renderer kính trong Map.prefab."
                    : "Không tìm thấy renderer nào dùng material tên chứa \"glass\" trong Map.prefab.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        void FixGlassOnSelection()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (glass == null)
            {
                status = $"Không thấy {GlassMaterialPath}.";
                return;
            }

            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Fix glass");
                count += AssignGlass(go, glass);
            }

            MarkSceneDirty();
            status = count > 0
                ? $"Đã gán Mat_Glass_Night cho {count} renderer kính trong object đang chọn."
                : "Object đang chọn không có renderer nào dùng material tên chứa \"glass\".";
        }

        static int AssignGlass(GameObject root, Material glass)
        {
            int changedRenderers = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.ToLowerInvariant().Contains("glass"))
                    {
                        materials[i] = glass;
                        changed = true;
                    }
                }

                if (!changed)
                    continue;

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                changedRenderers++;
            }

            return changedRenderers;
        }

        /// <summary>In ra Console danh sách những chỗ đang lệch preset để biết cần sửa gì.</summary>
        void Diagnose()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Night Lighting] Kiểm tra scene Gameplay:");

            var light = FindMoonLight();
            if (light == null)
            {
                sb.AppendLine("  • Không có đèn Directional → map không có key light.");
            }
            else
            {
                float sum = light.color.r + light.color.g + light.color.b;
                if (sum < 0.02f)
                    sb.AppendLine($"  • Đèn '{light.name}' đang có màu gần ĐEN (intensity {light.intensity:0.###}) → không chiếu gì cả.");
                else if (light.intensity < 0.1f)
                    sb.AppendLine($"  • Đèn '{light.name}' intensity quá thấp ({light.intensity:0.###}).");
            }

            if (RenderSettings.ambientMode == AmbientMode.Flat && RenderSettings.ambientIntensity < 0.15f)
                sb.AppendLine("  • Ambient đang Flat và rất tối → bầu trời sao không đóng góp ánh sáng nào.");

            float visibility = RenderSettings.fogDensity > 0.0001f ? 1.98f / RenderSettings.fogDensity : 999f;
            if (RenderSettings.fog && visibility < 40f)
                sb.AppendLine($"  • Sương mù dày: tầm nhìn chỉ ~{visibility:0} m.");

            if (RenderSettings.fog) 
            {
                float fogLuma = RenderSettings.fogColor.r + RenderSettings.fogColor.g + RenderSettings.fogColor.b;
                if (fogLuma < 0.15f)
                    sb.AppendLine("  • Màu sương gần như đen nên vật ở xa 'biến mất' thay vì mờ dần vào nền.");
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                sb.AppendLine($"  • Không thấy {ProfilePath}.");
            }
            else if (!profile.TryGet<ColorAdjustments>(out var color) || !color.active || !color.postExposure.overrideState)
            {
                sb.AppendLine("  • Global Volume chưa có ColorAdjustments/post exposure → không có bước grade nào để mở vùng tối.");
            }

            if (FindSsaoFeature() == null)
                sb.AppendLine("  • PC_Renderer không có ScreenSpaceAmbientOcclusion (bỏ qua nếu không cần AO).");

            sb.AppendLine("  • Nhắc: ngoài ánh sáng, độ tối còn do material. Nút \"Gán kính cho Map.prefab\" chỉ thay đúng mesh kính.");
            Debug.Log(sb.ToString());

            status = "Đã in báo cáo ra Console (Window ▸ General ▸ Console).";
        }

        static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ giao diện

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Preset A = \"đêm có trăng — nhìn rõ đường\": trăng làm key light, ambient xanh lạnh làm fill, " +
                "vũng sáng đèn đường làm điểm neo, post-exposure mở vùng tối. Vẫn giữ chất kinh dị.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Đọc lại từ scene"))
                    ReadFromScene();

                if (GUILayout.Button("Đặt về preset A"))
                    ApplyPresetA();

                if (GUILayout.Button("Áp vào scene"))
                    ApplyAll();
            }

            EditorGUILayout.Space();
            DrawMoonSection();
            DrawAmbientSection();
            DrawFogSection();
            DrawPostSection();
            DrawLampSection();
            DrawGlassSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chẩn đoán", EditorStyles.boldLabel);
            if (GUILayout.Button("In báo cáo ra Console"))
                Diagnose();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        void DrawMoonSection()
        {
            EditorGUILayout.LabelField("Trăng (key light)", EditorStyles.boldLabel);
            moonColor = EditorGUILayout.ColorField("Màu trăng", moonColor);
            moonIntensity = EditorGUILayout.Slider("Intensity", moonIntensity, 0f, 2f);
            EditorGUILayout.LabelField(" ", $"Đang dùng: {(moonLight != null ? moonLight.name : "(không có đèn Directional)")}");

            if (GUILayout.Button("Khớp hướng trăng với skybox"))
                MatchMoonDirection();

            EditorGUILayout.Space();
        }

        void DrawAmbientSection()
        {
            EditorGUILayout.LabelField("Ambient (fill)", EditorStyles.boldLabel);
            ambientMode = AmbientModeValues[EditorGUILayout.Popup("Kiểu ambient", ModeToIndex(AmbientModeValues, ambientMode), AmbientModeLabels)];

            ambientSky = EditorGUILayout.ColorField("Trời (up)", ambientSky);
            ambientEquator = EditorGUILayout.ColorField("Ngang (side)", ambientEquator);
            ambientGround = EditorGUILayout.ColorField("Đất (down)", ambientGround);
            ambientIntensity = EditorGUILayout.Slider("Cường độ", ambientIntensity, 0f, 3f);
            reflectionIntensity = EditorGUILayout.Slider("Cường độ phản chiếu", reflectionIntensity, 0f, 1f);
            EditorGUILayout.Space();
        }

        void DrawFogSection()
        {
            EditorGUILayout.LabelField("Sương mù", EditorStyles.boldLabel);
            fogEnabled = EditorGUILayout.Toggle("Bật fog", fogEnabled);
            fogMode = FogModeValues[EditorGUILayout.Popup("Kiểu", ModeToIndex(FogModeValues, fogMode), FogModeLabels)];
            fogColor = EditorGUILayout.ColorField("Màu", fogColor);
            fogDensity = EditorGUILayout.Slider("Độ đặc", fogDensity, 0f, 0.12f);

            float visibility = fogDensity > 0.0001f ? 1.98f / fogDensity : 999f;
            EditorGUILayout.LabelField(" ", $"Tầm nhìn xấp xỉ: {visibility:0} m");
            EditorGUILayout.Space();
        }

        void DrawPostSection()
        {
            EditorGUILayout.LabelField("Post-processing (SampleSceneProfile)", EditorStyles.boldLabel);
            postExposure = EditorGUILayout.Slider("Post exposure (EV)", postExposure, -1f, 2f);
            contrast = EditorGUILayout.Slider("Contrast", contrast, -50f, 50f);
            saturation = EditorGUILayout.Slider("Saturation", saturation, -50f, 50f);
            shadowLift = EditorGUILayout.Slider("Nhấc vùng tối (lift)", shadowLift, 0f, 0.1f);
            vignette = EditorGUILayout.Slider("Vignette", vignette, 0f, 0.6f);
            bloomThreshold = EditorGUILayout.Slider("Bloom threshold", bloomThreshold, 0f, 2f);
            bloomIntensity = EditorGUILayout.Slider("Bloom intensity", bloomIntensity, 0f, 1.5f);

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data != null)
                ssaoIntensity = EditorGUILayout.Slider("SSAO intensity", ssaoIntensity, 0f, 1f);

            EditorGUILayout.Space();
        }

        void DrawLampSection()
        {
            EditorGUILayout.LabelField("Đèn đường (lamppost.prefab — 26 đèn trong Map)", EditorStyles.boldLabel);
            lampColor = EditorGUILayout.ColorField("Màu đèn", lampColor);
            lampIntensity = EditorGUILayout.Slider("Intensity", lampIntensity, 0f, 10f);
            lampRange = EditorGUILayout.Slider("Range", lampRange, 1f, 30f);
            EditorGUILayout.Space();
        }

        void DrawGlassSection()
        {
            EditorGUILayout.LabelField("Cửa sổ / kính", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(" ", "Mat_Glass_Night: base xanh tối, smoothness 0.4, không emission.");
            if (GUILayout.Button("Gán kính cho Map.prefab (mesh tên chứa \"glass\")"))
                FixGlassOnMapPrefab();
            if (GUILayout.Button("Gán kính cho object đang chọn trong scene"))
                FixGlassOnSelection();
            EditorGUILayout.Space();
        }
    }
}
