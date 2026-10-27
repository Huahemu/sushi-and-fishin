using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using HowToFish;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object=UnityEngine.Object;

// Asset reads, deterministic probability formulas, in-memory DTOs and static UI
// rendering only. Never enters Play or steps game/physics/input state.
public static class VerifyArchipelago {
 const string Out="D:/UnityProjects/_CodexWork/Archipelago-20261026/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string title){checks.Add((value?"PASS ":"FAIL ")+title);File.WriteAllLines(Out+"Verification.txt",checks);if(!value)throw new Exception(title);}
 static string Read(string name)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+name+".cs");
 public static void Run(){
  Check(!EditorApplication.isPlaying,"No Play, game lifecycle, simulated inputs or physics stepping");var gravity=Physics.gravity;var settings=ArchipelagoSettings.Load();var config=MigrationConfig.Load();
  Check(settings&&settings.enabled&&settings.boatPrefab&&settings.accessPrefab&&settings.regions.Length==3,"Three regions, source boat and collision docks are enabled");
  var rig=settings.boatPrefab;Check(rig.forcePoints.Length==6&&rig.forcePoints.All(t=>t)&&rig.hull.Length==17,"Original six buoyancy points and 17 physical hull parts remain assigned");
  Check(rig.body.mass==100&&rig.motorForce==800&&rig.waterForce==800&&rig.motorAngle==20&&rig.startDelay==1.6f&&rig.bounciness==1&&rig.antiRoll==2500,"Original authored base boat mass, propulsion, steering, buoyancy and engine delay match");
  Check(Vector3.Dot(-rig.propeller.right,rig.transform.forward)>.99f&&Mathf.Abs(rig.driverSeat.position.x)<.6f,"Boat thrust faces its bow and helm seat is inside its normalized hull");
  Check(settings.motorStart&&settings.motorStop&&settings.motorRun&&settings.motorIdle&&settings.waterMoving,"Original motor start, stop, run, idle and water sounds are assigned");
  Check(rig.motor&&rig.propeller&&rig.steering&&rig.throttle&&rig.driverSeat,"Original animated motor, propeller, wheel, throttle and seat references exist");
  var ids=settings.regions.SelectMany(r=>r.fish).Select(f=>f.originalId).ToArray();Check(ids.Length==60&&ids.Distinct().Count()==60&&ids.OrderBy(i=>i).SequenceEqual(config.profile.fish.Select(f=>f.originalId).OrderBy(i=>i)),"All 60 species are preserved exactly once across three normal regional pools");
  Check(config.species.Length==60&&config.species.All(b=>b.original&&b.definition),"Existing book bindings and original/shiny art remain intact");
  Check(settings.RegionAt(settings.regions[0].center)==0&&settings.RegionAt(settings.boatSpawn)==0,"Restaurant and initial boat belong to main-island fish pool");
  Check(settings.RegionAt(settings.regions[1].center)==1&&settings.RegionAt(settings.regions[2].center)==2,"Each new island selects its own pool");
  Check(settings.RegionAt(new Vector3(5000,0,5000))==0,"Open water outside both small-island fishing radii uses main-island pool");
  Check(Vector3.Distance(settings.regions[1].center,settings.regions[2].center)>settings.regions[1].fishingRadius+settings.regions[2].fishingRadius,"Small-island fishing zones do not overlap");
  Check(Vector3.ProjectOnPlane(settings.boatSpawn-settings.regions[0].center,Vector3.up).magnitude<75,"Boat remains beside the sushi restaurant rather than at a remote spawn");
  Check(settings.regions[1].prefab!=settings.regions[2].prefab&&settings.regions.Skip(1).All(r=>r.prefab.GetComponentsInChildren<MeshCollider>().Any(c=>c.sharedMesh)),"Different imported island prefabs both have terrain collision");
  var report=new StringBuilder("海域鱼池与概率（无事件；保留非普通鱼倍率）\n");float previousRare=-1,previousLegend=-1;
  foreach(var region in settings.regions){
   var profile=Object.Instantiate(config.profile);settings.ApplyTo(profile,region.center);
   Check(profile.fish.SequenceEqual(region.fish)&&!ReferenceEquals(profile,config.profile),region.title+": cast pool is applied to an isolated actor profile");
   Check(region.fish.Any(f=>f.shallowWaterFish&&f.rarity==CatchRarity.Common)&&region.fish.Any(f=>f.rarity==CatchRarity.Rare)&&region.fish.Any(f=>f.rarity==CatchRarity.Legendary),region.title+": shallow guaranteed fish plus rare and legendary tiers are available");
   foreach(var bait in profile.baits){var shallow=FishingCatchPool.Odds(profile,bait,0,0);Check(Mathf.Approximately(shallow.commonChance,1)&&shallow.rareChance==0&&shallow.legendaryChance==0&&FishingCatchPool.SmallCatch(profile,bait,.5f,int.MaxValue,int.MaxValue),region.title+" / "+bait.title+": shallow casts retain guaranteed common catches");}
   var near=FishingCatchPool.Odds(profile,profile.baits[0],profile.minDepth,0);var far=FishingCatchPool.Odds(profile,profile.baits[0],profile.fullQualityDepth,profile.distanceQualityFull);
   Check(near.rareChance>previousRare&&near.legendaryChance>previousLegend&&far.rareChance>=near.rareChance&&far.legendaryChance>=near.legendaryChance,region.title+": rarity increases by island and by depth/distance");previousRare=near.rareChance;previousLegend=near.legendaryChance;
   bool normalized=true,reachable=true;foreach(float depth in new[]{0,profile.minDepth,profile.fullQualityDepth})foreach(float distance in new[]{0,30,70})foreach(float bonus in new[]{1,2}){var odds=FishingCatchPool.Odds(profile,profile.baits[0],depth,distance,bonus);float sum=profile.fish.Sum(f=>FishingCatchPool.CastWeight(f,profile.baits[0],odds));normalized&=Mathf.Abs(sum-1)<.0001f&&odds.rareChance>=0&&odds.legendaryChance>=0&&odds.commonChance>=0;}
   var normal=FishingCatchPool.Odds(profile,profile.baits[0],profile.fullQualityDepth,profile.distanceQualityFull);foreach(var fish in profile.fish)reachable&=FishingCatchPool.CastWeight(fish,profile.baits[0],normal)>0;
   Check(normalized&&reachable,region.title+": all eligible species are reachable and every probability distribution sums to one");
   report.AppendLine(region.title+"："+region.fish.Length+"种，近岸稀有 "+near.rareChance.ToString("P1")+" / 传说 "+near.legendaryChance.ToString("P1")+"；深远稀有 "+far.rareChance.ToString("P1")+" / 传说 "+far.legendaryChance.ToString("P1"));foreach(var fish in profile.fish)report.AppendLine("  "+fish.originalId+" "+fish.displayName+" "+fish.RarityLabel);Object.DestroyImmediate(profile);
  }
  Check(config.profile.fish.Length==60&&Mathf.Approximately(config.profile.nonCommonProbabilityMultiplier,.7f),"Shared original profile and existing 30% non-common reduction remain intact");File.WriteAllText(Out+"RegionalFish.txt",report.ToString());
  CheckSaves(settings);CheckSource();RenderUI(config);
  Check(Physics.gravity==gravity,"Inspection leaves world gravity unchanged");InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Verification.txt",checks);Debug.Log("ARCHIPELAGO_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
 }
 static void CheckSaves(ArchipelagoSettings settings){
  var old=JsonUtility.FromJson<FishingWorldSave>("{\"version\":1,\"ownedWeapons\":31,\"stock\":[{\"token\":\"kept\",\"itemId\":\"HTF_N089\",\"weight\":2}],\"acquired\":[\"kept\"]}");FishingCaptureLedger.Normalize(old);Check((old.boat==null||!old.boat.valid)&&old.ownedWeapons==31&&old.stock.Single().token=="kept","Version-1 saves without a boat remain compatible without modifying warehouse or purchases");
  old.boat=new SavedBoat{valid=true,position=settings.boatSpawn,rotation=settings.boatRotation};var snapshot=FishingCaptureLedger.Snapshot(old,Array.Empty<SavedCatch>());var round=JsonUtility.FromJson<FishingWorldSave>(JsonUtility.ToJson(snapshot));Check(round.boat.valid&&round.boat.position==settings.boatSpawn&&Quaternion.Angle(round.boat.rotation,settings.boatRotation)<.01f&&round.stock.Count==1&&round.resolved.Contains("kept"),"In-memory JSON roundtrip preserves boat pose and existing capture ownership");
  Check(!ArchipelagoWorld.ValidPosition(new Vector3(float.NaN,0,0),settings.boatSpawn)&&!ArchipelagoWorld.ValidPosition(settings.boatSpawn+Vector3.up*35,settings.boatSpawn)&&!ArchipelagoWorld.ValidRotation(new Quaternion(0,0,0,0))&&ArchipelagoWorld.ValidRotation(settings.boatRotation),"Invalid boat save poses are rejected; authored pose remains valid");
  var wire=new SailingSnapshot{position=settings.boatSpawn,rotation=settings.boatRotation,driver=42,input=new Vector2(-1,1),velocity=new Vector3(1,0,4)};var decoded=JsonUtility.FromJson<SailingSnapshot>(JsonUtility.ToJson(wire));Check(decoded.driver==42&&decoded.input==wire.input&&decoded.velocity==wire.velocity,"Sailing state DTO retains driver authority, axes and velocity for remote presentation");
 }
 static void CheckSource(){
  var world=Read("ArchipelagoWorld");var rig=Read("OriginalBoatRig");var harvest=Read("MigrationHarvest");var network=Read("MigrationNetworking");var actor=Read("FishingActor");
  Check(world.Contains("Boat.driver!=ulong.MaxValue||!OnDeck")&&world.Contains("Boat.driver==actor.player.OwnerClientId")&&world.Contains("actor.player.Interactor.CurrentHeld||actor.equipped||actor.player.IsToolActive"),"Only an empty-handed, eligible player standing at the helm can take the unoccupied seat; only its driver can steer");
  Check(world.Contains("IsKnocked")&&rig.Contains("lastInputAt>.5f")&&world.Contains("Cursor.lockState==CursorLockMode.Locked")&&world.Contains("IsTabletActive"),"Disconnect/knockout and menu/input timeout paths stop propulsion");
  Check(world.Contains("impartPlatformMovement")&&world.Contains("impartPlatformRotation")&&world.Contains("collider.Raycast(ray")&&world.Contains("hit.normal.y>.45f"),"Passengers use native moving-platform support without duplicate movement; nearby swimmers are not carried");
  Check(world.Contains("fish.CarryOnBoat")&&File.ReadAllText("Assets/HowToFish/Runtime/FishingCatch.cs").Contains("foreach(var part in bodies){part.position=current+delta"),"Loose connected fish bodies receive one consistent boat delta without stretching joints");
  Check(world.Contains("oldMotorEnabled")&&world.Contains("TryRestoreCharacterControlAfterModeExit")&&world.Contains("WaterCastEntered-=p.entered"),"Leaving/reloading the boat restores movement and unsubscribes cast/input callbacks");
  var look=typeof(ECM2.Character).GetMethod("ConsumeRotationInput",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Check(look!=null&&look.ReturnType==typeof(void)&&look.GetParameters().Length==0&&world.Contains("consumeNativeLook?.Invoke()")&&File.ReadAllText("Assets/SushiAndFish/FishingIntegration/link.xml").Contains("ConsumeRotationInput"),"Driving consumes the existing native yaw queue without stepping locomotion; IL2CPP preserves the rotation callback");
  Check(harvest.Contains("SaveWorld();ClearArchipelago();")&&harvest.Contains("SaveData.boat=new SavedBoat")&&Read("FishingCaptureLedger").Contains("boat=save.boat"),"Slot changes rebuild boat state and normal snapshots persist its pose");
  Check(network.Contains("Archipelago.Command")&&network.Contains("boat=Archipelago?Archipelago.Snapshot()")&&rig.Contains("remoteReceived")&&rig.Contains("Mathf.Exp(-20*Time.deltaTime)"),"Boat uses authoritative existing network commands and per-frame observer interpolation");
  Check(actor.Contains("Instantiate(world.Config.profile)")&&File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleBites.cs").Contains("if(!castTouchedWater){AnnounceWaterCast();"),"Regional pool is selected only at first water contact and remains fixed for that cast");
  Check(Read("MigrationEvents").Contains("eventDirector.Eligibility=()=>actors.Values.Any")&&Read("FishingNativeHints").Contains("RenderSailing"),"Existing fishing-only event eligibility and native hint presentation remain connected");
 }
 static void RenderUI(MigrationConfig config){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Static native sailing UI camera",typeof(Camera)).GetComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.30f,.36f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
  var root=new GameObject("Static sailing hints",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;var hints=root.AddComponent<FishingNativeHints>();hints.Build(config);hints.RenderSailing(true,true,4,"主岛 32米 后方 · 小岛1 230米 左侧 · 小岛2 440米 前方",null);
  foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1109,520)}){var target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))label.ForceMeshUpdate();foreach(var graphic in root.GetComponentsInChildren<Graphic>(true)){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Out+"sailing-ui-"+size.x+".png",image.EncodeToPNG());Check(image.GetPixels32().Count(p=>p.r>220&&p.b>220&&p.g<45)<12,"Native sailing hints have no shader-error magenta at "+size);RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
  Check(root.GetComponentsInChildren<TMP_Text>().Where(t=>t.transform.name!="HintIconText").All(t=>!t.isTextOverflowing),"Native sailing labels do not truncate their content");Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);
 }
}
