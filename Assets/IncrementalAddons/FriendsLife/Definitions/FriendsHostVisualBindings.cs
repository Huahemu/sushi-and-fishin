using System;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    /// <summary>Shared host art templates. Original item identity, variant, ownership and effects stay in the frozen core.</summary>
    public static class FriendsHostVisualBindings
    {
        public static string OutfitResource(LifeCreativeClothDefinition cloth)
        {
            if (cloth == null) return null;
            switch (cloth.sourceType)
            {
                case "All": return "overall/BSMC_Overall_Overall";
                case "Tops": return "top/BSMC_Top_Shirt";
                case "Topslong": return "top/Top_BattleDress";
                case "BottomsA": return "bottom/BSMC_Bottom_Jeans";
                case "BottomsB": return "socks/Socks_BasicSocks";
                case "Headwear": return cloth.id.StartsWith("ClothHeadwearGlass", StringComparison.Ordinal) ? "upperface/UpperFace_SimpleGlasses" : "hat/Hat_SunHat";
                case "Shoes": return "feet/BSMC_Feet_ComfyShoes";
                case "Accessory": return "neck/Neck_RibbonBow";
                default: return null;
            }
        }
        public static readonly string[] OutfitResources =
        {
            "overall/BSMC_Overall_Overall", "top/BSMC_Top_Shirt", "top/Top_BattleDress", "bottom/BSMC_Bottom_Jeans",
            "socks/Socks_BasicSocks", "upperface/UpperFace_SimpleGlasses", "hat/Hat_SunHat", "feet/BSMC_Feet_ComfyShoes", "neck/Neck_RibbonBow"
        };
        public static OutfitData OutfitData(Outfit template, int originalVariant)
        {
            var result = new OutfitData { outfit = template.Type.name + "/" + template.name,
                colors = new List<Color>(template.defaultColors != null && template.defaultColors.Length > 0 ? template.defaultColors : template.colors),
                color = Color.white, pattern = "", patternColors = new List<Color> { Color.white, Color.white, Color.white },
                decalColors = new List<Color>(), decalDatas = new List<DecalData>(),
                partVisibility = new bool[template.optionalPieces?.Length ?? 0] };
            for (int i = 0; i < result.partVisibility.Length; i++) result.partVisibility[i] = template.optionalPieces[i] != null && template.optionalPieces[i].activeSelf;
            if (template.outfitSwatches != null && template.outfitSwatches.Count > 0)
            {
                result.swatch = Math.Min(originalVariant, template.outfitSwatches.Count - 1);
                result.color = template.outfitSwatches[result.swatch].IconColorTop;
            }
            return result;
        }

        static readonly Dictionary<string, string> RoomThemes = BuildRoomThemes();
        static Dictionary<string, string> BuildRoomThemes()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            Add(result, "Wood", "Log WoodBoard Japanese Shoji Shogun Simple Asianresort Sushi Temple Atelier Classroom Book Midcentury Chinese Market");
            Add(result, "Soft", "Baby Cute Candy Heart Lace Stuffed Flowergarden SmallFlower BigFlower Sakura Heaven Wedding GardenWedding Christmas");
            Add(result, "Green", "Jungle Grassland Botanical Field Garden Camouflage Dinosaur Dog Fruit");
            Add(result, "Stone", "Marble Strata Cemetery Desert Dilapidated Western WesternStyle Pirate Garage Street Brick");
            Add(result, "Cool", "Bathroom PublicBath Antarctic Beach IceRink Snow TropicalSea Rain Hospital WhiteRoom LockerRoom");
            Add(result, "Dark", "Abduction Galaxy Factry Club Formula Game Gold Gym Halloween Horror InterviewBoard Jail Labo LightStick MonitorRoom MusicRoom NeonSign NightView Nobility Office Otaku Party SF SFX Stage Steampunk Stadium StarPrint Success Training Train Wrestling");
            Add(result, "Graphic", "BlueSheet Cage Damask Dot DropCurtain Fantasy Festival HoneycombTile Minimul MovingBox MultiDot Sloppy StainedGlass Triangle Sauna");
            return result;
        }
        static void Add(Dictionary<string, string> map, string theme, string names)
        { foreach (string name in names.Split(' ')) map.Add(name, theme); }

        public static string[] RoomMaterials(string originalRoomId)
        {
            string stem = (originalRoomId ?? "").TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            string suffix = originalRoomId == null ? "" : originalRoomId.Substring(stem.Length);
            int.TryParse(suffix, out var variant);
            RoomThemes.TryGetValue(stem, out var theme);
            // These are existing ResidentialIsland materials, selected only for
            // presentation. No source room prices, variants or effects change.
            switch (theme)
            {
                case "Soft": return variant % 2 == 0 ? new[] { "rug", "cream" } : new[] { "oak", "sage" };
                case "Green": return variant % 2 == 0 ? new[] { "cedar", "sage" } : new[] { "oak", "leaf" };
                case "Stone": return variant % 2 == 0 ? new[] { "stone", "plaster" } : new[] { "stone", "charcoal" };
                case "Cool": return variant % 2 == 0 ? new[] { "porcelain", "sage" } : new[] { "stone", "porcelain" };
                case "Dark": return variant % 2 == 0 ? new[] { "charcoal", "cedar" } : new[] { "oak", "charcoal" };
                case "Graphic": return variant % 2 == 0 ? new[] { "rug", "sage" } : new[] { "cedar", "cream" };
                default: return variant % 2 == 0 ? new[] { "oak", "plaster" } : new[] { "cedar", "cream" };
            }
        }
        public static bool HasRoomTheme(string originalRoomId)
            => RoomThemes.ContainsKey((originalRoomId ?? "").TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'));
    }
}
