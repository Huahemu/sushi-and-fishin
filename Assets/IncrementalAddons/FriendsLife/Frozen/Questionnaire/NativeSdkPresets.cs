using System;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace TomodachiLife
{
    [Serializable] public sealed class NativeSdkPresetFields
    {
        public int FontRegion, FavoriteColor, Gender, Height, Build, RegionMove;
        public int FacelineType, FacelineColor, FacelineWrinkle, FacelineMake, HairType, HairColor, HairFlip;
        public int EyeType, EyeColor, EyeScale, EyeAspect, EyeRotate, EyeX, EyeY;
        public int EyebrowType, EyebrowColor, EyebrowScale, EyebrowAspect, EyebrowRotate, EyebrowX, EyebrowY;
        public int NoseType, NoseScale, NoseY, MouthType, MouthColor, MouthScale, MouthAspect, MouthY;
        public int BeardType, BeardColor, MustacheType, MustacheScale, MustacheY;
        public int GlassType, GlassColor, GlassScale, GlassY, MoleType, MoleScale, MoleX, MoleY;
        public int RawByte2B, RawByte57;
    }
    [Serializable] public sealed class NativeSdkPresetFace
    {
        public string kind, matchingCatalogPart;
        public NativeFaceRawParameters sdkRaw, gameConvertedRaw;
        public int sdkColor;
        public bool sourceVerified, gameInitialPresetVerified;
    }
    [Serializable] public sealed class NativeSdkPresetPart
    { public string category; public int sdkType, gameConvertedType; public string[] matchingCatalogParts; }
    [Serializable] public sealed class NativeSdkMiiPreset
    {
        public string id, visualBytesHex;
        public int tableIndex, tableAddress, visualByteStart;
        public int[] rawUint32Words, visualBytes;
        public NativeSdkPresetFields fields;
        public NativeSdkPresetFace[] neutralFace;
        public NativeSdkPresetPart[] parts;
        public bool sourceVerified, gameInitialPresetVerified;
    }
    [Serializable] public sealed class NativeSdkMiiPresets
    {
        public int schema;
        public string source;
        public bool sdkTemplatesVerified, gameInitialPresetVerified, runtimeParity;
        public NativeSdkMiiPreset[] presets;
        public NativeSdkMiiPreset Get(string id)
        {
            var value = presets == null ? null : Array.Find(presets, p => p.id == id);
            if (value == null) throw new ArgumentException("Unknown original SDK template ID: " + id);
            return value;
        }
#if UNITY_5_3_OR_NEWER
        public static NativeSdkMiiPresets Load() => NativeSdkPresets.Load();

        public ResidentEditDocument CreateDocument(string id, AvatarCatalog catalog)
        {
            var preset = Get(id);
            if (!preset.sourceVerified || preset.fields == null || preset.neutralFace == null || preset.neutralFace.Length != 3)
                throw new InvalidOperationException("SDK template lacks source-verified visual inputs.");
            var fields = preset.fields;
            var selections = new List<AvatarSelection>();
            foreach (var mapping in preset.parts)
            {
                if (mapping.matchingCatalogParts == null || mapping.matchingCatalogParts.Length != 1)
                    throw new InvalidOperationException("SDK type requires one explicit catalog mapping: " + mapping.category);
                var part = catalog.Part(mapping.matchingCatalogParts[0]);
                if (part.category != mapping.category || part.nativeIndex != mapping.gameConvertedType)
                    throw new InvalidOperationException("SDK/catalog type mapping differs: " + mapping.category);
                // Explicit SDK/game values below replace source table slider
                // defaults. No DefaultTrans/DefaultScale is used as a raw value.
                var selected = new AvatarSelection { category=part.category,part=part.id,enabled=true,color=8,subColor=8,aspect=3 };
                if (mapping.category=="Hair") {selected.color=fields.HairColor;selected.flipped=fields.HairFlip!=0;}
                else if(mapping.category=="Nose") {selected.scale=fields.NoseScale;selected.positionY=fields.NoseY;}
                else if(mapping.category=="Eye" || mapping.category=="Eyebrow" || mapping.category=="Mouth")
                {
                    var face=Array.Find(preset.neutralFace,p=>p.kind==mapping.category);
                    var raw=face.gameConvertedRaw;
                    selected.color=face.sdkColor;selected.positionX=raw.x;selected.positionY=raw.y;
                    selected.scale=raw.scale;selected.aspect=raw.aspect;selected.rotation=raw.rotation;
                }
                selections.Add(selected);
            }
            var inspection=ResidentAppearance.InspectionDefault(catalog);
            var ear=inspection.Selection("Ear");
            if(ear==null) throw new InvalidOperationException("Explicit inspection Ear fallback missing.");
            selections.Add(new AvatarSelection {category=ear.category,part=ear.part,positionX=ear.positionX,positionY=4,
                rotation=ear.rotation,scale=2,aspect=ear.aspect,color=ear.color,subColor=ear.subColor,flipped=ear.flipped,enabled=ear.enabled});
            var neutral=new NativeFaceRawParameters[3];
            for(int i=0;i<neutral.Length;i++)
            {
                var raw=preset.neutralFace[i].gameConvertedRaw;
                neutral[i]=new NativeFaceRawParameters {kind=raw.kind,x=raw.x,y=raw.y,rotation=raw.rotation,scale=raw.scale,aspect=raw.aspect,
                    offsetRotate=raw.offsetRotate,axis=(float[])raw.axis.Clone(),right=false,rawSource=raw.rawSource};
            }
            return new ResidentEditDocument {residentId=Guid.NewGuid().ToString("N"),name="SDK 模板 · "+preset.id,catchphrase="",hobby="",
                originalRuntimeParity=false,
                parameterSource="Original SDK DefaultMiiTable "+preset.id+" (row "+preset.tableIndex+") and source-decoded game converter 006a5fe4; SDK template verified, current original-game initial preset unverified. " +
                    "Ear00/scale2/Y4 follows the explicit game converter006a5fe4 defaults. " +
                    "Eye shadow color/channel flags, mouth red-channel flag, full 3D nose/body parameter application and rendered parity remain unverified. " +
                    "FavoriteColor is the table value; native SetDefaultMii callers may override it. Migration resident ID/name are newly authored.",
                appearance=new ResidentAppearance {skinColor=fields.FacelineColor,favoriteColor=fields.FavoriteColor,height=fields.Height,build=fields.Build,parts=selections.ToArray()},
                neutralParameters=neutral};
        }
#endif
    }

    public static class NativeSdkPresets
    {
#if UNITY_5_3_OR_NEWER
        public static NativeSdkMiiPresets Load()
        {
            var text=Resources.Load<TextAsset>("Avatar/sdk-presets");
            if(text==null) throw new InvalidOperationException("Original SDK preset resource missing.");
            var value=JsonUtility.FromJson<NativeSdkMiiPresets>(text.text);
            if(value.schema!=1 || value.presets==null || value.presets.Length!=8 || !value.sdkTemplatesVerified || value.gameInitialPresetVerified || value.runtimeParity)
                throw new InvalidOperationException("Original SDK preset resource contract differs.");
            return value;
        }
        public static NativeSdkMiiPreset Get(string id) => Load().Get(id);
        public static ResidentEditDocument CreateDocument(string id, AvatarCatalog catalog) => Load().CreateDocument(id,catalog);
#endif
    }
}
