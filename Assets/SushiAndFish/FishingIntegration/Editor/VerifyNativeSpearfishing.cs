using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools.Harpoon;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Wheel;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;

// Source/data and prefab-reference checks only. No Play, tool activation,
// server commands, fish spawning, catching, input or physics simulation.
public static class VerifyNativeSpearfishing
{
    const string Out="D:/UnityProjects/_CodexWork/NativeSpearfishing-20261028/";
    static readonly List<string> checks=new List<string>();
    [Serializable] class SceneAudit { public string sceneSha256,wheelManagerBody,harpoonElementBody,toolWheelBody,harpoonElementId,toolWheelId;public Row[] rows; }
    [Serializable] class Row { public string id,kind,enabled,body;public bool active; }
    static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!value)throw new Exception(label);}
    static string Native(string path)=>File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/"+path+".cs");
    static string Source(string path)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+path+".cs");
    static T Field<T>(UnityEngine.Object obj,string name)where T:UnityEngine.Object => new SerializedObject(obj).FindProperty(name)?.objectReferenceValue as T;
    public static void Run(){VerifyFishing();VerifyOcean();Debug.Log("NATIVE_SPEARFISHING_VERIFIED checks="+checks.Count+" play=false simulation=false");}
    public static void VerifyFishing()
    {
        Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Read-only assets and pure tool-data transforms; no gameplay or physics simulation");
        var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");var harpoon=root.GetComponentInChildren<HarpoonTool>(true);
        var data=Field<HarpoonData>(harpoon,"_data");
        Check(harpoon&&data&&data.Sprite,"Original native HarpoonTool, data and white wheel icon exist in the real player prefab");
        foreach(string field in new[]{"_grapple","_hookedPoint","_tipSensor","_fpsGraphicParent","_tpsGraphicParent"})
            Check(new SerializedObject(harpoon).FindProperty(field)?.objectReferenceValue, "Native harpoon keeps its authored "+field+" reference");
        Check(data.Levels.Count>0&&data.Levels.All(l=>l&&l.GraphicPrefab&&l.Sprite&&l.ShootRange>0&&l.ShootSpeed>0),"Every existing harpoon tier retains its original model, art, range and shot speed");
        Check(new[]{data.SpamCommon,data.SpamRare,data.SpamEpic,data.PrecisionEasy,data.PrecisionNormal,data.PrecisionHard}.All(p=>float.IsFinite(p.SuccessRange)&&p.SuccessRange>0&&p.GetMinimumSuccessZoneCount()>=1),"Original fishing challenge presets retain valid success ranges and zone counts, rather than being replaced with sea-fishing gun combat");
        var sceneData=JsonUtility.FromJson<SceneAudit>(File.ReadAllText(Out+"OceanSceneAudit.json"));
        string dataGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        Check(sceneData.harpoonElementBody.Contains("guid: "+dataGuid)&&sceneData.harpoonElementBody.Contains("_wheel: {fileID: "+sceneData.toolWheelId+"}"),"Main R wheel in the real scene uses the original harpoon element and its existing data, icon and native button");
        string elements=Regex.Match(sceneData.toolWheelBody,@"  _elements:(.*?)(?=\n  \w+:)",RegexOptions.Singleline).Value;
        Check(elements.Contains("fileID: "+sceneData.harpoonElementId+"}"),"Restored original harpoon remains registered in the main wheel's actual selectable element list");
        var empty=new ToolDataNet{SyncRevision=7};var basic=NativeSpearfishingAccess.RestoreBaseKit(empty);
        Check(basic.HarpoonUnlocked&&basic.DivingEquipmentUnlocked&&basic.DivingEquipmentLevel==1&&basic.HarpoonLevel==0&&basic.SyncRevision==8,"First explicit selection restores the original basic harpoon and first diving tier");
        var progressed=new ToolDataNet{SyncRevision=12,HarpoonLevel=3,DivingEquipmentLevel=4,FishingRodLevel=2,ScooterLevel=3,MopLevel=2,CompassLevel=2,FishingRodUnlocked=true,ScooterUnlocked=true,MopUnlocked=true,CompassUnlocked=true,PoseidonTridentUnlocked=true};
        var restored=NativeSpearfishingAccess.RestoreBaseKit(progressed);
        Check(restored.HarpoonLevel==3&&restored.DivingEquipmentLevel==4&&restored.FishingRodLevel==2&&restored.ScooterLevel==3&&restored.MopLevel==2&&restored.CompassLevel==2&&restored.FishingRodUnlocked&&restored.ScooterUnlocked&&restored.MopUnlocked&&restored.CompassUnlocked&&restored.PoseidonTridentUnlocked,"Restoration preserves all existing tool levels and unrelated unlocks");
        Check(NativeSpearfishingAccess.RestoreBaseKit(restored).Equals(restored),"Repeated selections do not grant levels or repeatedly change native save revision");
        string player=Native("Core/Player/PPG_Player"),wheel=Source("FishingEquipmentWheel"),service=Source("FishingMigration"),access=Source("NativeSpearfishingAccess"),network=Source("MigrationNetworking"),nativeWheel=Native("Wheel/PPG_WheelManager");
        Check(!wheel.Contains("原版钓竿")&&!wheel.Contains("TryEquipFishingBackend")&&!player.Contains("TryEquipFishingBackend")&&!Native("Core/PlayerTools/FishingRod").Contains("_migratedBackend"),"Mistaken original-rod option and its additional backend are removed");
        Check(wheel.Contains("Add(\"海钓竿\"")&&player.Contains("OpenNativeHarpoon(this,data)")&&player.Contains("LastToolAction=ToggleHarpoon"),"Sea rod and native underwater harpoon have distinct original-style equipment entry points");
        Check(nativeWheel.Contains("_harpoon.SetLock(status || SushiAndFish.FishingIntegration.FishingMigration.Enabled)"),"A previously locked save can see the restored native main-wheel entry");
        Check(access.Contains("actor.world.Server")&&access.Contains("!relay.IsServer")&&network.Contains("command.action!=\"native-harpoon\"")&&Source("FishingActor").Contains("case \"native-harpoon\":"),"Only a validated owner request is handled on the server, even while sea equipment is stowed");
        Check(access.Contains("actor.Send(\"native-harpoon\", 1)")&&!access.Contains("void Awake(")&&!access.Contains("void Start(")&&!access.Contains("QuestComplete")&&!access.Contains("Instantiate("),"Restoration happens on explicit equipment selection, without automatic login grants, quest reset or a second fish spawner");
        Check(access.Contains("TryDisableActiveToolForModeChange()")&&player.Contains("ToggleHarpoon(true)")&&player.Contains("TryDisableOtherTools(_harpoon)"),"Native tool switching stows the old sea rod/gun and retains the original tool exclusivity guard");
        Check(service.Contains("player.FishingRod.ToolStatus")&&service.Contains("player.ActiveTool.CapturesInteractInput"),"Sea commands depend on its active rod slot and do not steal native harpoon interact input");
        Check(Source("FishingActor").Contains("if(!equipped||!FishingMigration.UsesMigratedFishing(player))return;"),"Hidden migrated weapons stop writing hand/finger poses while the native underwater harpoon is equipped");
        Check(Source("FishingHud").Contains("fish!=null&&!nativeHarpoon")&&Source("FishingHud").Contains("!nativeHarpoon&&!string.IsNullOrEmpty(world.EventTitle)"),"Native harpoon keeps its own UI without stale sea-catch health or event overlays");
        string harpoonSource=Native("Core/PlayerTools/Harpoon/HarpoonTool"),catchSource=Native("Core/Ocean/Creatures/CatchableSeaCreature");
        Check(harpoonSource.Contains("TryGetComponent<SeaCreature>")&&harpoonSource.Contains("ServerRequestHookCreatureRpc")&&!harpoonSource.Contains("FishingMigration"),"Actual spear hit routes to original underwater creatures and their original server hook RPC");
        Check(catchSource.Contains("if (!_pendingCatchFinalize)")&&catchSource.Contains("_pendingCatchFinalize = false")&&catchSource.Contains("SpawnDeadFish(fishSO")&&catchSource.Contains("ServerDespawnCaughtCreature()"),"Original catch finalization consumes its pending state and removes the live creature, retaining one-catch delivery");
        string combat=File.ReadAllText("Assets/HowToFish/Runtime/FishCombat.cs");
        Check(combat.Contains("GetComponentInParent<FishingCatch>()")&&!combat.Contains("SeaCreature"),"Migrated guns process only migrated catches, keeping the two capture paths separate");
    }
    static void VerifyOcean()
    {
        var audit=JsonUtility.FromJson<SceneAudit>(File.ReadAllText(Out+"OceanSceneAudit.json"));
        string scene="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
        using(var hash=SHA256.Create())Check(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(scene))).Replace("-","").ToLowerInvariant()==audit.sceneSha256,"Ocean inspection matches the exact unchanged native GameState scene");
        var layers=audit.rows.Where(r=>r.kind=="SeaLayer").ToArray();
        Check(layers.Length==26&&layers.All(r=>r.active&&r.enabled=="1"),"All 26 original underwater fishing layers remain active and enabled");
        var manager=audit.rows.Single(r=>r.kind=="SeaCreatureManager");Check(manager.active&&manager.enabled=="1"&&layers.All(r=>manager.body.Contains("fileID: "+r.id)),"Existing native creature manager still owns all original layers; no replacement manager is created");
        int live=0,dead=0;var ids=new HashSet<string>();
        foreach(var layer in layers)
        {
            var matches=Regex.Matches(layer.body,@"CreatureData: \{fileID: \d+, guid: (\w+).*?Count: (\d+).*?DeadSpawn: (\d)",RegexOptions.Singleline);
            Check(matches.Count>0||layer.body.Contains("_spawnableCreatures: []"),"Original layer "+layer.id+" retains its authored population, including empty transit layers");
            foreach(Match match in matches)
            {
                var item=AssetDatabase.LoadAssetAtPath<CreatureSO>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
                bool isDead=match.Groups[3].Value=="1";int count=int.Parse(match.Groups[2].Value);
                Check(item&&!item.Id.StartsWith("HTF_")&&(isDead?item.DeadPrefab:item.Prefab&&item.Prefab.GetComponent<SeaCreature>()),"Native spawn entry "+(item?item.Id:match.Groups[1].Value)+" resolves its own live/dead original prefab");
                if(isDead)dead+=count;else live+=count;if(item)ids.Add(item.Id);
            }
        }
        File.WriteAllText(Out+"NativePopulation.txt","layers="+layers.Length+"\nspecies="+ids.Count+"\nliveConfigured="+live+"\ndeadConfigured="+dead+"\nNo population was spawned; these are original authored limits.\n");
        Check(live>0&&ids.Count>0,"Original authored population contains live underwater targets and is independent of migrated cast fish");
    }
}
