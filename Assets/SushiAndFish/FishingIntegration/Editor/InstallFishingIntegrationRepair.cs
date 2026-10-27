using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HowToFish;
using SushiAndFish.FishingIntegration;
using StylizedWater3;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Wheel;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Serialized assets, projection geometry and closed-form rules only.
// Never opens the gameplay scene, enters Play or invokes physics/game input.
public static class InstallFishingIntegrationRepair {
 const string Out="D:/UnityProjects/_CodexWork/FishingIntegrationRepair-20261026/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);if(!value){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(text);}}
 static string Read(string name)=>File.ReadAllText("Assets/"+name);
 public static void ConfigureAndVerify(){
  Check(!EditorApplication.isPlaying,"No Play, game lifecycle, fishing inputs or physics simulation");
  float authored=ReadSeaHeight();var config=MigrationConfig.Load();config.seaHeight=authored;EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
  Verify();
 }
 static float ReadSeaHeight(){
  var script=AssetDatabase.LoadAllAssetsAtPath("Assets/Plugins/sc.stylizedwater3.runtime.dll").OfType<MonoScript>().First(s=>s.GetClass()==typeof(OceanFollowBehaviour));
  AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script,out string guid,out long localId);
  var scene=Read("SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
  var docs=Regex.Matches(scene,@"(?ms)^--- !u!(\d+) &(\d+).*?(?=^--- |\z)").Cast<Match>().ToArray();
  string reference="m_Script: {fileID: "+localId+", guid: "+guid+", type: 3}";
  var ocean=docs.FirstOrDefault(d=>d.Value.Contains(reference));Check(ocean!=null,"Original island contains the actual OceanFollowBehaviour from the water runtime DLL");
  string gameObject=Regex.Match(ocean.Value,@"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
  var transforms=docs.Where(d=>d.Groups[1].Value=="4").ToDictionary(d=>d.Groups[2].Value,d=>d.Value);
  string oceanTransform=transforms.First(d=>Regex.IsMatch(d.Value,@"m_GameObject: \{fileID: "+gameObject+@"\}")).Key;
  Func<string,Matrix4x4> matrix=null;matrix=id=>{
   if(id=="0")return Matrix4x4.identity;string doc=transforms[id];
   Func<string,float[]> values=key=>Regex.Match(doc,key+@": \{([^}]+)\}").Groups[1].Value.Split(',').Select(v=>float.Parse(v.Split(':')[1],CultureInfo.InvariantCulture)).ToArray();
   var p=values("m_LocalPosition");var r=values("m_LocalRotation");var s=values("m_LocalScale");string parent=Regex.Match(doc,@"m_Father: \{fileID: (\d+)\}").Groups[1].Value;
   return matrix(parent)*Matrix4x4.TRS(new Vector3(p[0],p[1],p[2]),new Quaternion(r[0],r[1],r[2],r[3]),new Vector3(s[0],s[1],s[2]));
  };
  float height=matrix(oceanTransform).MultiplyPoint3x4(Vector3.zero).y;
  Check(float.IsFinite(height)&&Mathf.Abs(height+4)<.01f,"Serialized ocean transform confirms island water Y="+height.ToString(CultureInfo.InvariantCulture));
  File.WriteAllText(Out+"WaterDatum.txt","OceanFollowBehaviour script: "+guid+"/"+localId+"\nGameObject: "+gameObject+"\nTransform: "+oceanTransform+"\nWorld Y: "+height.ToString(CultureInfo.InvariantCulture));return height;
 }
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"Verification runs outside Play");var config=MigrationConfig.Load();
  Check(config.seaHeight==-4,"Missing-ocean fallback matches the original map, rather than zero");
  var service=Read("SushiAndFish/FishingIntegration/Runtime/FishingMigration.cs");var actor=Read("SushiAndFish/FishingIntegration/Runtime/FishingActor.cs");
  var wheel=Read("SushiAndFish/FishingIntegration/Runtime/FishingEquipmentWheel.cs");var hud=Read("SushiAndFish/FishingIntegration/Runtime/FishingHud.cs");
  var player=Read("Scripts/Assembly-CSharp/PaperPixelGames/Core/Player/PPG_Player.cs");var element=Read("Scripts/Assembly-CSharp/PaperPixelGames/Wheel/PPG_WheelElement.cs");
  Check(service.Contains("OceanFollowBehaviour.Instance")&&service.Contains("ocean.transform.position.y"),"Runtime sea height follows the same native ocean source as native water-aware props");
  Check(actor.Contains("module.waterSurface.position=Vector3.up*world.SeaHeight")&&Read("SushiAndFish/FishingIntegration/Runtime/MigrationEvents.cs").Contains("Vector3.up*SeaHeight"),"Authoritative fishing and client weather replicas refresh native water height");
  Check(!service.Contains("point.y>Config.seaHeight")&&!actor.Contains("Vector3.up*world.Config.seaHeight"),"Water queries and fishing actor no longer use the stale zero datum");
  Check(player.Contains("FishingMigration.OpenFishingEquipment(this,data)")&&service.Contains("data is PaperPixelGames.Core.Data.Tools.FishingRodData"),"Existing native rod selection opens the equipment page directly");
  Check(!wheel.Contains("海钓装备")&&!wheel.Contains("RegisterElement")&&!hud.Contains("AddComponent<FishingEquipmentWheel>"),"Duplicate top-level fishing group and late HUD initialization are removed");
  Check(actor.Contains("candidate.Bind(this)")&&actor.Contains("pendingEquipment=index")&&actor.Contains("Items.Count>=6"),"First selection creates the page immediately and early equip requests survive tool registration");
  Check(wheel.Contains("yield return null;if(manager&&page)")&&wheel.Contains("TryEquipMigratedFishing()")&&wheel.Contains("Time.unscaledTime+2"),"Submenu opening waits for parent close; equip waits for the native control/tool transition");
  Check(element.Contains("if(_wheel==null||!_wheel.WheelStatus)"),"First-show Awake cannot hide an already-opening native wheel element");
  Check(wheel.Contains("choiceQueued")&&wheel.Contains("page.ClearRuntimePage()"),"Submenu choices are debounced and use a clean native page");
  var module=Read("HowToFish/Runtime/FishingModule.cs");var landing=Read("HowToFish/Runtime/FishingModuleLanding.cs");var fish=Read("HowToFish/Runtime/FishingCatch.cs");var gun=Read("HowToFish/Runtime/FishWeapon.cs");
  Check(module.Contains("TryBeginDryLanding(fish)")&&module.Contains("StopLandingOnDeath(fish)"),"Near manual release uses dry delivery; death cancels remaining kinematic delivery");
  Check(landing.Contains("landingCatch=null;fish.ResumeFromLine();fish.MarkLanded()"),"A killed fish regains free gravity before its remaining landing animation can lift it");
  Check(fish.Contains("CanFloatCorpse(openWater")&&fish.Contains("else ApplyShoreWeight()"),"Dead fish float only in verified open water and fall normally on shore");
  Check(!FishingShoreRules.CanFloatCorpse(false,-1,0)&&!FishingShoreRules.CanFloatCorpse(true,1,0)&&FishingShoreRules.CanFloatCorpse(true,-1,0),"Corpse buoyancy excludes dry/air positions and accepts submerged open water");
  Check(!FishingShoreRules.CanFloatCorpse(false,-3.5f,-4)&&!FishingShoreRules.CanFloatCorpse(true,-.645f,-4)&&FishingShoreRules.CanFloatCorpse(true,-4.5f,-4),"Old zero-height hovering corpses are not buoyant above the actual -4 sea");
  Check(FishingLandingRules.IsSupportedTopContact(true,Vector3.up,new Vector3(0,-3.5f,0),new Vector3(0,-3.2f,0),0,-4),"Actual beach below world zero is valid dry landing support");
  Check(FishingShoreRules.CanEscape(true,false,true,-4.2f,-4,.15f)&&!FishingShoreRules.CanEscape(true,true,true,-4.2f,-4,.15f)&&!FishingShoreRules.CanEscape(true,false,false,-4.2f,-4,.15f),"Released fish escape only after entering actual water; line and dry shore block escape");
  Check(fish.Contains("CanEscape(Landed,Attached")&&!fish.Contains("CanEscape(HasReachedShore"),"A released fish returning directly to sea is not kept alive invisibly by missing shore contact");
  Check(fish.Contains("fishing.waterTarget.position-center")&&fish.Contains("UpdateLandHops()"),"Live fish resume high shore hops and initially steer toward their last valid water point");
  var down=FishingShoreRules.CorpseVelocity(new Vector3(1,9,2));Check(down.y==0&&down.magnitude<=4,"Lethal shot removes upward impulse without teleporting the corpse");
  Check(fish.Contains("else{ApplyShotKick(force)")&&fish.Contains("if(IsDead)return"),"Only nonlethal shots kick; corpses cannot launch from repeat shots");
  Check(!FishingWorldRules.ValidRestorePosition(new Vector3(0,-98739,0),-4,-2,false,90)&&!FishingWorldRules.ValidRestorePosition(new Vector3(float.NaN,0,0),-4,-2,false,90)&&FishingWorldRules.ValidRestorePosition(new Vector3(0,-3,0),-4,-2,true,90),"Corrupt below-world saves fall back to a nearby position without changing catch identity/health");
  Check(Read("SushiAndFish/FishingIntegration/Runtime/MigrationHarvest.cs").Contains("FishingWorldRules.ValidRestorePosition"),"Restore applies finite and lower-world validation");
  Check(actor.Contains("LayerMask.NameToLayer(\"ViewModel\")")&&actor.Contains("view=player.IsOwner&&player.MainCamera?player.MainCamera:Camera"),"Native palm and held tool use the same ViewModel camera and layer");
  Check(actor.Contains("rig.view=player.IsOwner?player.GameplayCamera:virtualCamera")&&actor.Contains("module.combat.ProjectileOrigin=ToolWorldPoint"),"Gameplay camera continues to aim physics; visible muzzle point maps to world projectiles");
  Check(module.Contains("ToolPoint(guides[i].position)")&&module.Contains("public Vector3 WorldRodTip=>ToolPoint(rod.tip.position)")&&gun.Contains("direction=(aim-origin).normalized")&&gun.Contains("combat.AddProjectile(this,origin"),"Every line guide, rod endpoint and projectile origin uses the camera mapping");
  Check(!Read("HowToFish/Runtime/FishingModuleBoundedLine.cs").Contains("rod.tip.position")&&!Read("HowToFish/Runtime/FishingModuleBites.cs").Contains("rod.tip.position"),"Haul, cast and bite positions cannot bypass mapped world rod tip");
  foreach(float fov in new[]{55f,70f,90f})CheckProjection(fov);
  RenderHands(config);
  InstallFishingMigrationAudit.Verify();
  File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_INTEGRATION_REPAIR_PASS checks="+checks.Count+" play=false simulation=false");
 }
 static void CheckProjection(float fov){
  var overlay=new GameObject("Static overlay projection",typeof(Camera)).GetComponent<Camera>();var world=new GameObject("Static world projection",typeof(Camera)).GetComponent<Camera>();
  overlay.fieldOfView=60;world.fieldOfView=fov;overlay.aspect=world.aspect=16f/9;world.transform.SetPositionAndRotation(new Vector3(10,-2,25),Quaternion.Euler(25,170,0));
  foreach(var point in new[]{new Vector3(.2f,-.2f,.6f),new Vector3(.2f,.1f,2),new Vector3(-.1f,-.1f,.4f)}){
   var mapped=FishingGripPose.ProjectToolPoint(overlay,world,point);Check(Vector2.Distance(overlay.WorldToViewportPoint(point),world.WorldToViewportPoint(mapped))*1920<.1f,"Tool/line/muzzle screen alignment at gameplay FOV="+fov);
   Check(Vector3.Distance(point,FishingGripPose.ProjectToolPoint(world,overlay,mapped))<.001f,"World hook can map back into bending rod coordinates at FOV="+fov);
  }
  Object.DestroyImmediate(overlay.gameObject);Object.DestroyImmediate(world.gameObject);
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
   Check(Vector3.Distance(tool.transform.TransformPoint(grip),palm)<.0001f,tools[i].name+": native palm and original authored tool grip meet within 0.1 mm");
   foreach(var r in frame.GetComponentsInChildren<Renderer>(true))r.enabled=r is SkinnedMeshRenderer&&r.name=="ProxyMesh";foreach(var t in frame.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;foreach(var t in tool.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
   var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Check(image.GetPixels32().Count(p=>p.r>210&&p.b>210&&p.g<65)<10,tools[i].name+": static hand pose has no missing-shader magenta");File.WriteAllBytes(Out+(i==0?"rod-hand.png":"gun-"+(i-1)+"-hand.png"),image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
   Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(tool);Object.DestroyImmediate(frame);
  }
  Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
 }
}
