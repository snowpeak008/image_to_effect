using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VFXComposer.Editor.W24
{
    /// <summary>
    /// Project-level W24 preview rendering infrastructure. The existing 2D renderer remains the
    /// project default; W24 3D authority cameras explicitly select this dedicated Forward renderer.
    /// Provisioning is isolated-shadow-only and rollback-safe because it changes shared settings.
    /// </summary>
    public static class W24PreviewRendererInfrastructure
    {
        public const string PipelineAssetPath = "Assets/Settings/UniversalRP2D.asset";
        public const string RendererAssetPath = "Assets/Settings/VFXPreviewUniversalRenderer.asset";

        internal static void ProvisionInIsolatedShadow()
        {
            var pipeline = AssetDatabase.LoadMainAssetAtPath(PipelineAssetPath);
            if (pipeline == null) throw new InvalidOperationException("Missing active URP asset: " + PipelineAssetPath);

            var pipelineAbsolute = ProjectAbsolute(PipelineAssetPath);
            var rendererAbsolute = ProjectAbsolute(RendererAssetPath);
            var pipelineBytes = File.ReadAllBytes(pipelineAbsolute);
            var pipelineMetaBytes = ReadOptional(pipelineAbsolute + ".meta");
            var rendererBytes = ReadOptional(rendererAbsolute);
            var rendererMetaBytes = ReadOptional(rendererAbsolute + ".meta");
            try
            {
                var renderer = AssetDatabase.LoadMainAssetAtPath(RendererAssetPath);
                if (renderer == null)
                {
                    var rendererType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
                    if (rendererType == null || !typeof(ScriptableObject).IsAssignableFrom(rendererType))
                        throw new InvalidOperationException("URP UniversalRendererData is unavailable.");
                    renderer = ScriptableObject.CreateInstance(rendererType);
                    renderer.name = "VFXPreviewUniversalRenderer";
                    AssetDatabase.CreateAsset(renderer, RendererAssetPath);
                    AssetDatabase.SaveAssetIfDirty(renderer);
                }

                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                if (list == null || !list.isArray) throw new InvalidOperationException("URP asset has no m_RendererDataList.");
                var found = -1;
                for (var index = 0; index < list.arraySize; index++)
                    if (list.GetArrayElementAtIndex(index).objectReferenceValue == renderer) found = index;
                if (found < 0)
                {
                    found = list.arraySize;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(found).objectReferenceValue = renderer;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(pipeline);
                }
                AssetDatabase.ImportAsset(RendererAssetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(PipelineAssetPath, ImportAssetOptions.ForceSynchronousImport);
                if (RequireRendererIndex() != found)
                    throw new InvalidOperationException("W24 Forward renderer did not retain its exact pipeline index.");
            }
            catch
            {
                Restore(RendererAssetPath, rendererAbsolute, rendererBytes, rendererMetaBytes);
                Restore(PipelineAssetPath, pipelineAbsolute, pipelineBytes, pipelineMetaBytes);
                throw;
            }
        }

        public static int RequireRendererIndex()
        {
            var pipeline = AssetDatabase.LoadMainAssetAtPath(PipelineAssetPath);
            var renderer = AssetDatabase.LoadMainAssetAtPath(RendererAssetPath);
            if (pipeline == null) throw new InvalidOperationException("Missing active URP asset: " + PipelineAssetPath);
            if (renderer == null) throw new InvalidOperationException("Missing W24 Forward renderer: " + RendererAssetPath);
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray) throw new InvalidOperationException("URP asset has no m_RendererDataList.");
            var found = -1;
            for (var index = 0; index < list.arraySize; index++)
            {
                if (list.GetArrayElementAtIndex(index).objectReferenceValue != renderer) continue;
                if (found >= 0) throw new InvalidOperationException("W24 Forward renderer is registered more than once.");
                found = index;
            }
            if (found < 0) throw new InvalidOperationException("W24 Forward renderer is not registered in the active URP asset.");
            if (found == 0) throw new InvalidOperationException("W24 Forward renderer must not replace the existing 2D default renderer.");
            return found;
        }

        public static void ApplyToCamera(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            var rendererIndex = RequireRendererIndex();
            var additionalCameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (additionalCameraDataType == null) throw new InvalidOperationException("URP UniversalAdditionalCameraData is unavailable.");
            var additionalCameraData = camera.GetComponent(additionalCameraDataType) ?? camera.gameObject.AddComponent(additionalCameraDataType);
            var setRenderer = additionalCameraDataType.GetMethod("SetRenderer", new[] { typeof(int) });
            if (setRenderer == null) throw new InvalidOperationException("URP camera SetRenderer API is unavailable.");
            setRenderer.Invoke(additionalCameraData, new object[] { rendererIndex });
            var serialized = new SerializedObject(additionalCameraData);
            var rendererProperty = serialized.FindProperty("m_RendererIndex");
            if (rendererProperty == null || rendererProperty.intValue != rendererIndex)
                throw new InvalidOperationException("W24 authority camera did not retain the Forward renderer index.");
        }

        private static void Restore(string assetPath, string absolutePath, byte[] bytes, byte[] metaBytes)
        {
            if (bytes == null)
            {
                if ((File.Exists(absolutePath) || File.Exists(absolutePath + ".meta")) && !AssetDatabase.DeleteAsset(assetPath))
                    throw new InvalidOperationException("Could not roll back W24 renderer asset: " + assetPath);
            }
            else
            {
                File.WriteAllBytes(absolutePath, bytes);
                RestoreOptional(absolutePath + ".meta", metaBytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static byte[] ReadOptional(string path) { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        private static void RestoreOptional(string path, byte[] bytes)
        {
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllBytes(path, bytes);
        }
        private static string ProjectAbsolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
