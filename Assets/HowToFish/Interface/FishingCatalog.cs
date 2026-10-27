using System;
using UnityEngine;
namespace HowToFish.Interface {
    [Serializable] public sealed class FishCatalogEntry {
        public FishDefinition definition;
        public Texture2D original,shiny;
        public bool HasShiny=>shiny;
        public string Title=>definition.seaHorse?"海马（真马）":definition.displayName;
    }
    public sealed class FishingCatalog:ScriptableObject {
        public FishCatalogEntry[] entries;
        public Material grayscaleMaterial,silhouetteMaterial;
    }
}
