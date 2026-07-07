using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot URP setup for batch mode: -executeMethod PinkSoftURPMigrator.Migrate
/// </summary>
public static class PinkSoftURPMigrator
{
    const string SettingsDir = "Assets/Settings";
    const string RendererPath = SettingsDir + "/URP_Renderer.asset";
    const string PipelinePath = SettingsDir + "/URP_Pipeline.asset";

    public static void Migrate()
    {
        EnsureSettingsFolder();

        var renderer = LoadOrCreateRenderer();
        var pipeline = LoadOrCreatePipeline(renderer);

        AssignPipeline(pipeline);
        UpgradeOpenScenes();
        UpgradeSceneAssets("Assets/Scenes/Boot.unity");
        UpgradeSceneAssets("Assets/Scenes/Lobby.unity");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PinkSoftURPMigrator] URP migration complete.");
        EditorApplication.Exit(0);
    }

    static void EnsureSettingsFolder()
    {
        if (!AssetDatabase.IsValidFolder(SettingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");
    }

    static UniversalRendererData LoadOrCreateRenderer()
    {
        var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (existing != null)
            return existing;

        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, RendererPath);
        return renderer;
    }

    static UniversalRenderPipelineAsset LoadOrCreatePipeline(UniversalRendererData renderer)
    {
        var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (existing != null)
        {
            BindRenderer(existing, renderer);
            return existing;
        }

        var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        AssetDatabase.CreateAsset(pipeline, PipelinePath);
        BindRenderer(pipeline, renderer);
        EditorUtility.SetDirty(pipeline);
        return pipeline;
    }

    static void BindRenderer(UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer)
    {
        var serialized = new SerializedObject(pipeline);
        var rendererList = serialized.FindProperty("m_RendererDataList");
        rendererList.ClearArray();
        rendererList.InsertArrayElementAtIndex(0);
        rendererList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignPipeline(UniversalRenderPipelineAsset pipeline)
    {
        GraphicsSettings.defaultRenderPipeline = pipeline;
        GraphicsSettings.defaultRenderPipeline = pipeline;

        for (var i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = pipeline;
        }

        QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), applyExpensiveChanges: true);
        EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
    }

    static void UpgradeOpenScenes()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
            UpgradeSceneCameras(SceneManager.GetSceneAt(i));
    }

    static void UpgradeSceneAssets(string scenePath)
    {
        if (!File.Exists(scenePath))
            return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        UpgradeSceneCameras(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void UpgradeSceneCameras(Scene scene)
    {
        if (!scene.isLoaded)
            return;

        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var camera in cameras)
        {
            if (camera.GetComponent<UniversalAdditionalCameraData>() != null)
                continue;

            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
            data.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
        }
    }
}
