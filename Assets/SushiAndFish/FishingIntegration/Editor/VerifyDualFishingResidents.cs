using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Ai.NPCs;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Data inspection and static visual-reference regression. No Play, spawned
// network objects, AI/player initialization, input or fishing/physics stepping.
public static class VerifyDualFishingResidents {
 const string Out="D:/UnityProjects/_CodexWork/DualFishingResidents-20261028/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!value)throw new Exception(label);}
 static string Native(string path)=>File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/"+path+".cs");
 static string Integration(string file)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+file+".cs");
 static bool Active(Transform t){for(;t;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
 public static void Run(){
  Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Verification never enters Play or initializes a game/player/network/AI session");
  var config=MigrationConfig.Load();var original=config.database._allItems.OfType<FishSO>().Where(f=>!f.Id.StartsWith("HTF_")&&f.FishingRodSettings!=null&&f.FishingRodSettings.Enabled&&f.DeadPrefab).ToArray();
  Check(original.Length>0,"Original fishing keeps its existing enabled fish definitions and dead-fish prefabs");
  Check(config.species.All(b=>!b.original.FishingRodSettings.Enabled&&(!b.shiny||!b.shiny.FishingRodSettings.Enabled)),"All imported fish still belong exclusively to the migrated fishing pool");
  string rod=Native("Core/PlayerTools/FishingRod"),player=Native("Core/Player/PPG_Player"),migration=Integration("FishingMigration"),actor=Integration("FishingActor"),wheel=Integration("FishingEquipmentWheel"),network=Integration("MigrationNetworking");
  Check(rod.Contains("NetworkVariable<bool> _migratedBackend")&&rod.Contains("NetworkVariableWritePermission.Owner")&&rod.Contains("_migratedBackend.Value"),"Each player's replicated mode has owner-only writes; there is no global native-versus-migrated toggle");
  Check(rod.Split(new[]{"NetworkVariableFields.Add(_migratedBackend)"},StringSplitOptions.None).Length==2&&rod.Contains("_migratedBackend.Initialize(this)")&&rod.Contains("__nameNetworkVariable(_migratedBackend"),"New backend field is registered exactly once in the recovered Netcode variable initializer");
  Check(rod.Contains("_migratedBackend.OnValueChanged += OnBackendChanged")&&rod.Contains("_migratedBackend.OnValueChanged -= OnBackendChanged"),"Mode-change lifecycle is registered and cleaned up");
  Check(rod.Contains("if(UsesMigratedFishing)return;")&&rod.Contains("if(UsesMigratedFishing){")&&rod.Contains("if (UsesMigratedFishing) return;"),"Only the selected implementation runs rod update, visuals and interact input");
  Check(migration.Contains("if(!UsesMigratedFishing(player)||!player.IsOwner)return false"),"Native mouse input passes through without also commanding the migrated module");
  Check(rod.Contains("if (!UsesMigratedFishing && id != 0)")&&rod.Contains("!base.IsServer || UsesMigratedFishing || rpcParams.Receive.SenderClientId"),"Server refuses native cast acceptance/rewards while the migrated backend is selected");
  Check(actor.Contains("!FishingMigration.UsesMigratedFishing(player)||!player.ToolsData.FishingRodUnlocked")&&actor.Contains("!inputActive||!FishingMigration.UsesMigratedFishing(player)"),"Server refuses migrated casts/shots/equipment commands while the native backend is selected");
  Check(rod.Contains("ServerReleaseUndeliveredReward(); ServerAbortFishing();")&&rod.Contains("CancelFishingSession(FishingCancelReason.ToolDisabled")&&rod.Contains("FishingMigration.ToolStatus(_player, false)"),"Switching releases pending native delivery and cancels the old session and migrated line before reapplying tool status");
  Check(player.Contains("_fishingRod.CancelForBackendChange()")&&player.Contains("!TryDisableActiveToolForModeChange()||!_fishingRod.TrySelectBackend(migrated)"),"Mode selection first cancels/stows the old tool through the native control guards");
  Check(wheel.Contains("Add(\"原版钓竿\",actor.player.FishingRod.Icon,-2)")&&wheel.Contains("Add(\"海钓竿\",c.rodIcon,-1)")&&wheel.Contains("TryEquipFishingBackend(false)"),"Existing native-style wheel exposes separate original and sea-fishing rods with their existing artwork");
  Check(network.Contains("view.equipped=actor.equipped&&FishingMigration.UsesMigratedFishing(view.player)"),"Delayed snapshots cannot show migrated weapons over the selected native rod");
  Check(rod.Contains("!fish.Id.StartsWith(\"HTF_\", StringComparison.Ordinal)")&&rod.Contains("_serverRewardAllocated")&&rod.Contains("FishingRodChallenge.IsNewerSession"),"Native species identity and existing session/reward idempotence remain in place");
  VerifyResidents();
  Debug.Log("DUAL_FISHING_RESIDENTS_VERIFIED checks="+checks.Count+" nativeFish="+original.Length+" play=false simulation=false");
 }
 static void VerifyResidents(){
  var data=AssetDatabase.FindAssets("t:ResidentDataSO",new[]{"Assets/MonoBehaviour"}).Select(g=>AssetDatabase.LoadAssetAtPath<ResidentDataSO>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
  foreach(var resident in data){
   var visual=resident.BakedVisualPrefab;
   Check(visual&&visual.GetComponentInChildren<Animator>(true)&&visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.enabled&&Active(r.transform)&&r.sharedMesh&&r.sharedMesh.vertexCount>0&&r.sharedMaterials.All(m=>m&&m.shader)),resident.NpcID+" "+resident.Name+": original active baked model, mesh and materials are available");
  }
  var gabe=data.Single(d=>d.NpcID==6);var schedule=new ResidentScheduleController(gabe);
  Check(schedule.TryFindActiveEntry(11,25,out _,out var work)&&work.Activity==ResidentActivityType.Work,"Screenshot time 11:25 is Gabe's original work schedule, not his home/hidden hours");
  Check(schedule.TryFindActiveEntry(5,9,out _,out var home)&&home.Activity==ResidentActivityType.Home,"Early-morning home visibility remains intentionally hidden");
  var old=new GameObject("Old NPC visual");var replacement=new GameObject("Replacement NPC visual");var third=new GameObject("Held replacement visual");
  try{
   var brain=new ResidentBrain(null,null,old,null,null);brain.SetModelVisible(false);replacement.SetActive(false);brain.RebindModel(replacement);brain.SetModelVisible(true);
   Check(replacement.activeSelf&&!old.activeSelf,"Regression: after replacing an initially hidden baked model, visibility changes target the new model rather than the destroyed old reference");
   brain.BeginModelVisibilityHold();brain.SetModelVisible(false);third.SetActive(false);brain.RebindModel(third);
   Check(third.activeSelf,"A replacement stays temporarily active during the existing customization visibility hold");
   brain.ReleaseModelVisibilityHold();Check(!third.activeSelf,"Releasing customization restores the intended hidden/home state on the replacement");
   brain.SetModelVisible(true);Check(third.activeSelf,"The same replacement can become visible for work without reinitializing the NPC");
  }finally{Object.DestroyImmediate(old);Object.DestroyImmediate(replacement);Object.DestroyImmediate(third);}
  string controller=Native("Core/Ai/NPCs/ResidentController"),manager=Native("Core/Ai/NPCs/ResidentManager");
  Check(controller.Contains("_brain?.RebindModel(gameObject)"),"Native baked-visual replacement explicitly updates its existing brain's model reference");
  Check(manager.Contains("_isInitDone && IsLiveNetworkAuthority()")&&manager.Contains("ReconcileHouseResidents();")&&manager.Contains("expected.Contains(resident.ResidentData.NpcID)"),"Only the authoritative host reconciles configured house residents; other staff and clients do not create duplicates");
  Check(manager.Contains("_residents.Find(r => r != null && r.ResidentData != null && r.ResidentData.NpcID == entry.ResidentData.NpcID)")&&manager.Contains("retained.Spawn(destroyWithScene: true)"),"Reconciliation reuses each existing stable resident ID, respawns a retained network object, or creates one missing resident");
  RenderGabe(gabe.BakedVisualPrefab);
 }
 static void RenderGabe(GameObject prefab){
  var model=Object.Instantiate(prefab);foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
  var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
  var camera=new GameObject("Static original Gabe preview camera").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.3f,.34f);camera.orthographic=true;camera.orthographicSize=1.2f;camera.transform.position=bounds.center+new Vector3(.25f,.1f,3);camera.transform.LookAt(bounds.center);
  var light=new GameObject("Static character preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,150,0);
  var target=new RenderTexture(800,1000,24);camera.targetTexture=target;
  try{camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(800,1000,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,800,1000),0,0);image.Apply();File.WriteAllBytes(Out+"OriginalGabe.png",image.EncodeToPNG());RenderTexture.active=previous;Object.DestroyImmediate(image);}
  finally{camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(model);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);}
 }
}
