using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using TomodachiLife;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    /// <summary>The existing R parental selection and stage-height body, with the host visual projection kept outside the core.</summary>
    public sealed class SourceFamilyAppearanceRuntime
    {
        SourceQuestionnaireRuntime questionnaire;
        public ResidentEditDocument Build(LifeSimulationRuntime simulation, LifeFamilyChild child, out HashSet<string> secondParentParts, out bool isNew)
        {
            if (questionnaire == null) questionnaire = new SourceQuestionnaireRuntime(simulation); else questionnaire.Simulation = simulation;
            secondParentParts = new HashSet<string>(StringComparer.Ordinal);
            var resident = simulation.FindResident(child.residentId);
            isNew = string.IsNullOrWhiteSpace(resident.avatarJson);
            ResidentEditDocument document;
            if (isNew)
            {
                // Original IslandLifeGameFamily.cs:28-44. Same seed expansion,
                // selection order, integer means and clamping as the R source.
                var a = simulation.FindResident(child.parentAId); var b = simulation.FindResident(child.parentBId);
                var first = string.IsNullOrEmpty(a?.avatarJson) ? questionnaire.NewQuestionnaireDocument() : JsonUtility.FromJson<ResidentEditDocument>(a.avatarJson);
                var second = string.IsNullOrEmpty(b?.avatarJson) ? first : JsonUtility.FromJson<ResidentEditDocument>(b.avatarJson);
                document = JsonUtility.FromJson<ResidentEditDocument>(JsonUtility.ToJson(first));
                var random = NativeMiiEventRandom.FromSystemTick(child.birthSeed);
                for (int i = 0; i < document.appearance.parts.Length; i++)
                {
                    var selected = document.appearance.parts[i]; var alternative = second.appearance.Selection(selected.category);
                    if (alternative != null && (NativeMiiEventRandom.NextUInt(ref random) & 1) != 0)
                    { document.appearance.parts[i] = JsonUtility.FromJson<AvatarSelection>(JsonUtility.ToJson(alternative)); secondParentParts.Add(selected.category); }
                }
                document.appearance.build = (first.appearance.build + second.appearance.build) / 2;
                document.appearance.voicePitch = Mathf.Clamp((first.appearance.voicePitch + second.appearance.voicePitch) / 2 + 15, 0, 100);
                document.appearance.voiceSpeed = (first.appearance.voiceSpeed + second.appearance.voiceSpeed) / 2;
                document.appearance.facePaintPixels = new int[256]; document.catchphrase = ""; document.hobby = "";
                document.parameterSource = "Reconstructed deterministic parental-part inheritance, saved birth seed " + child.birthSeed;
            }
            else document = JsonUtility.FromJson<ResidentEditDocument>(resident.avatarJson);
            document.name = resident.displayName; document.residentId = resident.id;
            document.appearance.height = child.stage == "Baby" ? 12 : child.stage == "Toddler" ? 24 : child.stage == "Child" ? 42 : child.stage == "Teen" ? 63 : 80;
            document.Validate(questionnaire.Catalog);
            return document;
        }

        public static CharacterData ProjectParentalParts(CharacterData first, CharacterData second, HashSet<string> sourceSecondCategories)
        {
            var result = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(first));
            if (second == null) return result;
            var slots = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Hair"] = new[] { "HairFront", "HairBack" }, ["Faceline"] = new[] { "Head" },
                ["Eye"] = new[] { "Eyes", "EyeLashes", "EyeShine" }, ["Eyebrow"] = new[] { "EyeBrows" }, ["Ear"] = new[] { "Ears" }
            };
            foreach (string category in sourceSecondCategories)
                if (slots.TryGetValue(category, out var types))
                    foreach (string type in types)
                    {
                        var parts = second.outfitDatas.Where(o => o.outfit != null && o.outfit.Split('/')[0].Equals(type, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (parts.Length == 0) continue;
                        result.outfitDatas.RemoveAll(o => o.outfit != null && o.outfit.Split('/')[0].Equals(type, StringComparison.OrdinalIgnoreCase));
                        result.outfitDatas.AddRange(parts);
                    }
            var shapeGroups = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Faceline"] = new[] { "Squareness", "Sharpness", "Stern" },
                ["Eye"] = new[] { "EyeLidHeight", "EyesSquare", "EyesOuterCornersLow", "EyesOuterCornersHigh", "IrisSize", "LashLength" },
                ["Eyebrow"] = new[] { "BrowsThickness", "LowerBrows", "RaiseBrows" },
                ["Nose"] = new[] { "NoseBridgeCurve", "NoseWidth", "NoseTiltDown", "NoseTiltUp" },
                ["Mouth"] = new[] { "MouthWide", "MouthThin" }, ["Ear"] = new[] { "EarsElf", "EarAngle" }
            };
            if (result.faceIDs != null && result.faceShapes != null && second.faceIDs != null && second.faceShapes != null)
                foreach (string category in sourceSecondCategories)
                    if (shapeGroups.TryGetValue(category, out var keys))
                        foreach (string key in keys)
                        {
                            int target = result.faceIDs.IndexOf(key), from = second.faceIDs.IndexOf(key);
                            if (target >= 0 && from >= 0 && target < result.faceShapes.Count && from < second.faceShapes.Count) result.faceShapes[target] = second.faceShapes[from];
                        }
            return result;
        }
    }
}
