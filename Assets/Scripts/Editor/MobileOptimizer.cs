using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Tối ưu phần "nặng" cho bản mobile. Chạy được cả trong Editor (menu
    /// <b>Tools ▸ Silent Sprint</b>) lẫn ở chế độ batchmode:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath &lt;project&gt; \
    ///           -executeMethod SilentSprint.EditorTools.MobileOptimizer.ApplyAudioForMobile
    /// </code>
    ///
    /// <para><b>Vì sao chỉ có audio:</b> khi rà lại thì phần texture và quality tier của project
    /// <i>đã đúng sẵn</i> — mọi texture trong <c>Assets/Texture</c>, <c>Assets/Sprites</c> đều đã
    /// <c>maxTextureSize: 1024</c>, <c>isReadable: 0</c>, <c>textureCompression: 1</c> kèm override
    /// Android; <c>QualitySettings</c> đã có level tên <c>Mobile</c> và
    /// <c>m_PerPlatformDefaultQuality.Android = 0</c> trỏ đúng vào nó. Thứ duy nhất còn sai là
    /// audio: cả 11 clip đang để <c>loadType: 0</c> (DecompressOnLoad) với <c>quality: 1</c> (100%),
    /// nên <c>BG_Music.wav</c> (30,6 MB nguồn) bị giải nén nguyên thành PCM nằm thường trú trong RAM
    /// — đúng kiểu làm app bị Android kill vì hết bộ nhớ.</para>
    ///
    /// <para><b>Cách sửa:</b> clip dài (<see cref="MusicThresholdSeconds"/> giây trở lên) chuyển sang
    /// <c>Streaming</c> (chỉ đọc dần từ đĩa, gần như không giữ PCM), hạ quality Vorbis xuống
    /// <see cref="MusicQuality"/>; clip ngắn giữ <c>DecompressOnLoad</c> nhưng hạ quality xuống
    /// <see cref="SfxQuality"/> để giảm dung lượng build. Ghi vào
    /// <c>defaultSampleSettings</c> — hiện <c>platformSettingOverrides</c> của mọi clip đang rỗng
    /// nên default áp cho tất cả nền tảng, không cần override riêng.</para>
    /// </summary>
    public static class MobileOptimizer
    {
        const string MenuRoot = "Tools/Silent Sprint/";
        const string AudioFolder = "Assets/Audio";
        const string GltfFolder = "Assets/Model";

        /// <summary>Clip dài hơn mức này được coi là nhạc/nền và chuyển sang streaming.</summary>
        public const float MusicThresholdSeconds = 10f;

        /// <summary>Quality Vorbis cho nhạc/nền (%).</summary>
        public const int MusicQuality = 60;

        /// <summary>Quality Vorbis cho hiệu ứng ngắn (%).</summary>
        public const int SfxQuality = 90;

        /// <summary>
        /// Điểm vào duy nhất cho batchmode: ép nhập lại asset (để Unity nhận các <c>.glb</c> đã được
        /// thu nhỏ ngoài Editor), áp cấu hình audio cho mobile, in LocalFileID sub-asset của glTF để
        /// kiểm tra tham chiếu prefab không gãy, rồi in bảng dung lượng.
        ///
        /// <code>
        /// Unity.exe -batchmode -quit -projectPath &lt;project&gt; -logFile - ^
        ///   -executeMethod SilentSprint.EditorTools.MobileOptimizer.OptimizeForMobile
        /// </code>
        /// </summary>
        [MenuItem(MenuRoot + "Optimize Project For Mobile", false, 239)]
        public static void OptimizeForMobile()
        {
            AssetDatabase.Refresh();
            ApplyExtractedGltfTextureSettings();
            ReimportGltfAssets();
            ApplyAudioForMobile();
            DumpGltfSubAssetIds();
            ReportAssetSizes();
            AssetDatabase.SaveAssets();
            Debug.Log("[MobileOptimizer] DONE OptimizeForMobile");
        }

        [MenuItem(MenuRoot + "Apply Mobile Audio Settings", false, 240)]
        public static void ApplyAudioForMobile()
        {
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToList();

            var report = new StringBuilder();
            report.AppendLine("[MobileOptimizer] Apply audio settings for mobile");
            report.AppendLine("  file                          | len(s) | loadType         | fmt    | quality | preload | bytes");

            int changed = 0;
            long beforeBytes = 0;

            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    report.AppendLine("  skip (không phải AudioImporter): " + path);
                    continue;
                }

                long size = 0;
                try { size = new FileInfo(path).Length; } catch { /* đường dẫn ảo */ }
                beforeBytes += size;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                float length = clip != null ? clip.length : 0f;
                bool isMusic = length >= MusicThresholdSeconds;

                var s = importer.defaultSampleSettings;
                bool dirty = s.loadType != (isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad)
                             || s.compressionFormat != AudioCompressionFormat.Vorbis
                             || !Mathf.Approximately(s.quality, (isMusic ? MusicQuality : SfxQuality) / 100f)
                             || s.preloadAudioData != !isMusic;

                s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = (isMusic ? MusicQuality : SfxQuality) / 100f;
                s.preloadAudioData = !isMusic;
                importer.defaultSampleSettings = s;
                importer.loadInBackground = isMusic;
                importer.forceToMono = false;
                importer.ambisonic = false;

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }

                report.AppendLine(string.Format("  {0,-29} | {1,6:F1} | {2,-16} | {3,-6} | {4,5}% | {5,-7} | {6}",
                    Path.GetFileName(path), length, s.loadType, s.compressionFormat,
                    Mathf.RoundToInt(s.quality * 100f), s.preloadAudioData, size));
            }

            report.AppendLine(string.Format("  -> {0}/{1} clip đã đổi cấu hình; tổng file nguồn {2:F2} MB",
                changed, paths.Count, beforeBytes / 1048576f));
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// In ra LocalFileID của mọi sub-asset trong các <c>.glb</c>. Dùng để kiểm tra việc thu nhỏ
        /// texture nhúng (offline) có làm đổi ID mà prefab đang tham chiếu hay không — ví dụ
        /// <c>Compass.prefab</c> trỏ tới mesh sub-asset có LocalFileID <c>472276926024506521</c>.
        /// </summary>
        /// <summary>
        /// Chuẩn hoá import settings cho texture đã tách ra khỏi <c>.glb</c>. Texture nhúng do
        /// glTFast tạo thẳng nên không qua <c>TextureImporter</c> và không bao giờ được nén; texture
        /// nằm ngoài thì đi qua importer bình thường nên nén được.
        ///
        /// Không gian màu phải khớp glTF: bản đồ màu (basecolor/emissive) là sRGB, còn
        /// normal/metallic/roughness/occlusion là linear — glTFast cũng tạo texture nhúng với đúng
        /// quy tắc đó (<c>forceSampleLinear</c>), nên đặt sai sẽ làm sai ánh sáng.
        /// </summary>
        [MenuItem(MenuRoot + "Apply Settings To Extracted glTF Textures", false, 244)]
        public static void ApplyExtractedGltfTextureSettings()
        {
            const int MaxSize = 1024;
            var report = new StringBuilder();
            report.AppendLine("[MobileOptimizer] Texture tách từ .glb");

            int changed = 0, scanned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { GltfFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("_Textures/", System.StringComparison.Ordinal) < 0) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                scanned++;

                string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool linear = file.Contains("normal") || file.Contains("metallic")
                              || file.Contains("roughness") || file.Contains("metalrough")
                              || file.Contains("occlusion");

                bool dirty = false;
                if (importer.maxTextureSize != MaxSize) { importer.maxTextureSize = MaxSize; dirty = true; }
                if (importer.isReadable) { importer.isReadable = false; dirty = true; }
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Compressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    dirty = true;
                }
                if (importer.sRGBTexture != !linear) { importer.sRGBTexture = !linear; dirty = true; }

                var android = importer.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.maxTextureSize != MaxSize)
                {
                    android.overridden = true;
                    android.maxTextureSize = MaxSize;
                    android.textureCompression = TextureImporterCompression.Compressed;
                    android.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(android);
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
                report.AppendLine(string.Format("  {0,-52} linear={1} size={2}",
                    path, linear, importer.maxTextureSize));
            }

            report.AppendLine(string.Format("  -> {0}/{1} texture đã chỉnh; {2} lần ghi lại",
                changed, scanned, changed));
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Ép nhập lại mọi <c>.glb</c>. Cần thiết vì glTFast không ghi file ảnh ngoài vào
        /// <c>assetDependencies</c> của <c>.meta</c>, nên Unity không tự nhập lại <c>.glb</c> khi
        /// texture đổi tên — material sẽ giữ tham chiếu cũ (trỏ vào texture đã bị xoá).
        /// </summary>
        [MenuItem(MenuRoot + "Reimport glTF Assets", false, 245)]
        public static void ReimportGltfAssets()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { GltfFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }
            Debug.Log(string.Format("[MobileOptimizer] đã ép nhập lại {0} file .glb", n));
        }

        /// <summary>
        /// Đo layout thật của từng mesh nhập từ <c>.glb</c> để biết chỗ nào còn cắt được.
        /// Dùng <see cref="Mesh.GetVertexAttributes"/> (đọc descriptor, không cấp phát mảng dữ liệu)
        /// nên biết chính xác Unity đang lưu thuộc tính nào, định dạng gì, bao nhiêu byte.
        /// </summary>
        [MenuItem(MenuRoot + "Report glTF Meshes", false, 246)]
        public static void ReportGltfMeshes()
        {
            var report = new StringBuilder();
            report.AppendLine("[MobileOptimizer] Mesh nhập từ .glb");
            report.AppendLine("  file                       | meshes |  vertices | triangles | tangent | uv |  est. MB");

            long allVerts = 0, allTris = 0, allBytes = 0;
            int meshesWithTangent = 0, meshesTotal = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { GltfFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;

                long verts = 0, tris = 0, bytes = 0;
                int n = 0, withTangent = 0;

                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var mesh = obj as Mesh;
                    if (mesh == null) continue;
                    n++;
                    verts += mesh.vertexCount;
                    tris += (long)mesh.GetIndexCount(0) / 3;

                    long perVertex = 0;
                    int uvChannels = 0;
                    bool tangent = false;
                    foreach (var a in mesh.GetVertexAttributes())
                    {
                        perVertex += (long)a.dimension * FormatSize(a.format);
                        if (a.attribute == UnityEngine.Rendering.VertexAttribute.TexCoord0
                            || a.attribute == UnityEngine.Rendering.VertexAttribute.TexCoord1
                            || a.attribute == UnityEngine.Rendering.VertexAttribute.TexCoord2
                            || a.attribute == UnityEngine.Rendering.VertexAttribute.TexCoord3) uvChannels++;
                        if (a.attribute == UnityEngine.Rendering.VertexAttribute.Tangent) tangent = true;
                    }
                    if (tangent) withTangent++;
                    int indexSize = mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? 2 : 4;
                    bytes += (long)mesh.vertexCount * perVertex
                             + (long)mesh.GetIndexCount(0) * indexSize;

                    report.AppendLine(string.Format("      mesh {0,-18} verts={1,8} tris={2,8} attrBytes={3,3} uv={4} tangent={5}",
                        mesh.name, mesh.vertexCount, mesh.GetIndexCount(0) / 3, perVertex, uvChannels, tangent));
                }

                if (n == 0) continue;
                allVerts += verts; allTris += tris; allBytes += bytes;
                meshesTotal += n; meshesWithTangent += withTangent;
                report.AppendLine(string.Format("  {0,-26} | {1,6} | {2,9} | {3,9} | {4,7} |    | {5,8:F2}",
                    Path.GetFileName(path), n, verts, tris, withTangent, bytes / 1048576f));
            }

            report.AppendLine(string.Format("  -> {0} mesh, {1} vertex, {2} triangle, {3}/{4} mesh có tangent, tổng ~{5:F1} MB",
                meshesTotal, allVerts, allTris, meshesWithTangent, meshesTotal, allBytes / 1048576f));
            Debug.Log(report.ToString());
        }

        static int FormatSize(UnityEngine.Rendering.VertexAttributeFormat f)
        {
            switch (f)
            {
                case UnityEngine.Rendering.VertexAttributeFormat.Float32: return 4;
                case UnityEngine.Rendering.VertexAttributeFormat.Float16: return 2;
                case UnityEngine.Rendering.VertexAttributeFormat.UNorm8:
                case UnityEngine.Rendering.VertexAttributeFormat.SNorm8: return 1;
                case UnityEngine.Rendering.VertexAttributeFormat.UNorm16:
                case UnityEngine.Rendering.VertexAttributeFormat.SNorm16: return 2;
                case UnityEngine.Rendering.VertexAttributeFormat.UInt8:
                case UnityEngine.Rendering.VertexAttributeFormat.SInt8: return 1;
                case UnityEngine.Rendering.VertexAttributeFormat.UInt16:
                case UnityEngine.Rendering.VertexAttributeFormat.SInt16: return 2;
                case UnityEngine.Rendering.VertexAttributeFormat.UInt32:
                case UnityEngine.Rendering.VertexAttributeFormat.SInt32: return 4;
                default: return 4;
            }
        }

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>
        /// Chẩn đoán vì sao một scene mất material: in ID của object chính từng <c>.glb</c>
        /// (đây là thứ <c>m_SourcePrefab</c> trỏ tới) rồi mở scene ra liệt kê mọi chỗ thiếu
        /// material / thiếu script / PrefabInstance không kết nối được.
        /// </summary>
        [MenuItem(MenuRoot + "Diagnose Scene Materials", false, 247)]
        public static void DiagnoseSceneMaterials()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MobileOptimizer] ID object chính của từng .glb (m_SourcePrefab trỏ vào đây)");

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { GltfFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main == null) continue;
                string g;
                long id;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(main, out g, out id);
                sb.AppendLine(string.Format("  {0,-28} main={1,-14} type={2,-12} id={3}",
                    Path.GetFileName(path), main.name, main.GetType().Name, id));
            }

            const string ScenePath = "Assets/Scenes/CutScene1.unity";
            sb.AppendLine("[MobileOptimizer] Mở " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int noMaterial = 0, notAnAsset = 0, missingScript = 0, brokenPrefab = 0, renderers = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;

                    foreach (var c in go.GetComponents<Component>())
                    {
                        if (c == null)
                        {
                            sb.AppendLine("  MISSING SCRIPT : " + HierarchyPath(t));
                            missingScript++;
                        }
                    }

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderers++;
                        var mat = renderer.sharedMaterial;
                        if (mat == null)
                        {
                            sb.AppendLine("  NO MATERIAL    : " + HierarchyPath(t));
                            noMaterial++;
                        }
                        else if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))
                        {
                            sb.AppendLine(string.Format("  MATERIAL DETACH: {0} mat={1} shader={2}",
                                HierarchyPath(t), mat.name, mat.shader != null ? mat.shader.name : "?"));
                            notAnAsset++;
                        }
                    }

                    var status = PrefabUtility.GetPrefabInstanceStatus(go);
                    if (status != PrefabInstanceStatus.Connected && status != PrefabInstanceStatus.NotAPrefab)
                    {
                        sb.AppendLine(string.Format("  PREFAB {0,-15}: {1}", status, HierarchyPath(t)));
                        brokenPrefab++;
                    }
                }
            }

            sb.AppendLine(string.Format("  -> renderer={0} noMaterial={1} materialDetached={2} missingScript={3} nonConnectedPrefab={4}",
                renderers, noMaterial, notAnAsset, missingScript, brokenPrefab));
            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuRoot + "Dump glTF Sub-asset IDs", false, 242)]
        public static void DumpGltfSubAssetIds()
        {
            var report = new StringBuilder();
            report.AppendLine("[MobileOptimizer] glTF sub-asset LocalFileIDs");

            var glbs = AssetDatabase.FindAssets("t:GameObject", new[] { GltfFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p, System.StringComparer.Ordinal);

            foreach (var path in glbs)
            {
                report.AppendLine("  " + Path.GetFileName(path));
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj == null) continue;
                    string guid;
                    long localId;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId);
                    report.AppendLine(string.Format("    {0,-22} id={1,-20} name={2}",
                        obj.GetType().Name, localId, obj.name));

                    // xác nhận material có bind được texture ngoài hay không
                    var mat = obj as Material;
                    if (mat != null)
                    {
                        var slots = new StringBuilder();
                        foreach (var prop in new[] { "baseColorTexture", "_BaseMap", "_MainTex",
                                                     "normalTexture", "_BumpMap",
                                                     "metallicRoughnessTexture", "_MetallicGlossMap",
                                                     "occlusionTexture", "_OcclusionMap",
                                                     "emissiveTexture", "_EmissionMap" })
                        {
                            if (!mat.HasProperty(prop)) continue;
                            var tex = mat.GetTexture(prop);
                            if (tex == null)
                            {
                                slots.AppendFormat(" {0}=null", prop);
                            }
                            else
                            {
                                var where = AssetDatabase.GetAssetPath(tex);
                                slots.AppendFormat(" {0}={1}[{2}]", prop, tex.name,
                                    string.IsNullOrEmpty(where) ? "NO-ASSET" : where);
                            }
                        }
                        if (slots.Length > 0) report.AppendLine("      textures:" + slots);
                    }
                }
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Build một APK Android để biết dung lượng build thật (số liệu này khác hẳn tổng dung
        /// lượng asset nguồn vì Unity chỉ đóng gói asset được tham chiếu và đã nén lại).
        /// APK dùng cho việc đo sẽ bị xoá ngay sau khi lấy được kích thước.
        /// </summary>
        [MenuItem(MenuRoot + "Build Android APK (Size Report)", false, 243)]
        public static void BuildAndroidApkForSizeReport()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            const string OutputPath = "Temp/SilentSprint-size-report.apk";
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            var sb = new StringBuilder();
            sb.AppendLine("[MobileOptimizer] Android build report");
            sb.AppendLine(string.Format("  result      : {0}", summary.result));
            sb.AppendLine(string.Format("  totalSize   : {0:F2} MB ({1} bytes)", summary.totalSize / 1048576f, summary.totalSize));
            sb.AppendLine(string.Format("  scenes      : {0}", scenes.Length));
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Warning)
                        sb.AppendLine(string.Format("  {0}: {1}", message.type, message.content));
                }
            }
            Debug.Log(sb.ToString());

            try
            {
                if (File.Exists(OutputPath))
                {
                    long onDisk = new FileInfo(OutputPath).Length;
                    Debug.Log(string.Format("[MobileOptimizer] APK trên đĩa: {0:F2} MB", onDisk / 1048576f));
                    File.Delete(OutputPath);
                    Debug.Log("[MobileOptimizer] đã xoá APK dùng để đo");
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning("[MobileOptimizer] không xoá được APK đo: " + e.Message);
            }
        }

        /// <summary>In ra dung lượng từng nhóm asset còn lại trong build.</summary>
        [MenuItem(MenuRoot + "Report Asset Sizes", false, 241)]
        public static void ReportAssetSizes()
        {
            var groups = new[] { "Audio", "Model", "Mixamo", "Texture", "Sprites", "Material",
                                 "Prefabs", "Scenes", "Resources", "TextMesh Pro", "Font",
                                 "EasyAssets", "Hovl Studio", "Particlecollection_Free samples",
                                 "Rain Particles", "Settings", "Shader", "Plugins", "Scripts" };
            var report = new StringBuilder();
            report.AppendLine("[MobileOptimizer] Dung lượng theo thư mục (Assets/)");
            long total = 0;
            foreach (var name in groups)
            {
                string dir = "Assets/" + name;
                if (!Directory.Exists(dir)) continue;
                long bytes = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    .Sum(f => new FileInfo(f).Length);
                total += bytes;
                report.AppendLine(string.Format("  {0,-35} {1,10:F2} MB", name, bytes / 1048576f));
            }
            report.AppendLine(string.Format("  {0,-35} {1,10:F2} MB", "TOTAL", total / 1048576f));
            Debug.Log(report.ToString());
        }
    }
}
