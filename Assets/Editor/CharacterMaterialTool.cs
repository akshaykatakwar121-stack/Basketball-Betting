using UnityEngine;
using UnityEditor;
using System.IO;

public class CharacterMaterialTool : EditorWindow
{
    private float metallic = 0.0f;
    private float smoothness = 0.4f;
    private Color baseColor = Color.white;
    private bool autoExtract = true;
    
    [MenuItem("Tools/Character Material Settings")]
    public static void ShowWindow()
    {
        GetWindow<CharacterMaterialTool>("Char Materials");
    }

    void OnGUI()
    {
        GUILayout.Label("Character Material Properties", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        metallic = EditorGUILayout.Slider("Metallic", metallic, 0f, 1f);
        smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0f, 1f);
        baseColor = EditorGUILayout.ColorField("Base Color Tint", baseColor);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Apply to All Characters", GUILayout.Height(30)))
        {
            if (autoExtract) ExtractMaterials();
            ApplySettings();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This tool finds all character FBX files in Assets/3d model. If their materials are embedded, it will extract them to a Materials folder, then apply the chosen Metallic, Smoothness, and Base Color to them.", MessageType.Info);
    }

    void ApplySettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/3d model" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                Undo.RecordObject(mat, "Update Material Settings");
                
                // URP or Standard
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CharMaterials] Applied settings to {count} materials.");
    }

    void ExtractMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/3d model" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                // Material import mode 2 = UseMaterials
                if (importer.materialLocation == ModelImporterMaterialLocation.InPrefab)
                {
                    Debug.Log($"[CharMaterials] Extracting materials for {path}...");
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}