using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectBootstrap
{
    /// <summary>
    /// Creates a Universal Render Pipeline asset and makes it the project's render pipeline, then strips
    /// the Built-In-only shaders out of the always-included list.
    /// </summary>
    /// <remarks>
    /// Assigning <see cref="GraphicsSettings.defaultRenderPipeline"/> is what actually takes the project
    /// off the Built-In pipeline: it writes <c>m_CustomRenderPipeline</c> in GraphicsSettings.asset, which
    /// is the field the Hub reads to decide whether a project is still on the deprecated pipeline.
    /// Per-quality-level overrides are deliberately left empty so every level falls back to this one asset
    /// rather than cluttering the project with six near-identical pipeline assets.
    /// </remarks>
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelineAssetPath = SettingsFolder + "/UniversalRenderPipelineAsset.asset";
        private const string RendererAssetPath = SettingsFolder + "/UniversalRenderer.asset";

        /// <summary>
        /// Shaders in the always-included list that only exist to serve the Built-In pipeline. Everything
        /// else in that list (UI, sprite and default resources) is pipeline-agnostic and stays.
        /// </summary>
        private static readonly string[] BuiltInOnlyShaderPrefixes =
        {
            "Legacy Shaders/",
            "Hidden/Internal-Deferred",
            "Hidden/Internal-PrePassLighting",
            "Hidden/Internal-ScreenSpaceShadows",
            "Hidden/Internal-MotionVectors",
            "Hidden/VideoDecode",
        };

        public static void Configure()
        {
            var pipeline = CreatePipelineAsset();
            AssignPipeline(pipeline);
            PruneAlwaysIncludedShaders();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RenderPipelineSetup] Active pipeline: {GraphicsSettings.currentRenderPipeline?.GetType().Name ?? "<none, still Built-In>"}");
            EditorApplication.Exit(0);
        }

        private static UniversalRenderPipelineAsset CreatePipelineAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (existing != null)
            {
                Debug.Log("[RenderPipelineSetup] Reusing the existing pipeline asset.");
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);

            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RenderPipelineSetup] Created {PipelineAssetPath} and {RendererAssetPath}.");
            return pipeline;
        }

        private static void AssignPipeline(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Clear any per-level override so every quality level resolves to the default asset above.
            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);

            Debug.Log($"[RenderPipelineSetup] Assigned as the default pipeline; cleared {QualitySettings.names.Length} per-level overrides.");
        }

        private static void PruneAlwaysIncludedShaders()
        {
            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphicsSettings == null)
            {
                Debug.LogWarning("[RenderPipelineSetup] Could not open GraphicsSettings.asset; leaving the shader list alone.");
                return;
            }

            var serialized = new SerializedObject(graphicsSettings);
            var list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[RenderPipelineSetup] m_AlwaysIncludedShaders not found; leaving it alone.");
                return;
            }

            var kept = new List<Shader>();
            var dropped = new List<string>();

            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == null)
                {
                    dropped.Add("<missing shader reference>");
                    continue;
                }

                if (BuiltInOnlyShaderPrefixes.Any(p => shader.name.StartsWith(p, System.StringComparison.Ordinal)))
                    dropped.Add(shader.name);
                else
                    kept.Add(shader);
            }

            if (dropped.Count == 0)
            {
                Debug.Log($"[RenderPipelineSetup] Always-included shaders already clean: {string.Join(", ", kept.Select(s => s.name))}");
                return;
            }

            list.ClearArray();
            for (int i = 0; i < kept.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[RenderPipelineSetup] Dropped Built-In-only always-included shaders: {string.Join(", ", dropped)}");
            Debug.Log($"[RenderPipelineSetup] Kept: {string.Join(", ", kept.Select(s => s.name))}");
        }
    }
}
