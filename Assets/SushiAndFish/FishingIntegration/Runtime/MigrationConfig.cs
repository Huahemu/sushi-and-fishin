using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SushiAndFish.FishingIntegration {
    [Serializable] public sealed class FishingSpeciesBinding {
        public FishDefinition definition;
        public FishSO original,shiny;
        public Sprite icon;
        public Material shinyIconMaterial;
        public bool edible=true;
        public string ingredientRoute;
        public FishSO Item(bool alternate)=>alternate&&shiny?shiny:original;
    }
    public sealed class MigrationConfig:ScriptableObject {
        public bool enabled=true;
        public GameObject systemPrefab;
        public GameObject nativeHintTemplate;
        public TMP_SpriteAsset nativeHintSprites;
        public FishingProfile profile;
        public ItemDatabaseSO database;
        public FishingSpeciesBinding[] species;
        public Material grayscale,silhouette;
        public Sprite panelSprite,buttonSprite,rodIcon,gunIcon,warehouseIcon,stowIcon;
        public Sprite[] equipmentIcons;
        public FishingWeaponOffer[] weapons;
        public TextAsset iconGeometry;
        public PaperPixelGames.Framework.UI.BaseButton nativeButtonTemplate;
        public TMP_FontAsset font;
        public Font legacyFont;
        public float seaHeight=-4;
        public FishingSpeciesBinding Find(int id)=>species?.FirstOrDefault(x=>x.definition.originalId==id);
        public FishingSpeciesBinding Find(string id)=>species?.FirstOrDefault(x=>x.original.Id==id||x.shiny&&x.shiny.Id==id);
        public bool IsShiny(string id){var b=Find(id);return b!=null&&b.shiny&&b.shiny.Id==id;}
        public static MigrationConfig Load(){var config=Resources.Load<MigrationConfig>("FishingMigration");FishingIconGeometry.Apply(config);return config;}
    }
    [Serializable] public sealed class StoredFish {
        public string token,itemId;
        public float weight;
    }
    [Serializable] public sealed class WarehouseLine {public string itemId;public long count;}
    [Serializable] public sealed class SavedCatch {
        public string token;
        public int species,health;
        public ulong fisher,steam;
        public Vector3 position;
        public Quaternion rotation;
        public float weight;
        public bool shiny,reachedShore,seen;
    }
    [Serializable] public sealed class FishingWorldSave {
        public int version=1;
        public int ownedWeapons;
        public SavedBoat boat;
        public FishingArchiveData archive=new FishingArchiveData();
        public System.Collections.Generic.List<StoredFish> stock=new System.Collections.Generic.List<StoredFish>();
        public System.Collections.Generic.List<SavedCatch> pending=new System.Collections.Generic.List<SavedCatch>();
        public System.Collections.Generic.List<string> acquired=new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> resolved=new System.Collections.Generic.List<string>();
    }
}
