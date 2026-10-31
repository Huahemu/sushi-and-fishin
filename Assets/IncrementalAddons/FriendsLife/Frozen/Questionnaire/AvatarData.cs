using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TomodachiLife
{
    [Serializable] public sealed class AvatarModelVariant { public string key, model; }
    [Serializable] public sealed class AvatarPart
    {
        public string id, category, sourceRow, model, flippedModel, hatModel, flippedHatModel;
        public string texture, noLipTexture, icon, maskIcon;
        public uint typeHash;
        public int nativeIndex, defaultAspect, defaultScale, defaultX, defaultY, maximumX, minimumY, maximumY, rotationOffset;
        public bool visible, flippable, colorable, subColorable, useTextureColor;
        public float[] expressionAxis;
        public float expressionSize;
        public AvatarModelVariant[] hairPartsModels, hairPartsHatModels;
        // Native virtual accessor00173d58 adds category table029fe308 to the
        // RSDB OffsetRotate field at row+0x108. Eye=4, Eyebrow=6, Mouth=0.
        public int NativeRotationOffset => rotationOffset + (category == "Eye" ? 4 : category == "Eyebrow" ? 6 : 0);
    }
    [Serializable] public sealed class AvatarPartOrder { public string category; public int[] indices; }
    [Serializable] public sealed class AvatarColorEntry
    {
        public string table; public int index; public float[] linear, srgb;
        public Color Linear => new Color(linear[0], linear[1], linear[2], 1);
        public Color Srgb => new Color(srgb[0], srgb[1], srgb[2], 1);
    }
    [Serializable] public sealed class AvatarPositionMap { public int index; public string resource; }
    [Serializable] public sealed class AvatarCatalog
    {
        public int schema;
        public string source;
        public bool originalRuntimeParity;
        public AvatarPart[] parts;
        public AvatarPartOrder[] orders, colorOrders;
        public AvatarColorEntry[] colors;
        public AvatarPositionMap[] facePositionMaps;
        [NonSerialized] private Dictionary<string, AvatarPart> lookup;
        public AvatarPart Part(string id)
        {
            if (lookup == null) lookup = parts.ToDictionary(p => p.id, StringComparer.Ordinal);
            return lookup.TryGetValue(id, out var part) ? part : throw new ArgumentException("Unknown original avatar part " + id);
        }
        public IEnumerable<AvatarPart> Category(string category)
        {
            var values = parts.Where(p => p.category == category && p.visible);
            var order = orders.FirstOrDefault(o => o.category == category)?.indices;
            return order == null || order.Length == 0 ? values.OrderBy(p => p.nativeIndex) :
                values.OrderBy(p => { int i = Array.IndexOf(order, p.nativeIndex); return i < 0 ? int.MaxValue : i; }).ThenBy(p => p.nativeIndex);
        }
        public Color Color(string table, int index, bool linear = true)
        {
            var item = colors.FirstOrDefault(c => c.table == table && c.index == index);
            if (item == null) throw new ArgumentException("Original color does not exist: " + table + ":" + index);
            return linear ? item.Linear : item.Srgb;
        }
        public Color SkinColor(ResidentAppearance appearance, bool linear = true)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            // The original game uses CommonColor below100 and the native skin
            // tables at100..113. Older SDK fixtures used skin0..9 directly.
            // Keep the two domains explicit when loading existing documents.
            return appearance.skinColorDomain == "game" && appearance.skinColor < 100 ?
                Color("common", appearance.skinColor, linear) : Color("skin", appearance.skinColor, linear);
        }
        public static AvatarCatalog Load()
        {
            var source = Resources.Load<TextAsset>("Avatar/catalog");
            if (source == null) throw new InvalidOperationException("Original avatar catalog has not been staged.");
            var catalog = JsonUtility.FromJson<AvatarCatalog>(source.text);
            if (catalog.schema != 1 || catalog.parts.Length != 779) throw new InvalidOperationException("Avatar source contract differs.");
            return catalog;
        }
    }
    [Serializable] public sealed class AvatarSelection
    {
        public string category, part;
        public int positionX, positionY, rotation, scale, aspect = 3, color = 8, subColor = 8;
        public bool flipped, enabled = true;
        public static AvatarSelection From(AvatarPart definition) => new AvatarSelection {
            category = definition.category, part = definition.id, positionX = definition.defaultX, positionY = definition.defaultY,
            scale = definition.defaultScale, aspect = definition.defaultAspect
        };
    }
    [Serializable] public sealed class ResidentAppearance
    {
        public int schema = 1;
        public int skinColor = 1, favoriteColor, height = 64, build = 64;
        public string skinColorDomain = "legacySdk";
        // Migration-owned editable profile values. Voice values are saved
        // parameters only until the original voice runtime is ported.
        public int walkStyle, personalityEnergy = 2, personalitySpeech = 2, personalityExpression = 2, personalityMood = 2, personalityManners = 2;
        public int voicePitch = 50, voiceSpeed = 50, voiceTone = 50;
        public int[] facePaintPixels = new int[256];
        public AvatarSelection[] parts;
        public static ResidentAppearance InspectionDefault(AvatarCatalog catalog)
        {
            string[] ids = { "Faceline00", "Ear00", "Nose00", "HairAll000", "Eye000", "Eyebrow00", "Mouth000" };
            var appearance = new ResidentAppearance { parts = ids.Select(id => AvatarSelection.From(catalog.Part(id))).ToArray() };
            foreach (var part in appearance.parts)
            {
                if (part.category == "Hair") part.color = 1;
                if (part.category == "Mouth") part.color = 19;
            }
            return appearance;
        }
        public AvatarSelection Selection(string category) => parts?.FirstOrDefault(p => p.category == category && p.enabled);
    }
}
