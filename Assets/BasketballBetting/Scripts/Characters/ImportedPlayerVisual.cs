using UnityEngine;

namespace BasketballBetting
{
    public static class ImportedPlayerVisual
    {
        public const string AssetPath = "Assets/3d model/blaze+player+3d+model.fbx";
        public const string ResourcesPath = "PlayerModels/BlazePlayer";
        public const string ResourcesFolder = "PlayerModels/";

        public static readonly string[] SearchFolders = { "Assets/3d model" };

        public static GameObject Load()
        {
            return LoadFor(null);
        }

        public static GameObject LoadFor(CharacterDefinition def)
        {
            if (def != null && def.CustomVisualPrefab != null)
                return def.CustomVisualPrefab;

            string key = def != null ? def.ImportedVisualKey : null;
            if (string.IsNullOrEmpty(key))
            {
                if (def != null && !def.UseImportedVisual)
                    return null;
                key = "blaze_game";
            }

            GameObject fromResources = Resources.Load<GameObject>(ResourcesFolder + PrefabName(key));
            if (fromResources != null)
                return fromResources;

            if (key == "blaze_player" || key == "blaze_game")
            {
                GameObject legacy = Resources.Load<GameObject>(ResourcesPath);
                if (legacy != null && key == "blaze_player")
                    return legacy;
            }

#if UNITY_EDITOR
            GameObject editor = LoadEditor(key);
            if (editor != null)
                return editor;
#endif
            return Resources.Load<GameObject>(ResourcesPath);
        }

        public static string PrefabName(string key)
        {
            switch (key)
            {
                case "blaze_street": return "BlazeStreet";
                case "blaze_player": return "BlazePlayer";
                case "hawk_game": return "HawkGame";
                case "hawk_casual": return "HawkCasual";
                case "kingsley_game": return "KingsleyGame";
                case "kingsley_street": return "KingsleyStreet";
                case "striker_game": return "StrikerGame";
                case "striker_street": return "StrikerStreet";
                default: return "BlazeGame";
            }
        }

        public static string[] AllKeys()
        {
            return new[]
            {
                "blaze_game", "blaze_street", "blaze_player",
                "hawk_game", "hawk_casual",
                "kingsley_game", "kingsley_street",
                "striker_game", "striker_street"
            };
        }

#if UNITY_EDITOR
        public static GameObject LoadEditor(string key)
        {
            string[] tokens = TokensFor(key);
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Model", SearchFolders);
            string bestPath = null;
            int bestScore = -1;
            for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                string lower = path.Replace('\\', '/').ToLowerInvariant();
                int score = ScorePath(lower, tokens);
                if (score <= 0)
                    continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            if (string.IsNullOrEmpty(bestPath) && (key == "blaze_player" || key == "blaze_game"))
                bestPath = AssetPath;

            if (string.IsNullOrEmpty(bestPath))
                return null;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
        }

        static int ScorePath(string lowerPath, string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!lowerPath.Contains(tokens[i]))
                    return 0;
            }
            int score = 10 + tokens.Length;
            if (lowerPath.Contains("tripo_convert"))
                score += 5;
            if (lowerPath.Contains("street") && !ContainsToken(tokens, "street") && !ContainsToken(tokens, "casual"))
                return 0;
            if (lowerPath.Contains("casual") && !ContainsToken(tokens, "casual") && !ContainsToken(tokens, "street"))
                return 0;
            if (lowerPath.Contains("game") && ContainsToken(tokens, "player"))
                return 0;
            if (lowerPath.Contains("player") && ContainsToken(tokens, "game"))
                return 0;
            return score;
        }

        static bool ContainsToken(string[] tokens, string token)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == token)
                    return true;
            }
            return false;
        }

        static string[] TokensFor(string key)
        {
            switch (key)
            {
                case "blaze_street": return new[] { "blaze", "street" };
                case "blaze_player": return new[] { "blaze", "player" };
                case "hawk_game": return new[] { "hawk", "game" };
                case "hawk_casual": return new[] { "hawk", "casual" };
                case "kingsley_game": return new[] { "kingsley", "game" };
                case "kingsley_street": return new[] { "kingsley", "street" };
                case "striker_game": return new[] { "striker", "game" };
                case "striker_street": return new[] { "striker", "street" };
                default: return new[] { "blaze", "game" };
            }
        }
#endif
    }
}
