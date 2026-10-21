using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SushiAndFish.ResidentialIsland;
public static class MergedHousingInstallation
{
    [Serializable]public class Receipt
    {
        public string utc,project,entryScene,unity,error;
        public bool passed,importCompiled,catalog,clips,shaders,nativeSeat,prefabs,missingScripts,resourceLinks;
        public int homes,assetDependencies;public string[] dependencies;
    }
    public static void Validate()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports/residential-merged"));Directory.CreateDirectory(root);
        var r=new Receipt{utc=DateTime.UtcNow.ToString("o"),project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),entryScene="Assets/Scenes/Main.unity",unity=Application.unityVersion};
        try
        {
            r.importCompiled=!EditorApplication.isCompiling;
            var c=AssetDatabase.LoadAssetAtPath<HousingCatalog>("Assets/SushiAndFish/ResidentialIsland/Resources/Housing/Catalog.asset");r.catalog=c;
            if(!c)throw new Exception("Installed catalog missing");r.homes=c.homes.Length;
            r.clips=new[]{c.restIdle,c.restSit,c.restLay,c.restTuck,c.restBrace,c.restRecline}.All(a=>a&&a.humanMotion);
            r.shaders=c.floorPlanShader&&c.textShader&&c.graphicMaterial&&c.floorPlanShader.isSupported&&c.textShader.isSupported;
            r.nativeSeat=c.nativeSeatPrefab&&c.nativeSeatPrefab.GetComponent<global::EmoteArea>()&&c.nativeSeatPrefab.GetComponent<SushiAndFish.CityExpansion.CityDiningSeat>()&&c.nativeSeatPrefab.GetComponent<Unity.Netcode.NetworkObject>();
            r.prefabs=c.homes.Length==2&&c.homes.All(h=>h.prefab&&h.prefab.GetComponentsInChildren<HousingDoor>(true).Length==3&&h.prefab.GetComponentsInChildren<HousingRestInteractable>(true).Length==2&&h.prefab.GetComponent<HousingHouse>().chairs.Length==5);
            r.missingScripts=c.homes.SelectMany(h=>h.prefab.GetComponentsInChildren<Transform>(true)).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0);
            r.resourceLinks=c.font&&c.panelSprite&&c.buttonSprite&&c.upSprite&&c.downSprite&&c.closeSprite&&c.homes.All(h=>h.prefab.GetComponentsInChildren<HousingRestInteractable>(true).All(t=>t.seatHip&&t.sleepHip&&t.exit&&t.cameraPoint&&(t.kind!=HousingRestKind.Toilet||t.toilet&&t.toilet.lid)));
            r.dependencies=AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(c),true);r.assetDependencies=r.dependencies.Length;
            r.passed=r.importCompiled&&r.catalog&&r.clips&&r.shaders&&r.nativeSeat&&r.prefabs&&!r.missingScripts&&r.resourceLinks;
            if(!r.passed)throw new Exception("Installed module references contract failed");
        }
        catch(Exception e){r.error=e.ToString();throw;}
        finally{File.WriteAllText(Path.Combine(root,"merged-import-and-references.json"),JsonUtility.ToJson(r,true));}
    }
}
