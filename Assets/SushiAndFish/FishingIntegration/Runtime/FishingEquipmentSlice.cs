using System;
using System.Linq;
using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Wheel;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public sealed class FishingEquipmentSlice:ScriptableObject,IWheelSlice {
        public string title;public Sprite icon;public int index;
        public string WheelName=>title;public Sprite WheelSprite=>icon;
    }
}
