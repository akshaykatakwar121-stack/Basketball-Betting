using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class AutoFixPrefabs
{
    static AutoFixPrefabs()
    {
        EditorApplication.delayCall += DoFix;
    }

    static void DoFix()
    {
        if (EditorPrefs.GetBool("PrefabsFixed_Hoops_v2", false))
            return;
            
        EditorPrefs.SetBool("PrefabsFixed_Hoops_v2", true);
        
        string[] prefabs = {
            "BlazeGame", "BlazePlayer", "BlazeStreet",
            "HawkCasual", "HawkGame",
            "KingsleyGame", "KingsleyStreet",
            "StrikerGame", "StrikerStreet"
        };
        
        string dstDir = "Assets/BasketballBetting/Resources/PlayerModels/";
        
        foreach (string p in prefabs)
        {
            string charName = "";
            string fbxName = "";
            if (p.StartsWith("Blaze")) { charName = "BLAZE"; fbxName = "BLAZE_FINAL_RIGGED"; }
            else if (p.StartsWith("Hawk")) { charName = "HAWK"; fbxName = "HAWK_FINAL_RIGGED"; }
            else if (p.StartsWith("Kingsley")) { charName = "KINGSLEY"; fbxName = "KINGSLAY_FINAL_RIGGED"; }
            else if (p.StartsWith("Striker")) { charName = "STRIKER"; fbxName = "STRIKER_FINAL_RIGGED"; }
            
            string fbxPath = $"Assets/3d model/{charName}/{fbxName}.fbx";
            
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[AutoFix] Could not find FBX: {fbxPath}");
                continue;
            }
            
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            instance.name = p;
            
            string prefabPath = dstDir + p + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            
            Debug.Log($"[AutoFix] Recreated prefab {p} from {fbxPath}");
        }
        
        string ravenPath = "Assets/3d model/RAVEN/RAVEN_FINAL_RIGGED.fbx";
        GameObject ravenFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ravenPath);
        if (ravenFbx != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(ravenFbx);
            instance.name = "RavenGame";
            PrefabUtility.SaveAsPrefabAsset(instance, dstDir + "RavenGame.prefab");
            Object.DestroyImmediate(instance);
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("[AutoFix] All prefabs updated successfully! The characters should now render correctly.");
    }
}