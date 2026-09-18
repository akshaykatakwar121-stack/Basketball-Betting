#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BasketballBetting;

namespace BasketballBetting.EditorTools
{
    public static class BettingGameTools
    {
        [MenuItem("Basketball Betting/Reload Betting Config")]
        public static void ReloadConfig()
        {
            BettingConfigLoader.Reload();
            Debug.Log("[Basketball Betting] Config reloaded from StreamingAssets/BettingConfig.json");
        }

        [MenuItem("Basketball Betting/Reset Demo Wallet")]
        public static void ResetWallet()
        {
            PlayerPrefs.DeleteKey("BasketballBetting.LocalBalance");
            PlayerPrefs.Save();
            Debug.Log("[Basketball Betting] Demo wallet cleared. Next Play Mode uses startingBalance from config.");
        }

        [MenuItem("Basketball Betting/Create Character Asset Folder")]
        public static void CreateCharacterFolder()
        {
            const string folder = "Assets/BasketballBetting/Resources/Characters";
            if (!AssetDatabase.IsValidFolder("Assets/BasketballBetting/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/BasketballBetting", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/BasketballBetting/Resources", "Characters");
            }

            foreach (var src in CharacterCatalog.All)
            {
                string path = folder + "/" + src.Id + ".asset";
                if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path) != null)
                    continue;
                var asset = ScriptableObject.CreateInstance<CharacterDefinition>();
                asset.Id = src.Id;
                asset.DisplayName = src.DisplayName;
                asset.Nickname = src.Nickname;
                asset.JerseyNumber = src.JerseyNumber;
                asset.Attributes = src.Attributes;
                asset.Placeholder = src.Placeholder;
                asset.CustomVisualPrefab = src.CustomVisualPrefab;
                asset.UseImportedVisual = src.UseImportedVisual;
                asset.ImportedVisualKey = src.ImportedVisualKey;
                asset.StatsAreCosmetic = true;
                AssetDatabase.CreateAsset(asset, path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Basketball Betting] Character assets created. Assign CustomVisualPrefab on each asset. Prefab needs a child named RightHand.");
        }
    }
}
#endif
