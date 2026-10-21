using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FMODUnity;
using SushiAndFish.Casino.Original;
using UnityEngine;

namespace SushiAndFish.Casino
{
    // FMOD's SDK loads a fixed Resources path. Bind its imported settings and
    // platform subassets explicitly without exposing a second global resource.
    public static class CasinoAudioBridge
    {
        static FMODUnity.Settings settings;
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        public static void Prepare()
        {
            if(settings)return;
            var source=CasinoResources.Load<FMODUnity.Settings>("FMODStudioSettings");
            if(!source)throw new InvalidOperationException("Original casino FMOD settings were not imported.");
            settings=UnityEngine.Object.Instantiate(source);
            settings.name="Casino original audio settings (runtime)";
            settings.Platforms.Clear();
            foreach(var platform in CasinoResources.LoadAll<Platform>("FMODStudioSettings"))
            {
                var clone=UnityEngine.Object.Instantiate(platform);
                typeof(Platform).GetMethod("EnsurePropertiesAreValid",Flags).Invoke(clone,null);
#if UNITY_EDITOR
                if(Application.isBatchMode)typeof(Platform).GetField("OutputTypeName",Flags)?.SetValue(clone,"NOSOUND");
#endif
                settings.Platforms.Add(clone);
            }
            settings.DefaultPlatform=settings.Platforms.First(p=>p.GetType().Name=="PlatformDefault");
            settings.PlayInEditorPlatform=settings.Platforms.First(p=>p.GetType().Name=="PlatformPlayInEditor");
            typeof(FMODUnity.Settings).GetField("instance",Flags).SetValue(null,settings);
            ((IDictionary)typeof(FMODUnity.Settings).GetField("PlatformForRuntimePlatform",Flags).GetValue(settings)).Clear();
            foreach(var platform in settings.Platforms)typeof(FMODUnity.Settings).GetMethod("LinkPlatform",Flags).Invoke(settings,new object[]{platform});
            settings.HasPlatforms=false;
            settings.SourceBankPath=Application.streamingAssetsPath;
            settings.TargetBankFolder="Casino";
            settings.ImportType=ImportType.StreamingAssets;
            Debug.Log("PARK_CASINO_AUDIO_BOUND platforms="+settings.Platforms.Count+" banks="+CasinoPaths.StreamingAssetsPath);
        }
        public static void ReleaseSettings()
        {
            if(!settings)return;
            foreach(var platform in settings.Platforms)if(platform)UnityEngine.Object.Destroy(platform);
            if(typeof(FMODUnity.Settings).GetField("instance",Flags).GetValue(null)==settings)typeof(FMODUnity.Settings).GetField("instance",Flags).SetValue(null,null);
            UnityEngine.Object.Destroy(settings);settings=null;
        }
    }
}
