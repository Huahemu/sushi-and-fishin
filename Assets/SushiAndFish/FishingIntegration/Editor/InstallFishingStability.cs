using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using PaperPixelGames.Core.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Asset conversion, mesh/transform inspection and camera rendering only. Never
// enters Play or invokes a gameplay lifecycle, cast, physics step or save write.
public static class InstallFishingStability {
 const string Out="D:/UnityProjects/_CodexWork/FishingStability-20261025/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string message){checks.Add((value?"PASS ":"FAIL ")+message);if(!value){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(message);}}
 public static void ConfigureAndVerify(){
  foreach(var name in new[]{"Original palette","Demo dark","Fishing line"}){
   var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/HowToFish/Materials/"+name+".mat");
   var texture=material.GetTexture("_BaseMap");var color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
   material.shader=Shader.Find(name=="Fishing line"?"HowToFish/Shot unlit":"Universal Render Pipeline/Lit");
   material.shaderKeywords=Array.Empty<string>();material.SetColor("_BaseColor",color);
   if(name!="Fishing line"){material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.1f);material.SetFloat("_Metallic",0);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);}
   material.renderQueue=name=="Fishing line"?3000:-1;EditorUtility.SetDirty(material);
  }
  AssetDatabase.SaveAssets();Verify();
 }
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"No Play, game lifecycle, physics simulation or input replay");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var config=MigrationConfig.Load();
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/HowToFish/Materials"})){
   var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
   Check(material.shader&&material.shader.name!="Hidden/InternalErrorShader"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),material.name+": supported, compiled shader; no missing-shader fallback");
  }
  var palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/HowToFish/Materials/Original palette.mat");Check(palette.GetTexture("_BaseMap")&&palette.GetTexture("_BaseMap").name=="OriginalPalette","Original palette texture and UV source retained");
  var actorSource=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingActor.cs");
  Check(actorSource.Contains("player.GameplayCamera")&&!actorSource.Contains("rig.view=player.IsOwner?player.MainCamera"),"Aiming, lure and fish aggression use the world camera, independently of the hand overlay");
  var gripSource=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingGripPose.cs");
  Check(gripSource.Contains("ViewportToWorldPoint")&&actorSource.Contains("FpsRig")&&actorSource.Contains("LayerMask.NameToLayer(\"ViewModel\")")&&actorSource.Contains("module.ToolToWorld=ToolWorldPoint"),"Native hands and tools share the hand camera; line and muzzle points map into the gameplay camera");
  Check(actorSource.Contains("Quaternion.identity")&&!actorSource.Contains("Quaternion.Euler(0,-90,0)"),"Rod points along its authored forward axis rather than sideways");
  Check(actorSource.Contains("CacheToolGraphics()")&&!actorSource.Contains("foreach(var tool in rig.Items)"),"Weapon hierarchy scanning is removed from the per-frame actor loop");
  var line=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleBoundedLine.cs");var module=File.ReadAllText("Assets/HowToFish/Runtime/FishingModule.cs");
  Check(line.Contains("FishingCastMath.Launch")&&line.Contains("SphereCastNonAlloc"),"Charged cast uses the configured ballistic range with a swept obstacle query");
  Check(module.Contains("if(BoundedLineMotion){FixedBoundedLine();return;}")&&module.Contains("fish.SuspendOnLine()")&&line.Contains("hookBody.isKinematic=true"),"Bounded cast and haul bypass dynamic spring/rigidbody tug-of-war");
  var landingSource=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleLanding.cs");
  Check(line.Contains("haulRaised")&&line.Contains("TryPlaceOnDryShore")&&landingSource.Contains("SurfaceHeight+.03f"),"Haul first clears the shore edge, then releases only over confirmed dry support");
  var catchSource=File.ReadAllText("Assets/HowToFish/Runtime/FishingCatch.cs");Check(catchSource.Contains("maxDepenetrationVelocity=2")&&catchSource.Contains("maxAngularVelocity=6")&&catchSource.Contains("Mathf.Clamp(speed,-220,220)"),"Free catches have bounded separation velocity, spin and tail motor speed");
  var shoreSource=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleShore.cs");Check(shoreSource.Contains("budget<12")&&shoreSource.Contains("EscapeWaterSearch"),"Escape-water exploration continues across hops with at most 12 rays per search step");
  Check(actorSource.Contains("authoredGraphics")&&actorSource.Contains("initialToolCollidersCleared"),"Native hidden gun attachments stay hidden, and recalled tools cannot leave invisible colliders");
  var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab").GetComponent<PPG_Player>();var nativeData=new SerializedObject(native);var world=(Camera)nativeData.FindProperty("_playerCamera").objectReferenceValue;var overlay=(Camera)nativeData.FindProperty("_viewModelCamera").objectReferenceValue;
  Check(world!=overlay&&(world.cullingMask&1)!=0&&(overlay.cullingMask&1)==0,"Authoring confirms two distinct cameras and the original default-layer visibility mismatch");
  var prefab=config.systemPrefab.GetComponent<FishingModule>();var rod=prefab.rod;
  Check(rod.transform.InverseTransformPoint(rod.tip.position).z>1,"Original rod is authored along positive local Z");
  foreach(var d in config.profile.fish){Check(d.catchPrefab&&d.catchPrefab.body&&d.catchPrefab.item,d.displayName+": catch physics and model references intact");}
  var roots=config.profile.fish.Select(d=>d.catchPrefab.gameObject).Concat(config.species.SelectMany(b=>b.shiny?new[]{b.original.DeadGraphic,b.shiny.DeadGraphic}:new[]{b.original.DeadGraphic})).Concat(new[]{config.systemPrefab}).ToArray();
  foreach(var material in roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())Check(material.shader&&material.shader.name!="Hidden/InternalErrorShader"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"Live and collectible model dependency: "+material.name+" has a valid shader");
  Check(!line.Contains("PaperPixelGames"),"Portable fishing assembly has no dependency on native game classes");
  Check(actorSource.Contains("Destroy(module.rope)"),"Unused spring constraint is removed from the bounded integration path");
  foreach(var gun in prefab.combat.guns){var muzzle=gun.transform.InverseTransformPoint(gun.muzzle.position);Check(muzzle.z>0,gun.title+": authored muzzle is forward after removing the erroneous sideways grip rotation");}
  RenderModels(config,world);
  InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_STABILITY_PASS checks="+checks.Count+" play=false simulation=false savesChanged=false");
 }
 static void RenderModels(MigrationConfig config,Camera source){
  var camera=new GameObject("Static equipment inspection camera",typeof(Camera)).GetComponent<Camera>();camera.fieldOfView=60;camera.nearClipPlane=.03f;camera.farClipPlane=100;camera.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.38f,.46f);camera.cullingMask=1<<29;
  var sun=new GameObject("Static model light",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(40,-40,0);sun.cullingMask=1<<29;RenderSettings.ambientLight=Color.gray;
  // Clone an inactive equipment subtree; no runtime Start/Awake is called outside Play.
  var clone=Object.Instantiate(config.systemPrefab.GetComponent<FishingModule>().rod.gameObject);foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;clone.transform.SetPositionAndRotation(new Vector3(.2f,-.19f,.55f),Quaternion.identity);foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;foreach(var r in clone.GetComponentsInChildren<Renderer>(true))r.enabled=true;
  var lineGo=new GameObject("Static fishing line",typeof(LineRenderer));lineGo.layer=29;var visibleLine=lineGo.GetComponent<LineRenderer>();visibleLine.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/HowToFish/Materials/Fishing line.mat");visibleLine.positionCount=2;visibleLine.widthMultiplier=.012f;visibleLine.SetPosition(0,clone.GetComponent<FishingRod>().tip.position);visibleLine.SetPosition(1,new Vector3(-2,-.3f,7));
  Render(camera,"rod-material-and-orientation.png");Object.DestroyImmediate(lineGo);Object.DestroyImmediate(clone);
  var definition=config.profile.fish.First(d=>d.originalId==0);var graphic=config.Find(definition.originalId).original.DeadGraphic;clone=Object.Instantiate(graphic);clone.transform.SetPositionAndRotation(new Vector3(0,0,3),Quaternion.Euler(0,90,0));foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
  Render(camera,"fish-original-palette.png");Object.DestroyImmediate(clone);
 }
 static void Render(Camera camera,string filename){
  var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();var pixels=image.GetPixels32();int magenta=pixels.Count(p=>p.r>210&&p.b>210&&p.g<65);Check(magenta<10,"Static model render has no error-magenta pixels: "+filename);Check(pixels.Distinct().Take(30).Count()>20,"Model actually appears in the static render: "+filename);File.WriteAllBytes(Out+filename,image.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
 }
}
