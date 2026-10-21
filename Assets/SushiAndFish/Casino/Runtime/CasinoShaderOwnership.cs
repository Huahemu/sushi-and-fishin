using UnityEngine;

namespace SushiAndFish.Casino
{
    public static class CasinoShaderOwnership
    {
        public const string Tag = "SushiAndFishOriginalCasino";
        public static bool Owns(Material material) => material && material.GetTag(Tag, false, "") == "1";
    }
}
