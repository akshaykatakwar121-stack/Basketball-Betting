using System;
using UnityEngine;

namespace BasketballBetting
{
    [Serializable]
    public sealed class CharacterVisualProfile
    {
        public string displayName;
        public string nickname;
        public int jerseyNumber = 0;
        public Color skin;
        public Color hair;
        public Color jersey;
        public Color shorts;
        public Color trim;
        public Color shoes;
        public Color socks;
        public float height = 1.95f;
        public float muscle = 1f;
        public float bulk = 1f;
        public bool bald;
        public bool longHair;
        public bool headband;
        public bool tattoos;
        public CharacterAttributes attributes;
    }

    /// <summary>
    /// Assign CustomVisualPrefab, or set ImportedVisualKey / UseImportedVisual to load a rigged model from Assets/3d model.
    /// </summary>
    [CreateAssetMenu(menuName = "Basketball Betting/Character Definition", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public string Nickname;
        public int JerseyNumber;
        public CharacterAttributes Attributes;
        public CharacterVisualProfile Placeholder;
        public GameObject CustomVisualPrefab;
        public bool UseImportedVisual;
        public string ImportedVisualKey;
        public bool StatsAreCosmetic = true;

        public static CharacterDefinition CreateRuntime(string id, CharacterVisualProfile profile, string importedVisualKey = null)
        {
            var def = CreateInstance<CharacterDefinition>();
            def.Id = id;
            def.DisplayName = profile.displayName;
            def.Nickname = profile.nickname;
            def.JerseyNumber = profile.jerseyNumber;
            def.Attributes = profile.attributes;
            def.Placeholder = profile;
            def.ImportedVisualKey = importedVisualKey;
            def.UseImportedVisual = !string.IsNullOrEmpty(importedVisualKey);
            def.StatsAreCosmetic = true;
            return def;
        }
    }

    public static class CharacterCatalog
    {
        static CharacterDefinition[] _all;

        public static CharacterDefinition[] All
        {
            get
            {
                if (_all == null)
                    _all = BuildDefaultRoster();
                return _all;
            }
        }

        public static void Override(CharacterDefinition[] roster)
        {
            if (roster != null && roster.Length > 0)
                _all = roster;
        }

        public static CharacterDefinition Get(string id)
        {
            var all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].Id == id)
                    return all[i];
            }
            return all.Length > 0 ? all[0] : null;
        }

        static CharacterDefinition[] BuildDefaultRoster()
        {
            CharacterDefinition[] resources = Resources.LoadAll<CharacterDefinition>("Characters");
            if (resources != null && resources.Length > 0)
                return resources;

            return new[]
            {
                CharacterDefinition.CreateRuntime("marcus_hale", new CharacterVisualProfile
                {
                    displayName = "Marcus Hale",
                    nickname = "JET",
                    jerseyNumber = 3,
                    skin = Hex("4A2C1A"),
                    hair = Hex("1A120C"),
                    jersey = Hex("111111"),
                    shorts = Hex("111111"),
                    trim = Hex("D4AF37"),
                    shoes = Hex("D4AF37"),
                    socks = Hex("F2F2F2"),
                    height = 2.01f,
                    muscle = 0.92f,
                    bulk = 0.86f,
                    bald = false,
                    longHair = false,
                    headband = true,
                    tattoos = false,
                    attributes = new CharacterAttributes { shooting = 88, accuracy = 84, releaseSpeed = 91, power = 70, threePoint = 90 }
                }, "blaze_player"),
                CharacterDefinition.CreateRuntime("rico_vargas", new CharacterVisualProfile
                {
                    displayName = "Rico Vargas",
                    nickname = "RICO",
                    jerseyNumber = 11,
                    skin = Hex("C68642"),
                    hair = Hex("2B1B12"),
                    jersey = Hex("C8102E"),
                    shorts = Hex("1A1A1A"),
                    trim = Hex("FFFFFF"),
                    shoes = Hex("C8102E"),
                    socks = Hex("FFFFFF"),
                    height = 1.85f,
                    muscle = 1.18f,
                    bulk = 1.16f,
                    bald = false,
                    longHair = true,
                    headband = false,
                    tattoos = true,
                    attributes = new CharacterAttributes { shooting = 80, accuracy = 77, releaseSpeed = 82, power = 88, threePoint = 74 }
                }, "kingsley_street"),
                CharacterDefinition.CreateRuntime("andre_cole", new CharacterVisualProfile
                {
                    displayName = "Andre Cole",
                    nickname = "TOWER",
                    jerseyNumber = 32,
                    skin = Hex("3B2214"),
                    hair = Hex("111111"),
                    jersey = Hex("0B1F4B"),
                    shorts = Hex("0B1F4B"),
                    trim = Hex("E31837"),
                    shoes = Hex("FFFFFF"),
                    socks = Hex("E31837"),
                    height = 2.13f,
                    muscle = 1.22f,
                    bulk = 1.28f,
                    bald = true,
                    longHair = false,
                    headband = false,
                    tattoos = true,
                    attributes = new CharacterAttributes { shooting = 76, accuracy = 79, releaseSpeed = 68, power = 94, threePoint = 71 }
                }, "kingsley_game"),
                CharacterDefinition.CreateRuntime("kai_nakamura", new CharacterVisualProfile
                {
                    displayName = "Kai Nakamura",
                    nickname = "KAI",
                    jerseyNumber = 7,
                    skin = Hex("E0B090"),
                    hair = Hex("1C1C1C"),
                    jersey = Hex("008E97"),
                    shorts = Hex("111111"),
                    trim = Hex("F9A01B"),
                    shoes = Hex("008E97"),
                    socks = Hex("F9A01B"),
                    height = 1.90f,
                    muscle = 0.88f,
                    bulk = 0.82f,
                    bald = false,
                    longHair = false,
                    headband = true,
                    tattoos = false,
                    attributes = new CharacterAttributes { shooting = 86, accuracy = 90, releaseSpeed = 93, power = 66, threePoint = 88 }
                }, "blaze_player"),
                CharacterDefinition.CreateRuntime("darius_bell", new CharacterVisualProfile
                {
                    displayName = "Darius Bell",
                    nickname = "D-ROCK",
                    jerseyNumber = 23,
                    skin = Hex("8D5524"),
                    hair = Hex("2A1A10"),
                    jersey = Hex("552583"),
                    shorts = Hex("FDB927"),
                    trim = Hex("FDB927"),
                    shoes = Hex("552583"),
                    socks = Hex("FFFFFF"),
                    height = 1.98f,
                    muscle = 1.10f,
                    bulk = 1.04f,
                    bald = false,
                    longHair = false,
                    headband = false,
                    tattoos = true,
                    attributes = new CharacterAttributes { shooting = 83, accuracy = 81, releaseSpeed = 80, power = 86, threePoint = 82 }
                }, "kingsley_street")
            };
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
