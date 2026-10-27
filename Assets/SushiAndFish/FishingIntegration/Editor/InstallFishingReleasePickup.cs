using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using PaperPixelGames.Core.Player;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// In-memory save DTOs, analytic geometry and static art only. No Play, game
// lifecycle, inputs, native pickup calls, physics stepping or real save writes.
public static class InstallFishingReleasePickup {
 const string Out="D:/UnityProjects/_CodexWork/FishingReleaseAndPickup-20261026/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool v,string text){checks.Add((v?"PASS ":"FAIL ")+text);if(!v){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(text);}}
 static string Read(string path)=>File.ReadAllText("Assets/"+path);
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"No Play, inputs, game lifecycle or physics stepping");
  var gravity=Physics.gravity;var config=MigrationConfig.Load();var actor=Read("SushiAndFish/FishingIntegration/Runtime/FishingActor.cs");var module=Read("HowToFish/Runtime/FishingModule.cs");var line=Read("HowToFish/Runtime/FishingModuleBoundedLine.cs");var landing=Read("HowToFish/Runtime/FishingModuleLanding.cs");
  Check(actor.Contains("module.RequireManualCatchRelease=true"),"Island integration enables manual right-click release");
  Check(line.Contains("if(!RequireManualCatchRelease&&reeling>.01f&&haulRaised"),"Reeling alone cannot start automatic dry landing in manual mode");
  Check(landing.Contains("if(RequireManualCatchRelease){rig.Notify")&&landing.Contains("先按右键"),"Changing to a gun cannot silently detach a fish before right-click");
  Check(actor.Contains("if(module.Hooked)module.ReleaseCatch(module.Hooked);else module.BeginCast()")&&module.Contains("TryBeginDryLanding(fish)"),"Explicit right-click releases to the existing visible dry-shore delivery");
  Check(line.Contains("FishingHaulMath.HeldTarget")&&line.Contains("RequireManualCatchRelease&&catchReeledIn")&&module.Contains("haulRaised=catchReeledIn=false"),"Reeled-in catch follows its short line until release; each new catch resets the hold latch");
  var tip=new Vector3(.2f,-1.5f,1.8f);var target=FishingHaulMath.HeldTarget(tip,-3.8f,.4f,.12f);
  Check(Mathf.Abs(Vector3.Distance(tip,target)-.12f)<.0001f,"Normal fish stops on a 12 cm line instead of a raised metre-long hold target");
  foreach(float height in new[]{.1f,.5f,1f,2.5f,5f}){var p=FishingHaulMath.HeldTarget(tip,-3.8f,height,.12f);Check(p.y-height>=-3.8001f&&p.x==tip.x&&p.z==tip.z,"Large-fish safety keeps its bottom above the shore without extending it outwards: "+height);}
  var hud=Read("SushiAndFish/FishingIntegration/Runtime/FishingHud.cs");Check(hud.Contains("右键放到岸上")&&!hud.Contains("自动放到岸上"),"Native HUD describes manual release rather than old automatic release");
  Check(Read("SushiAndFish/FishingIntegration/Runtime/FishingNativeHints.cs").Contains("放鱼到岸上 · 然后切枪"),"Native right mouse hint changes from casting to release while a fish is attached");
  CheckLedger();
  var harvest=Read("SushiAndFish/FishingIntegration/Runtime/MigrationHarvest.cs");
  Check(harvest.Contains("FishingCaptureLedger.Snapshot(SaveData,live)")&&!harvest.Contains("SaveData.pending=retained"),"Autosave serializes a separate object and never refills the runtime restore queue with live fish");
  Check(harvest.Contains("FishingCaptureLedger.Normalize(SaveData)")&&harvest.Contains("nativeTokens.Contains(saved.token)"),"Loading checks acquired, warehouse and existing native capture IDs before restoring an old corpse");
  int harvestStart=harvest.IndexOf("internal void Harvest");string conversion=harvest.Substring(harvestStart,harvest.IndexOf("DeadFish SpawnNative",harvestStart)-harvestStart);
  Check(conversion.Contains("convertingCatches.Add(token)")&&conversion.Contains("finally{convertingCatches.Remove(token);}"),"Reentrant/repeated pickup commands have a per-capture conversion guard with guaranteed cleanup");
  int spawn=conversion.IndexOf("var native=SpawnNative");int resolve=conversion.IndexOf("FishingCaptureLedger.Resolve",spawn);int hold=conversion.IndexOf("native.ServerLazyHold",spawn);
  Check(spawn<resolve&&resolve<hold&&conversion.Substring(spawn,resolve-spawn).Contains("if(!native)return"),"Failed spawn preserves the original; successful conversion resolves its ID before native hold callbacks");
  Check(conversion.Contains("finally{SaveWorld();}")&&conversion.Contains("fish.Stored=true;fish.item.Retire();catches.Remove(token)"),"Converted source is retired and committed even when native holding falls back");
  Check(Read("SushiAndFish/FishingIntegration/Runtime/MigrationConfig.cs").Contains("public int version=1"),"Additive capture ledger keeps version-1 save compatibility");
  RenderHands(config);
  Check(Physics.gravity==gravity&&config.seaHeight==-4,"World gravity and corrected native sea datum remain intact");
  InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_RELEASE_PICKUP_PASS checks="+checks.Count+" play=false simulation=false");
 }
 static SavedCatch Catch(string token,int health=0)=>new SavedCatch{token=token,species=89,health=health,weight=1,position=new Vector3(2,-3,3),rotation=Quaternion.identity};
 static void CheckLedger(){
  var save=new FishingWorldSave();var queued=Catch("unloaded",123);var live=Catch("live");save.pending.Add(queued);var queue=save.pending;
  var snapshot=FishingCaptureLedger.Snapshot(save,new[]{live});
  Check(ReferenceEquals(save.pending,queue)&&save.pending.Count==1&&snapshot.pending.Count==2&&!ReferenceEquals(snapshot.pending,queue),"Saving live fish leaves the original restore queue untouched");
  save.pending.Clear();snapshot=FishingCaptureLedger.Snapshot(save,new[]{live});
  Check(save.pending.Count==0&&snapshot.pending.Single().token=="live","Previously restored fish is saved without being queued for runtime resurrection");
  FishingCaptureLedger.Resolve(save,"live");snapshot=FishingCaptureLedger.Snapshot(save,new[]{live});
  Check(snapshot.pending.Count==0&&save.pending.Count==0&&save.resolved.Single()=="live","Pickup excludes both stale/live copies of that ID from future snapshots");
  for(int i=0;i<100;i++)FishingCaptureLedger.Resolve(save,"live");
  Check(save.resolved.Count==1&&save.acquired.Count==0&&save.stock.Count==0,"Repeated conversion resolution stays idempotent without awarding stock or book progress");
  var round=JsonUtility.FromJson<FishingWorldSave>(JsonUtility.ToJson(snapshot));FishingCaptureLedger.Normalize(round);
  Check(FishingCaptureLedger.IsResolved(round,"live")&&round.pending.Count==0,"JSON save/reload retains consumed identity without restoring a corpse");
  var old=new FishingWorldSave{resolved=null,pending=new List<SavedCatch>{Catch("picked"),Catch("stored"),Catch("unknown",55),Catch("unknown",55)},acquired=new List<string>{"picked"},stock=new List<StoredFish>{new StoredFish{token="stored",itemId="HTF_N089",weight=2}}};
  FishingCaptureLedger.Normalize(old);
  Check(old.pending.Count==1&&old.pending[0].token=="unknown"&&old.pending[0].health==55,"Legacy acquired/stored corpses are filtered; unique unacquired catch and health survive");
  Check(old.stock.Single().weight==2&&old.acquired.Single()=="picked","Legacy cleanup preserves warehouse and acquisition records");
  FishingCaptureLedger.Resolve(old,"native-object-already-exists");old.pending.Add(Catch("native-object-already-exists"));FishingCaptureLedger.Normalize(old);
  Check(!old.pending.Any(p=>p.token=="native-object-already-exists"),"An already-existing native fish cannot return from a stale migration snapshot");
  var fresh=new FishingWorldSave();var failed=Catch("not-converted",77);fresh.pending.Add(failed);snapshot=FishingCaptureLedger.Snapshot(fresh,new[]{failed});
  Check(snapshot.pending.Count==1&&!FishingCaptureLedger.IsResolved(fresh,failed.token)&&snapshot.pending[0].health==77,"A non-converted source is preserved once, including health");
  fresh.pending.Clear();snapshot=FishingCaptureLedger.Snapshot(fresh,Array.Empty<SavedCatch>());Check(snapshot.pending.Count==0,"Removed/escaped live catches cannot reappear from an autosave-generated runtime queue");
  var legacy=JsonUtility.FromJson<FishingWorldSave>("{\"version\":1,\"pending\":[],\"stock\":[],\"acquired\":[]}");FishingCaptureLedger.Normalize(legacy);Check(legacy.resolved!=null&&legacy.pending.Count==0,"Old saves with no resolved field initialize safely");
 }
 static void RenderHands(MigrationConfig config){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab").GetComponent<PPG_Player>();var data=new SerializedObject(prefab);var original=(Camera)data.FindProperty("_viewModelCamera").objectReferenceValue;
  var camera=new GameObject("Static native pose camera",typeof(Camera)).GetComponent<Camera>();camera.fieldOfView=original.fieldOfView;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.44f,.5f);camera.aspect=16f/9;
  var sun=new GameObject("Static model light",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(35,-30,0);sun.cullingMask=1<<29;RenderSettings.ambientLight=Color.gray;
  var m=config.systemPrefab.GetComponent<FishingModule>();var tools=new[]{m.rod.gameObject}.Concat(m.combat.guns.Select(g=>g.gameObject)).ToArray();
  for(int i=0;i<tools.Length;i++){
   var frame=Object.Instantiate(original.gameObject);frame.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);foreach(var c in frame.GetComponentsInChildren<Camera>(true))c.enabled=false;foreach(var s in frame.GetComponentsInChildren<MonoBehaviour>(true))s.enabled=false;
   var body=frame.GetComponentsInChildren<Animator>(true).First(a=>a.name=="BodyRig");AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/AnimationClip/Rod_Idle.anim").SampleAnimation(body.gameObject,0);
   var hand=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="hand_r");var palm=FishingGripPose.Palm(hand,FishingGripPose.NativeFingers(hand));
   var tool=Object.Instantiate(tools[i]);foreach(var s in tool.GetComponentsInChildren<MonoBehaviour>(true))s.enabled=false;var weapon=tool.GetComponent<FishWeapon>();if(weapon&&weapon.animationPlayer&&weapon.animationPlayer["Idle"]!=null)weapon.animationPlayer["Idle"].clip.SampleAnimation(weapon.animationPlayer.gameObject,0);
   var fingers=FishingGripPose.AuthoredFingers(tool.transform);var grip=FishingGripPose.AuthoredGrip(tool.transform,fingers);tool.transform.SetPositionAndRotation(FishingGripPose.RootPosition(palm,Quaternion.identity,grip,tool.transform.lossyScale),Quaternion.identity);
   Check(Vector3.Distance(tool.transform.TransformPoint(grip),palm)<.0001f,tools[i].name+": actual grip centre meets native palm within 0.1 mm");
   if(i==0){Check(fingers.Length==1&&fingers[0].name=="RodBase"&&grip.x>.07f&&grip.x<.08f&&grip.z==0,"Rod uses the actual handle axis rather than the empty hand-bone zero fallback");}
   else {var originalGrip=Vector3.zero;foreach(var f in fingers)originalGrip+=f.position;originalGrip=tool.transform.InverseTransformPoint(originalGrip/fingers.Length);Check(fingers.Length==3&&Vector3.Distance(originalGrip,grip)<.00001f,tools[i].name+": gun hand-bone grip remains identical to the accepted implementation");}
   foreach(var r in frame.GetComponentsInChildren<Renderer>(true))r.enabled=r is SkinnedMeshRenderer&&r.name=="ProxyMesh";foreach(var t in frame.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;foreach(var t in tool.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
   var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Check(image.GetPixels32().Count(p=>p.r>210&&p.b>210&&p.g<65)<10,tools[i].name+": static artwork has no missing-shader magenta");File.WriteAllBytes(Out+(i==0?"rod-hand.png":"gun-"+(i-1)+"-hand.png"),image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
   Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(tool);Object.DestroyImmediate(frame);
  }
  Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
 }
}
