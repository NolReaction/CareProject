using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IntercomMvp.Editor
{
    /// <summary>Creates the small project's URP assets using Unity's asset API.</summary>
    internal static class IntercomRenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/IntercomMVP/Settings/Rendering/URP";
        private const string RendererPath = SettingsFolder + "/IntercomRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/IntercomPipeline.asset";

        internal static void EnsurePipeline()
        {
            // An existing URP project already has its renderer and quality setup.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                return;

            if (GraphicsSettings.currentRenderPipeline != null)
                throw new InvalidOperationException(
                    "Intercom MVP requires a Universal Render Pipeline project. " +
                    "Open the supplied standalone project, or use a new Universal 3D project.");

            EnsureFolder(SettingsFolder);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                // OnEnable initializes URP's built-in renderer resources.
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "IntercomRenderer";
                renderer.renderingMode = RenderingMode.Forward;
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "IntercomPipeline";
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1f;
                pipeline.supportsHDR = false;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.shadowDistance = 30f;
                pipeline.shadowCascadeCount = 2;
                pipeline.mainLightShadowmapResolution = 2048;

                // These two URP 17.3 settings have public getters only.
                var settings = new SerializedObject(pipeline);
                settings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string parent = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string current = parent + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(parent, segments[i]);
                parent = current;
            }
        }
    }
}
