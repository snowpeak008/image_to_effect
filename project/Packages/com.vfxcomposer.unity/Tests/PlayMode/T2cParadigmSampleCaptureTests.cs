using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VFXComposer.Gallery;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    /// <summary>
    /// T2c unit C: paradigm-sample frame capture (GALLERY_SPEC.md section 6.5).
    /// Reuses W24ContinuousCaptureRecorder against the gallery verdict page:
    /// 7 beat frames x dual Bloom passes x both dimensions, forced pre-capture
    /// scene state (section 6.2), evidence under test-results/gallery-capture/,
    /// and a repo-checked contact sheet + the four advisory metrics written to
    /// docs/design/paradigm/t2c-review/. Reference images ride along as a
    /// quality yardstick strip (never a replication target).
    /// </summary>
    [Explicit("Graphics-backed gallery capture. Run through Invoke-Unity.ps1 -Mode PlayMode -UseGraphics after the paradigm samples are built.")]
    public sealed class T2cParadigmSampleCaptureTests
    {
        private const int Width = 1280, Height = 720, Fps = 60;
        private static readonly int[] BeatFrames = { 3, 12, 30, 60, 75, 120, 180 };
        private const string ToolRelativePath = "Packages/com.vfxcomposer.unity/Tests/PlayMode/T2cParadigmSampleCaptureTests.cs";

        [UnityTest]
        public IEnumerator CaptureVerdictPages_BothDimensions_DualBloomPass()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string sessionRoot = Path.Combine(repoRoot, "test-results", "gallery-capture",
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            string reviewRoot = Path.Combine(repoRoot, "docs", "design", "paradigm", "t2c-review");
            Directory.CreateDirectory(reviewRoot);

            var metricsJson = new StringBuilder();
            metricsJson.Append("{\n  \"schema\": \"t2c-review-metrics/v1\",\n  \"note\": \"advisory readings, not acceptance verdicts (GALLERY_SPEC 6.5)\",\n  \"pages\": [");
            bool firstPage = true;

            foreach (string dimension in new[] { "3d", "2d" })
            {
                string scenePath = "Assets/VFX/Gallery/VFXGallery_" + dimension.ToUpperInvariant() + ".unity";
                // Gallery scenes are deliberately outside EditorBuildSettings
                // (GA-8), so PlayMode capture loads them through the editor
                // play-mode path (this capture is an editor-only tool anyway).
#if UNITY_EDITOR
                Scene scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.That(scene.IsValid(), Is.True, scenePath);
                while (!scene.isLoaded) yield return null;
#else
                Scene scene = default;
                Assert.Ignore("Gallery capture is an editor-only tool (GA-8).");
                yield break;
#endif
                GameObject[] roots = scene.GetRootGameObjects();
                Camera camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
                GalleryController controller = roots.SelectMany(r => r.GetComponentsInChildren<GalleryController>(true)).Single();
                Volume volume = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).Single();

                // Forced pre-capture state (section 6.2): grid lines off,
                // inspection light off, no auto-advance, verdict page loaded.
                foreach (Light light in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
                    if (light.type == LightType.Directional) light.enabled = false;
                Transform grid = roots.Select(r => r.transform.Find("GridLines")).FirstOrDefault(t => t != null);
                if (grid != null) grid.gameObject.SetActive(false);
                controller.LoadPage(1); // paradigm verdict page
                yield return null;

                UnityEngine.Rendering.Universal.Bloom bloom = null;
                if (volume.sharedProfile != null) volume.sharedProfile.TryGet(out bloom);
                Assert.That(bloom, Is.Not.Null, "gallery volume must carry Bloom (GA-3)");
                // The shared profile is an asset: whatever happens, leave it in
                // the default-on state (GA-3 asserts bloom.active == true).
                bool bloomDefault = bloom.active;

                var passSheets = new Dictionary<string, List<Texture2D>>();
                try
                {
                foreach (bool bloomOn in new[] { true, false })
                {
                    bloom.active = bloomOn;
                    string passName = bloomOn ? "bloom-on" : "bloom-off";
                    string evidenceDir = Path.Combine(sessionRoot, dimension, "page1_verdict__" + passName);

                    var recorder = camera.gameObject.GetComponent<W24ContinuousCaptureRecorder>();
                    if (recorder == null) recorder = camera.gameObject.AddComponent<W24ContinuousCaptureRecorder>();
                    recorder.AuthorityCamera = camera;
                    recorder.DiagnosticEffectLayers = 1; // Default layer carries the sample instances

                    W24CaptureProfile profile = BuildProfile(camera, repoRoot, scenePath, bloomOn);
                    W24CaptureSourceHashes sources = BuildSources(repoRoot, scenePath, dimension);
                    recorder.Begin(evidenceDir, "t2c_" + dimension + "_" + passName, profile, sources);

                    // Synchronized replay: all nine cells launch on the same frame.
                    controller.ReplayAll();

                    var beautyFrames = new List<Texture2D>();
                    int captured = 0;
                    for (int frame = 0; frame <= BeatFrames[BeatFrames.Length - 1]; frame++)
                    {
                        yield return null;
                        if (Array.IndexOf(BeatFrames, frame) < 0) continue;
                        recorder.CaptureFrame(frame, frame / (float)Fps, "verdict", (uint)profile.CanonicalSeed);
                        captured++;
                        beautyFrames.Add(ReadFrame(evidenceDir, profile.CanonicalSeed, frame, "beauty"));
                    }
                    Assert.That(captured, Is.EqualTo(BeatFrames.Length));
                    recorder.Complete();
                    passSheets[passName] = beautyFrames;

                    // Advisory metrics from the impact-window beauty frame.
                    Texture2D metricsFrame = beautyFrames[3]; // frame 60
                    Metrics metrics = ComputeMetrics(metricsFrame);
                    if (!firstPage) metricsJson.Append(",");
                    firstPage = false;
                    metricsJson.Append("\n    { \"dimension\": \"").Append(dimension)
                        .Append("\", \"pass\": \"").Append(passName)
                        .Append("\", \"frame\": 60, \"luminanceStops\": ").Append(metrics.LuminanceStops)
                        .Append(", \"edgeSharpnessRatio\": ").Append(N(metrics.EdgeSharpnessRatio))
                        .Append(", \"anisotropyRatio\": ").Append(N(metrics.AnisotropyRatio))
                        .Append(", \"glowSpill\": ").Append(N(metrics.GlowSpill))
                        .Append(", \"overdrawP95Estimate\": ").Append(N(metrics.OverdrawProxy)).Append(" }");
                }

                // Contact sheet: 7 beat frames across, bloom-on row above
                // bloom-off row, reference yardstick strip at the bottom.
                Texture2D sheet = BuildContactSheet(passSheets["bloom-on"], passSheets["bloom-off"], repoRoot);
                File.WriteAllBytes(Path.Combine(reviewRoot, "contact-sheet_" + dimension + "_verdict.png"), sheet.EncodeToPNG());
                UnityEngine.Object.Destroy(sheet);
                }
                finally
                {
                    bloom.active = bloomDefault;
                    foreach (List<Texture2D> list in passSheets.Values)
                        foreach (Texture2D t in list) UnityEngine.Object.Destroy(t);
                }
            }

            metricsJson.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(reviewRoot, "metrics.json"), metricsJson.ToString());
        }

        // ------------------------------------------------------------ profile / sources

        private static W24CaptureProfile BuildProfile(Camera camera, string repoRoot, string scenePath, bool bloomOn)
        {
            const string pipelineAsset = "Assets/Settings/UniversalRP2D.asset";
            const string volumeAsset = "Assets/VFX/Gallery/GalleryVolume.asset";
            return new W24CaptureProfile
            {
                ProfileVersion = "t2c-gallery-capture-profile/v1",
                UnityVersion = Application.unityVersion,
                UrpVersion = "14.0.12",
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsDriverVersion = SystemInfo.graphicsDeviceVersion,
                RenderTextureFormat = RenderTextureFormat.ARGB32.ToString(),
                RendererAssetReference = pipelineAsset,
                RendererAssetSha256 = HashFile(Path.Combine(repoRoot, "project", pipelineAsset)),
                VolumeReference = volumeAsset,
                VolumeSha256 = HashFile(Path.Combine(repoRoot, "project", volumeAsset)),
                ScenePath = scenePath,
                SerializedCameraReference = scenePath + "#Main Camera",
                Width = Width,
                Height = Height,
                FramesPerSecond = Fps,
                Background = camera.backgroundColor,
                ColorSpace = QualitySettings.activeColorSpace.ToString(),
                Hdr = camera.allowHDR,
                Msaa = camera.allowMSAA,
                Bloom = bloomOn,
                ToneMapping = "Neutral",
                CanonicalSeed = 1101,
                RobustnessSeeds = new[] { 2202, 3303 },
                RetainedFrameIndices = BeatFrames
            };
        }

        private static W24CaptureSourceHashes BuildSources(string repoRoot, string scenePath, string dimension)
        {
            // One page = nine prefabs; the anchor prefab is cell 0 and the build
            // hash binds the whole grid (sha256 over all nine prefab hashes).
            string prefabRoot = Path.Combine(repoRoot, "project", "Assets", "VFX", "Generated", dimension);
            string[] prefabs = Directory.GetFiles(prefabRoot, "*__PM.prefab", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(prefabs.Length, Is.EqualTo(9), dimension + " verdict grid");
            string combined;
            using (var sha = SHA256.Create())
            {
                var all = new StringBuilder();
                foreach (string prefab in prefabs) all.Append(HashFile(prefab));
                combined = "sha256:" + string.Concat(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString())).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
            string anchor = prefabs[0];
            string anchorAssetPath = "Assets" + anchor.Replace('\\', '/').Split(new[] { "/Assets" }, StringSplitOptions.None).Last();
            string pageAsset = Path.Combine(repoRoot, "project", "Assets", "VFX", "Gallery", "GalleryPages_" + dimension.ToUpperInvariant() + ".asset");
            return new W24CaptureSourceHashes
            {
                SceneSourcePath = scenePath,
                SceneSha256 = HashFile(Path.Combine(repoRoot, "project", scenePath)),
                PrefabSourcePath = anchorAssetPath,
                PrefabGuid = "t2c-verdict-grid-anchor",
                PrefabSha256 = HashFile(anchor),
                ManifestSourcePath = "Assets/VFX/Gallery/GalleryPages_" + dimension.ToUpperInvariant() + ".asset",
                ManifestSha256 = HashFile(pageAsset),
                BuildHash = combined,
                CaptureToolSourcePath = ToolRelativePath,
                CaptureToolVersion = "t2c/v1",
                CaptureToolSha256 = HashFile(Path.Combine(repoRoot, "project", ToolRelativePath))
            };
        }

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static Texture2D ReadFrame(string evidenceDir, int seed, int frame, string pass)
        {
            string file = Path.Combine(evidenceDir, "frames",
                "seed_" + unchecked((uint)seed).ToString(CultureInfo.InvariantCulture),
                "frame_" + frame.ToString("D5", CultureInfo.InvariantCulture) + "_" + pass + ".png");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(file));
            return texture;
        }

        // ------------------------------------------------------------ contact sheet

        private static Texture2D BuildContactSheet(List<Texture2D> bloomOn, List<Texture2D> bloomOff, string repoRoot)
        {
            const int cell = 2; // downscale factor
            int cw = Width / cell, ch = Height / cell;
            int columns = bloomOn.Count;
            int referenceStrip = ch; // yardstick row height
            var sheet = new Texture2D(cw * columns, ch * 2 + referenceStrip, TextureFormat.RGBA32, false);
            FillBlack(sheet);

            for (int i = 0; i < columns; i++)
            {
                Blit(sheet, bloomOn[i], i * cw, ch + referenceStrip, cw, ch);
                Blit(sheet, bloomOff[i], i * cw, referenceStrip, cw, ch);
            }

            // Reference yardstick strip (quality bar, not a replication target):
            // three reference images across the bottom row.
            string referenceDir = Path.Combine(repoRoot, "docs", "design", "references");
            string[] refs = { "5.png", "8.png", "2.png" };
            int slot = sheet.width / refs.Length;
            for (int i = 0; i < refs.Length; i++)
            {
                string path = Path.Combine(referenceDir, refs[i]);
                if (!File.Exists(path)) continue;
                var reference = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                reference.LoadImage(File.ReadAllBytes(path));
                float scale = Mathf.Min(slot / (float)reference.width, referenceStrip / (float)reference.height);
                int rw = Mathf.Max(1, (int)(reference.width * scale)), rh = Mathf.Max(1, (int)(reference.height * scale));
                Blit(sheet, reference, i * slot + (slot - rw) / 2, 0, rw, rh);
                UnityEngine.Object.Destroy(reference);
            }
            sheet.Apply(false);
            return sheet;
        }

        private static void FillBlack(Texture2D texture)
        {
            var pixels = new Color32[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
            texture.SetPixels32(pixels);
        }

        /// <summary>Nearest-neighbour blit of source into (x, y, w, h) of the sheet.</summary>
        private static void Blit(Texture2D sheet, Texture2D source, int x, int y, int w, int h)
        {
            Color32[] src = source.GetPixels32();
            var block = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                int sy = Mathf.Clamp(j * source.height / h, 0, source.height - 1);
                for (int i = 0; i < w; i++)
                {
                    int sx = Mathf.Clamp(i * source.width / w, 0, source.width - 1);
                    block[j * w + i] = src[sy * source.width + sx];
                }
            }
            sheet.SetPixels32(x, y, w, h, block);
        }

        // ------------------------------------------------------------ advisory metrics (GALLERY_SPEC 6.5)

        private struct Metrics
        {
            public int LuminanceStops;
            public float EdgeSharpnessRatio;
            public float AnisotropyRatio;
            public float GlowSpill;
            public float OverdrawProxy;
        }

        private static Metrics ComputeMetrics(Texture2D frame)
        {
            Color32[] pixels = frame.GetPixels32();
            int w = frame.width, h = frame.height;
            Color32 background = pixels[0];

            // Luminance stops: non-background luminance histogram, count of
            // distinguishable peaks (adjacent-stop ratio >= 2 boundaries at
            // the hdr-grade default multipliers).
            float[] stopBounds = { 0.03f, 0.12f, 0.45f, 0.9f };
            var stopCounts = new int[stopBounds.Length + 1];
            int foreground = 0;

            // Gradient orientation energy for anisotropy + Laplacian for sharpness.
            double lapSum = 0, lapSqSum = 0; int lapN = 0;
            double axisX = 0, axisY = 0;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int y = 1; y < h - 1; y += 2)
            {
                for (int x = 1; x < w - 1; x += 2)
                {
                    Color32 p = pixels[y * w + x];
                    bool isForeground = Mathf.Max(Mathf.Abs(p.r - background.r), Mathf.Abs(p.g - background.g), Mathf.Abs(p.b - background.b)) > 12;
                    float lum = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                    if (isForeground)
                    {
                        foreground++;
                        int stop = 0;
                        while (stop < stopBounds.Length && lum > stopBounds[stop]) stop++;
                        stopCounts[stop]++;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                    float lc = Lum(pixels[y * w + x]);
                    float ll = Lum(pixels[y * w + x - 1]), lr = Lum(pixels[y * w + x + 1]);
                    float lu = Lum(pixels[(y - 1) * w + x]), ld = Lum(pixels[(y + 1) * w + x]);
                    float lap = 4f * lc - ll - lr - lu - ld;
                    lapSum += lap; lapSqSum += lap * lap; lapN++;
                    float gx = lr - ll, gy = ld - lu;
                    axisX += Mathf.Abs(gx); axisY += Mathf.Abs(gy);
                }
            }

            var metrics = new Metrics();
            int threshold = Mathf.Max(foreground / 50, 8);
            metrics.LuminanceStops = stopCounts.Count(c => c > threshold);
            double lapVar = lapN > 0 ? lapSqSum / lapN - (lapSum / lapN) * (lapSum / lapN) : 0;
            metrics.EdgeSharpnessRatio = (float)Math.Sqrt(Math.Max(lapVar, 0)) * 100f;
            metrics.AnisotropyRatio = (float)(Math.Max(axisX, axisY) / Math.Max(Math.Min(axisX, axisY), 1e-6));

            // Glow spill: non-background ratio in the ring band outside the
            // foreground bbox up to 1.5x its half-extent.
            if (maxX > minX && maxY > minY)
            {
                float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
                float ex = (maxX - minX) * 0.5f, ey = (maxY - minY) * 0.5f;
                int band = 0, lit = 0;
                for (int y = 1; y < h - 1; y += 3)
                {
                    for (int x = 1; x < w - 1; x += 3)
                    {
                        float nx = Mathf.Abs(x - cx) / Mathf.Max(ex, 1f), ny = Mathf.Abs(y - cy) / Mathf.Max(ey, 1f);
                        float ring = Mathf.Max(nx, ny);
                        if (ring <= 1f || ring > 1.5f) continue;
                        band++;
                        Color32 p = pixels[y * w + x];
                        if (Mathf.Max(Mathf.Abs(p.r - background.r), Mathf.Abs(p.g - background.g), Mathf.Abs(p.b - background.b)) > 12) lit++;
                    }
                }
                metrics.GlowSpill = band > 0 ? lit / (float)band : 0f;
            }
            metrics.OverdrawProxy = foreground / (float)((w / 2) * (h / 2));
            return metrics;
        }

        private static float Lum(Color32 p)
        {
            return (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
        }

        private static string N(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
