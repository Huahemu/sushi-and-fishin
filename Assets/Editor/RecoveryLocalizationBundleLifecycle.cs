using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Recovered Addressables DLLs do not register the package's editor Play-stop
// cleanup. Release only localization bundle containers outside Play so the
// next domain can load them without the native "same files already loaded" error.
[InitializeOnLoad]
public static class RecoveryLocalizationBundleLifecycle
{
    static RecoveryLocalizationBundleLifecycle() {
        EditorApplication.playModeStateChanged+=OnState;
        EditorApplication.delayCall+=()=>{if(!EditorApplication.isPlayingOrWillChangePlaymode)Release();};
    }
    static void OnState(PlayModeStateChange state) {
        if(state==PlayModeStateChange.ExitingEditMode||state==PlayModeStateChange.EnteredEditMode)Release();
    }
    static void Release() {
        var loaded=AssetBundle.GetAllLoadedAssetBundles().Where(b=>b).ToArray();
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"localization-loaded-bundles.json"),JsonUtility.ToJson(new BundleSnapshot{utc=DateTime.UtcNow.ToString("o"),bundles=loaded.Select(b=>new BundleRow{name=b.name,assets=b.GetAllAssetNames()}).ToArray()},true));
        var bundles=loaded.Where(b=>b.name.IndexOf("localization-",StringComparison.OrdinalIgnoreCase)>=0||b.name.Contains("_monoscripts_")).ToArray();
        if(bundles.Length==0)return;
        var names=bundles.Select(b=>b.name).ToArray();foreach(var bundle in bundles)bundle.Unload(true);
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"localization-bundle-lifecycle.json"),JsonUtility.ToJson(new Receipt{utc=DateTime.UtcNow.ToString("o"),unloaded=names},true));
        Debug.Log("RECOVERY_LOCALIZATION_BUNDLES_RELEASED "+names.Length);
    }
    [Serializable] class Receipt {public string utc;public string[] unloaded;}
    [Serializable] class BundleSnapshot {public string utc;public BundleRow[] bundles;}
    [Serializable] class BundleRow {public string name;public string[] assets;}
}
