using UnityEditor;
class Refresh {
    static void Main() {
        AssetDatabase.ImportAsset(""Assets/BasketballBetting/Art/Textures/Court_Albedo.png"", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(""Assets/BasketballBetting/Art/Textures/Court_Mask.png"", ImportAssetOptions.ForceUpdate);
    }
}
