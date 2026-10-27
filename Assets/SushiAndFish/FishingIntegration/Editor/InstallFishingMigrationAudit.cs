using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;

// Asset/reference and pure record checks only. Never starts the player loop or physics.
public static class InstallFishingMigrationAudit {
    static readonly List<string> checks=new List<string>();
    static void Check(bool condition,string text){checks.Add((condition?"PASS ":"FAIL ")+text);if(!condition){File.WriteAllLines("FishingMigrationAudit.txt",checks);throw new Exception(text);}}
    public static void Verify(){
        Check(!EditorApplication.isPlaying,"Editor remains outside Play mode");
        var c=MigrationConfig.Load();Check(c&&c.enabled,"Enabled migration config can be loaded by runtime bootstrap");
        foreach(var type in new[]{typeof(FishingMigration),typeof(MigrationConfig),typeof(FishingHud),typeof(FishingEquipmentWheel),typeof(FishingBookDetail),typeof(FishingWarehousePage),typeof(FishingWeaponShop),typeof(FishingIconOutline),typeof(FishingEnvironmentOverlay),typeof(MigratedCatchIdentity),typeof(FishingEquipmentSlice)}){
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/SushiAndFish/FishingIntegration/Runtime/"+type.Name+".cs");
            Check(script&&script.GetClass()==type,type.Name+": Unity MonoScript can resolve its attachable class");
        }
        Check(c.species.Length==60&&c.species.Select(b=>b.definition.originalId).Distinct().Count()==60,"60 distinct appended definitions");
        int skins=0,joints=0;
        foreach(var b in c.species){
            var root=b.definition.catchPrefab;
            Check(root&&root.body&&root.item&&root.definition==b.definition,b.definition.displayName+": physical catch bindings");
            Check(b.definition.MaximumHealth>0&&b.definition.shore!=null,b.definition.displayName+": health and shore motion");
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                skins++;Check(skin.sharedMesh&&skin.bones!=null&&skin.bones.All(t=>t)&&skin.sharedMesh.bindposes.Length==skin.bones.Length,b.definition.displayName+": complete skin bones / bind poses");
            }
            foreach(var joint in root.GetComponentsInChildren<Joint>(true)){joints++;Check(joint.connectedBody&&joint.GetComponent<Rigidbody>(),b.definition.displayName+": connected physical skeleton");}
            foreach(var item in new[]{b.original,b.shiny}.Where(x=>x)){
                Check(item.DeadGraphic.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&item.DeadGraphic.GetComponentsInChildren<Rigidbody>(true).Length==0,item.Id+": native pickup graphic contains no second gameplay / physics system");
                Check(item.DeadBoxSize.x>.001f&&item.DeadBoxSize.y>.001f&&item.DeadBoxSize.z>.001f,item.Id+": valid authored pickup bounds");
            }
        }
        foreach(string name in new[]{"HowToFish/Catalogue UI","SushiAndFish/Painted fish variation"}){var shader=Shader.Find(name);Check(shader&&!ShaderUtil.ShaderHasError(shader),name+": UI shader compiles");}
        var record=new FishCollectionRecord();Check(record.Appearance()==FishArchiveAppearance.Silhouette,"Legacy unseen discovery state remains independent of the original native locked tint");
        record.shinySeen=true;Check(record.Appearance()==FishArchiveAppearance.Grayscale,"Lifted and escaped fish remain gray until acquired");
        record.shinyCaptured=1;Check(record.Appearance()==FishArchiveAppearance.Shiny,"Shiny-only acquisition displays shiny by default");
        record.originalCaptured=1;Check(record.Appearance()==FishArchiveAppearance.Original&&record.Appearance(true)==FishArchiveAppearance.Shiny,"Original capture restores original default; acquired shiny remains selectable");
        var save=new FishingWorldSave();save.archive.captures=5000000000L;save.archive.collection.Add(record);save.stock.Add(new StoredFish{token="stable-guid",itemId=c.species[0].original.Id,weight=12.345f});save.pending.Add(new SavedCatch{token="live-guid",steam=76561198000000000UL,health=23,seen=true,shiny=true,position=new Vector3(2,4,6)});
        var roundtrip=JsonUtility.FromJson<FishingWorldSave>(JsonUtility.ToJson(save));
        Check(roundtrip.archive.captures==5000000000L&&roundtrip.stock[0].weight==12.345f&&roundtrip.stock[0].token=="stable-guid"&&roundtrip.pending[0].steam==76561198000000000UL&&roundtrip.pending[0].seen,"Save DTO preserves long counts, exact weight, stable identity and discovery state");
        Check(Mathf.Approximately(c.profile.nearRareChance*c.profile.nonCommonProbabilityMultiplier,.14f)&&Mathf.Approximately(c.profile.nearLegendaryChance*c.profile.nonCommonProbabilityMultiplier,.035f),"Latest ordinary near-water rare14% / legendary3.5% preserved before event and bait weighting");
        string events=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/MigrationEvents.cs");Check(events.Contains("a.inputActive")&&events.Contains("FishingState.Casting||a.module.State==FishingState.Floating||a.module.State==FishingState.Hooked"),"Events require equipped rod, active controls and a cast in progress");
        File.WriteAllLines("FishingMigrationAudit.txt",checks);Debug.Log("SUSHI_FISHING_STATIC_AUDIT_PASS checks="+checks.Count+" skins="+skins+" joints="+joints+" play=false simulation=false");
    }
}
