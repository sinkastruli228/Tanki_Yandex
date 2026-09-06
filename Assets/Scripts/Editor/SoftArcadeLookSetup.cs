using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Authoring only: the finished look is stored in materials, profiles and the scene.
public static class SoftArcadeLookSetup
{
    private const string Folder = "Assets/Materials/Terrain/";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Tanki/Apply Soft Arcade Look %&#g")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply the look outside Play mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open SampleScene before applying the look.");
        bool wasDirty = scene.isDirty;
        Directory.CreateDirectory(".utmp/soft-arcade");
        var texturePath = Folder + "SoftArcadeSand_Albedo.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 1024;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        var sand = AssetDatabase.LoadAssetAtPath<Material>(Folder + "SoftArcadeSand.mat");
        if (sand == null)
        {
            sand = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            sand.name = "SoftArcadeSand";
            AssetDatabase.CreateAsset(sand, Folder + "SoftArcadeSand.mat");
        }
        sand.enableInstancing = true;
        sand.SetFloat("_EnableInstancedPerPixelNormal", 1);
        sand.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
        EditorUtility.SetDirty(sand);

        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Folder + "SoftArcadeSand.terrainlayer");
        if (layer == null)
        {
            layer = new TerrainLayer { name = "SoftArcadeSand" };
            AssetDatabase.CreateAsset(layer, Folder + "SoftArcadeSand.terrainlayer");
        }
        layer.diffuseTexture = texture;
        layer.normalMapTexture = null;
        layer.tileSize = new Vector2(38, 38);
        layer.metallic = 0;
        layer.smoothness = 0.04f;
        layer.normalScale = 0;
        EditorUtility.SetDirty(layer);

        var terrain = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Terrain>(true)).Single();
        // Keep the original height/paint/tree data available for a complete rollback.
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "SoftArcadeTerrain.asset");
        if (data == null)
        {
            data = UnityEngine.Object.Instantiate(terrain.terrainData);
            data.name = "SoftArcadeTerrain";
            AssetDatabase.CreateAsset(data, Folder + "SoftArcadeTerrain.asset");
            data.terrainLayers = new[] { layer };
            var weights = new float[data.alphamapHeight, data.alphamapWidth, 1];
            for (int y = 0; y < data.alphamapHeight; y++)
                for (int x = 0; x < data.alphamapWidth; x++) weights[y, x, 0] = 1;
            data.SetAlphamaps(0, 0, weights);
            data.SetBaseMapDirty();
            EditorUtility.SetDirty(data);
        }
        Undo.RecordObject(terrain, "Apply soft sand");
        terrain.terrainData = data;
        terrain.materialTemplate = sand;
        terrain.drawInstanced = true;
        var collider = terrain.GetComponent<TerrainCollider>();
        Undo.RecordObject(collider, "Match sand terrain collider");
        collider.terrainData = data;
        terrain.Flush();

        ConfigurePipeline("PC", 180, 2, SoftShadowQuality.Medium);
        ConfigurePipeline("Mobile", 140, 2, SoftShadowQuality.Low);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var ao = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.name == "ScreenSpaceAmbientOcclusion");
        if (ao != null)
        {
            var serialized = new SerializedObject(ao);
            serialized.FindProperty("m_Settings.Intensity").floatValue = 0.25f;
            serialized.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.15f;
            serialized.FindProperty("m_Settings.Downsample").boolValue = true;
            serialized.ApplyModifiedProperties();
        }

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        profile.TryGet(out Bloom bloom);
        bloom.intensity.Override(0.18f);
        bloom.threshold.Override(1.15f);
        bloom.highQualityFiltering.Override(false);
        EditorUtility.SetDirty(bloom);
        profile.TryGet(out Vignette vignette);
        vignette.intensity.Override(0.08f);
        vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(vignette);
        profile.TryGet(out Tonemapping tonemapping);
        tonemapping.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(tonemapping);

        foreach (var camera in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true)))
        {
            var cameraData = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObject(cameraData, "Soften arcade edges");
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.dithering = true;
        }
        var sun = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Light>(true))
            .First(l => l.type == LightType.Directional);
        Undo.RecordObject(sun, "Warm arcade sunlight");
        sun.color = new Color(1f, 0.955f, 0.87f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.58f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.54f, 0.61f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.405f, 0.35f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.235f, 0.20f);
        RenderSettings.reflectionIntensity = 0.3f;

        Paint("Assets/Models/Tank/M_Tank_Body.mat", "#67824D", 0.26f);
        Paint("Assets/Models/Tank/Desert.mat", "#B78C53", 0.24f);
        Paint("Assets/Models/Tank/Body.mat", "#D3D9D3", 0.25f);
        Paint("Assets/Models/Tank/M_Maus_Body.mat", "#929C93", 0.22f);
        Paint("Assets/Models/Tank/Black.mat", "#34332F", 0.14f);
        Paint("Assets/Models/Tank/New Material.mat", "#41433E", 0.16f);
        Paint("Assets/Models/Tank/M_Tank_Trucks.mat", "#50514A", 0.18f, 0.12f);
        Paint("Assets/Models/Walls/New Material.mat", "#B69B77", 0.10f);
        Paint("Assets/Models/Plants/New Material.mat", "#6B8D56", 0.10f);
        Paint("Assets/Models/Box/Wood.mat", "#8C5F46", 0.16f);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!wasDirty) EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
        Selection.activeObject = sand;
        File.WriteAllText(".utmp/soft-arcade/applied.txt", "Soft arcade look applied. Scene saved: " + !wasDirty
            + "\nTerrain: " + data.size + "; alpha layers: " + data.alphamapLayers
            + "\nShader: " + sand.shader.name + "; supported: " + sand.shader.isSupported);
        Debug.Log("Soft arcade look applied. Original terrain data and material preserved.");
    }

    private static void ConfigurePipeline(string name, float distance, int cascades, SoftShadowQuality quality)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + name + "_RPAsset.asset");
        Undo.RecordObject(asset, "Soft arcade render quality");
        asset.renderScale = 1f;
        // FXAA on the cameras works with both existing render paths and avoids stacking AA methods.
        asset.msaaSampleCount = 1;
        asset.shadowDistance = distance;
        asset.shadowCascadeCount = cascades;
        asset.cascade2Split = 0.45f;
        asset.cascadeBorder = 0.15f;
        asset.shadowDepthBias = 0.5f;
        asset.shadowNormalBias = 0.4f;
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
        serialized.FindProperty("m_SoftShadowQuality").intValue = (int)quality;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    private static void Paint(string path, string hex, float smoothness, float metallic = 0)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException("Missing material: " + path);
        Undo.RecordObject(material, "Soft arcade palette");
        ColorUtility.TryParseHtmlString(hex, out var color);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
    }

    [MenuItem("Tools/Tanki/Validate Soft Arcade Terrain")]
    public static void ValidateTerrain()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "SoftArcadeTerrain.asset");
        var original = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath("ce121b6413af14a40aec60d1a452936e"));
        if (data == null || original == null || data.size != original.size || data.heightmapResolution != original.heightmapResolution)
            throw new InvalidOperationException("Terrain dimensions differ from the original.");
        int resolution = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, resolution, resolution);
        var before = original.GetHeights(0, 0, resolution, resolution);
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                if (heights[y, x] != before[y, x]) throw new InvalidOperationException("Terrain height changed.");
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain.terrainData != data || terrain.GetComponent<TerrainCollider>().terrainData != data)
            throw new InvalidOperationException("Terrain and collider data do not match.");
        var material = terrain.materialTemplate;
        if (!material.shader.isSupported || ShaderUtil.GetShaderMessages(material.shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
            throw new InvalidOperationException("Terrain shader has errors.");
        Directory.CreateDirectory(".utmp/soft-arcade");
        File.WriteAllText(".utmp/soft-arcade/validation.txt", "PASS: all terrain heights unchanged; dimensions unchanged; collider matches; terrain shader supported without compile errors.\n"
            + "Original tree count: " + original.treeInstanceCount + "; new tree count: " + data.treeInstanceCount);
        Debug.Log("Soft arcade terrain validation passed.");
    }

    [MenuItem("Tools/Tanki/Preview Mobile Render Profile")]
    public static void PreviewMobile() => QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"), true);

    [MenuItem("Tools/Tanki/Preview PC Render Profile")]
    public static void PreviewPC() => QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
}
