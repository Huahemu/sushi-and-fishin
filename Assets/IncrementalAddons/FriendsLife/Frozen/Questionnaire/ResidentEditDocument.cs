using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TomodachiLife
{
    [Serializable] public sealed class ResidentEditDocument
    {
        public int schema = 1;
        public string residentId, name, catchphrase, hobby, savedUtc;
        public string parameterSource;
        public bool originalRuntimeParity;
        public bool includeEyeShadow, includeLipColor;
        public ResidentAppearance appearance;
        public NativeFaceRawParameters[] neutralParameters;

        public static ResidentEditDocument CreateInspection(AvatarCatalog catalog)
        {
            var appearance = ResidentAppearance.InspectionDefault(catalog);
            var layout = NativeFaceLayout.Load();
            return new ResidentEditDocument {
                residentId = Guid.NewGuid().ToString("N"), name = "新居民", catchphrase = "", hobby = "",
                parameterSource = "Explicit inspection fixtures; original initial resident preset still unverified",
                appearance = appearance,
                neutralParameters = NativeFaceCompositor.CreateInspectionNeutral(appearance, catalog, layout)
                    .Where(p => !p.raw.right).Select(p => p.raw).ToArray()
            };
        }
        public NativeFaceLayerInput[] FaceLayers(AvatarCatalog catalog)
        {
            var layers = NativeFaceCompositor.CreateInspectionNeutral(appearance, catalog, NativeFaceLayout.Load());
            foreach (var layer in layers)
            {
                var original = Array.Find(neutralParameters, p => p.kind == layer.raw.kind && !p.right);
                if (original == null) throw new InvalidDataException("Missing explicit neutral raw values for " + layer.raw.kind);
                layer.raw.x = original.x; layer.raw.y = original.y; layer.raw.rotation = original.rotation;
                layer.raw.scale = original.scale; layer.raw.aspect = original.aspect;
                layer.raw.rawSource = parameterSource + "; selected part axis/offset: " + catalog.Part(appearance.Selection(layer.raw.kind).part).sourceRow;
                layer.rawParametersVerified = false;
                var selected = appearance.Selection(layer.raw.kind);
                var part = catalog.Part(selected.part);
                Color primary = catalog.Color("common", selected.color, false);
                Color red = layer.raw.kind == "Eye" ? catalog.Color("common", selected.subColor, false) : primary;
                Color green = layer.raw.kind == "Mouth" ? catalog.Color("upperLip", selected.color, false) : Color.white;
                Color blue = layer.raw.kind == "Eye" ? primary : Color.white;
                layer.modulate.mode = part.useTextureColor ? 1 : layer.raw.kind == "Eyebrow" ? 3 :
                    layer.raw.kind == "Eye" ? (includeEyeShadow ? 2 : 7) : (includeLipColor ? 2 : 8);
                layer.modulate.color0 = Rgba(red); layer.modulate.color1 = Rgba(green); layer.modulate.color2 = Rgba(blue);
                layer.modulate.bindingVerified = true; layer.modulate.colorsVerified = true;
                layer.modulate.source = "Explicit editable shadow/lip flags and selected color indices; native descriptor0016facc, SDK sRGB color accessors and upload0016f980; current original resident flags not asserted";
            }
            return layers;
        }
        private static float[] Rgba(Color color) => new[] {color.r,color.g,color.b,color.a};
        public void Validate(AvatarCatalog catalog)
        {
            if (schema != 1 || string.IsNullOrWhiteSpace(residentId) || string.IsNullOrWhiteSpace(parameterSource) ||
                appearance == null || appearance.schema != 1 || appearance.parts == null || neutralParameters == null || originalRuntimeParity)
                throw new InvalidDataException("Unsupported or unverified resident edit document contract.");
            if (appearance.height < 0 || appearance.height > 127 || appearance.build < 0 || appearance.build > 127)
                throw new InvalidDataException("Body parameters must remain in the native 0..127 domain.");
            if (!string.IsNullOrEmpty(appearance.skinColorDomain) && appearance.skinColorDomain != "game" && appearance.skinColorDomain != "legacySdk")
                throw new InvalidDataException("Unknown native skin color domain.");
            catalog.SkinColor(appearance);
            var categories = new HashSet<string>();
            foreach (var selected in appearance.parts)
            {
                if (!categories.Add(selected.category)) throw new InvalidDataException("Duplicate resident part category: " + selected.category);
                var part = catalog.Part(selected.part);
                if (part.category != selected.category) throw new InvalidDataException("Resident part category differs: " + part.id);
                catalog.Color(selected.category == "Mouth" ? "upperLip" : "common", selected.color);
                catalog.Color("common", selected.subColor);
            }
            foreach (string required in new[] { "Faceline", "Ear", "Nose", "Hair", "Eye", "Eyebrow", "Mouth" })
                if (appearance.Selection(required) == null) throw new InvalidDataException("Resident required part missing: " + required);
            if (neutralParameters.Length != 3 || neutralParameters.Select(p => p.kind).Distinct().Count() != 3)
                throw new InvalidDataException("Expected one neutral Eye/Eyebrow/Mouth parameter record each.");
            var evaluator = NativeFaceLayout.Load();
            foreach (var p in neutralParameters)
                if (!evaluator.TryEvaluateNeutral(p, "NativeFlippedY", out _, out string error)) throw new InvalidDataException(error);
        }
    }

    // These are migration-owned editable character slots, never original .sav
    // files. Validate before replacement; retain the previous known-good slot.
    public static class ResidentEditStore
    {
        public static string Save(string path, ResidentEditDocument document, AvatarCatalog catalog)
        {
            document.Validate(catalog);
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            document.savedUtc = DateTime.UtcNow.ToString("O");
            string temporary = full + ".pending";
            File.WriteAllText(temporary, JsonUtility.ToJson(document, true));
            Load(temporary, catalog);
            if (File.Exists(full)) File.Replace(temporary, full, full + ".previous");
            else File.Move(temporary, full);
            return full;
        }
        public static ResidentEditDocument Load(string path, AvatarCatalog catalog)
        {
            var document = JsonUtility.FromJson<ResidentEditDocument>(File.ReadAllText(path));
            if (document == null) throw new InvalidDataException("Resident slot is empty.");
            document.Validate(catalog); return document;
        }
    }
}
