using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Saved models, one sampled art pose and static UI only. No gameplay lifecycle,
// input replay, character movement, fishing, physics step, network or save writes.
public static class InstallFishingHandling {
 const string Out="D:/UnityProjects/_CodexWork/FishingHandling-20261025/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string message){checks.Add((value?"PASS ":"FAIL ")+message);if(!value){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(message);}}
 public static void ConfigureAndVerify(){var config=MigrationConfig.Load();config.nativeHintTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/HintItem.prefab");config.nativeHintSprites=AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>("Assets/MonoBehaviour/keyboard-&-mouse_sheet.asset");EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();Verify();}
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"Outside Play; no input, network, physics or game lifecycle invoked");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var config=MigrationConfig.Load();
  Check(config.nativeHintTemplate==AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/HintItem.prefab"),"HUD references the actual original HintItem prefab");
  Check(config.nativeHintSprites==AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>("Assets/MonoBehaviour/keyboard-&-mouse_sheet.asset"),"Key icons reference the exact native keyboard sprite atlas");
  Check(!FishingShoreRules.CanEscape(false,false,true,-1,0,.15f),"A catch that has not been released cannot be classified as escaping back to water");
  Check(FishingShoreRules.CanEscape(true,false,true,-1,0,.15f),"A landed live catch really returning to open water remains escapable");
  Check(!FishingShoreRules.CanEscape(true,true,true,-1,0,.15f)&&!FishingShoreRules.CanEscape(true,false,false,-1,0,.15f)&&!FishingShoreRules.CanEscape(true,false,true,1,0,.15f),"Attached catches, dry support and airborne hops never trigger water escape");
  var actor=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingActor.cs");var landing=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleLanding.cs");var module=File.ReadAllText("Assets/HowToFish/Runtime/FishingModule.cs");
  Check(actor.IndexOf("PrepareWeaponSwitch()")<actor.IndexOf("module.combat.Equip(index)"),"A dry landing must be prepared before returning the rod and equipping a gun");
  Check(!File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingEquipmentWheel.cs").Contains("actor.equipment=index"),"Wheel selection cannot overwrite the old equipment before a rejected switch");
  Check(landing.Contains("if(hooked==fish)hooked=null")&&module.IndexOf("TickLandingMotion()")<module.IndexOf("if(!rod||!rod.item.owner||!profile)return"),"Returning the rod cannot abort the pending fish delivery; motion continues without a held rod");
  Check(landing.Contains("DrySurface(point+right*.35f")&&landing.Contains("-forward*1.6f")&&landing.Contains("EnableLandingHitboxes()"),"Delivery checks dry side/inland support and allows shooting during the visible landing");
  Check(!landing.Contains("safeUntil")&&!landing.Contains("escapeAfter")&&!landing.Contains("Invulnerable"),"No safe-period timer or timed auto-escape is introduced");
  Check(File.ReadAllText("Assets/HowToFish/Runtime/FishWeapon.cs").Contains("!item.ExternalGripPose")&&File.ReadAllText("Assets/HowToFish/Runtime/FishingRod.cs").Contains("!item.ExternalGripPose"),"Legacy gun recentering and rod pose cannot overwrite the calibrated grip");
  RenderHands(config);
  RenderHints(config);
  InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_HANDLING_PASS checks="+checks.Count+" play=false simulation=false savesChanged=false");
 }
 static void RenderHands(MigrationConfig config){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab").GetComponent<PPG_Player>();var data=new SerializedObject(prefab);var original=(Camera)data.FindProperty("_viewModelCamera").objectReferenceValue;
  var camera=new GameObject("Static native pose camera",typeof(Camera)).GetComponent<Camera>();camera.fieldOfView=original.fieldOfView;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.44f,.5f);camera.aspect=16f/9;
  var sun=new GameObject("Static model light",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(35,-30,0);sun.cullingMask=1<<29;RenderSettings.ambientLight=Color.gray;
  var module=config.systemPrefab.GetComponent<FishingModule>();var tools=new[]{module.rod.gameObject}.Concat(module.combat.guns.Select(g=>g.gameObject)).ToArray();
  for(int i=0;i<tools.Length;i++){
   var frame=Object.Instantiate(original.gameObject);frame.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);foreach(var c in frame.GetComponentsInChildren<Camera>(true))c.enabled=false;foreach(var script in frame.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
   var body=frame.GetComponentsInChildren<Animator>(true).First(a=>a.name=="BodyRig");var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/AnimationClip/"+"Rod_Idle"+".anim");clip.SampleAnimation(body.gameObject,0);
   var hand=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="hand_r");var fingers=FishingGripPose.NativeFingers(hand);var palm=FishingGripPose.Palm(hand,fingers);
   var tool=Object.Instantiate(tools[i]);foreach(var script in tool.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;var gun=tool.GetComponent<FishWeapon>();if(gun&&gun.animationPlayer&&gun.animationPlayer["Idle"]!=null)gun.animationPlayer["Idle"].clip.SampleAnimation(gun.animationPlayer.gameObject,0);
   var sockets=FishingGripPose.AuthoredFingers(tool.transform);var local=FishingGripPose.AuthoredGrip(tool.transform,sockets);var rotation=Quaternion.identity;tool.transform.SetPositionAndRotation(FishingGripPose.RootPosition(palm,rotation,local,tool.transform.lossyScale),rotation);
   var gripWorld=tool.transform.TransformPoint(FishingGripPose.AuthoredGrip(tool.transform,sockets));Check(Vector3.Distance(gripWorld,palm)<.0001f,tools[i].name+": authored grip meets the native palm within 0.1 mm in the sampled art pose");
   if(i>0)Check(sockets.Length==3,tools[i].name+": original right-hand grip bones are bound");
   foreach(var r in frame.GetComponentsInChildren<Renderer>(true))r.enabled=r is SkinnedMeshRenderer&&r.name=="ProxyMesh";foreach(var t in frame.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;foreach(var t in tool.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
   Render(camera,Out+(i==0?"rod-hand.png":"gun-"+(i-1)+"-hand.png"),1280,720);
   // Verify only camera projection geometry, with no actor or motion simulation.
   var world=new GameObject("Projection validation camera",typeof(Camera)).GetComponent<Camera>();world.transform.SetPositionAndRotation(new Vector3(100,3,-70),Quaternion.Euler(10,30,0));world.fieldOfView=90;world.aspect=camera.aspect;var projected=FishingGripPose.ProjectPalm(camera,world,palm,.55f);var a=camera.WorldToViewportPoint(palm);var b=world.WorldToViewportPoint(projected);Check(Vector2.Distance(a,b)*1280<.1f,tools[i].name+": palm screen alignment is preserved between different camera poses and FOVs");Object.DestroyImmediate(world);Object.DestroyImmediate(tool);Object.DestroyImmediate(frame);
  }
  Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
 }
 static void RenderHints(MigrationConfig config){
  var camera=new GameObject("Static HUD camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=540;camera.transform.position=new Vector3(0,0,-500);camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.4f,.65f,.66f);
  var canvas=new GameObject("Static HUD",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;canvas.GetComponent<Canvas>().worldCamera=camera;var rect=(RectTransform)canvas.transform;rect.sizeDelta=new Vector2(1920,1080);
  var hints=canvas.AddComponent<FishingNativeHints>();hints.Build(config);hints.Render(true,4,100,30,30,"冲锋枪","鱼正在落到岸边 · 切枪后可直接射击");
  // Clone actual slot artwork, without initializing inventory or invoking gameplay.
  var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/QuickSlotElement.prefab");
  for(int i=0;i<4;i++){var slot=Object.Instantiate(source,canvas.transform);foreach(var script in slot.GetComponentsInChildren<MonoBehaviour>(true))if(!(script is Graphic))script.enabled=false;var r=(RectTransform)slot.transform;r.anchorMin=r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(1,0);r.anchoredPosition=new Vector2(-28-i*100,34);r.localScale=Vector3.one;var label=slot.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name.Contains("Number"));if(label)label.text=(4-i).ToString();}
  foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1472,1080),new Vector2(2560,1080)}){
   rect.sizeDelta=size;camera.aspect=size.x/size.y;LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();
   foreach(var row in hints.Rects){LayoutRebuilder.ForceRebuildLayoutImmediate(row);var corners=new Vector3[4];row.GetWorldCorners(corners);var left=rect.InverseTransformPoint(corners[0]).x+size.x/2;var right=rect.InverseTransformPoint(corners[2]).x+size.x/2;Check(left>=0&&right<size.x-480,"Native hint is separated from original quick slots at width "+size.x+": "+row.name);}
  }
  rect.sizeDelta=new Vector2(1920,1080);camera.aspect=16f/9;foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
  var sourceBackground=config.nativeHintTemplate.transform.Find("Background").GetComponent<Image>();foreach(var row in hints.Rects){var background=row.Find("Background").GetComponent<Image>();Check(background.sprite==sourceBackground.sprite&&background.color==sourceBackground.color,"Hint reuses exact original background sprite and tint");Check(row.GetComponent<TMP_Text>().fontSize==config.nativeHintTemplate.GetComponent<TMP_Text>().fontSize,"Hint retains original text size");}
  foreach(var icon in hints.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="HintIconText")){
   Check(icon.spriteAsset&&icon.spriteAsset.spriteSheet==config.nativeHintSprites.spriteSheet,"Native hint icon preserves the original atlas texture");
   Check(icon.spriteAsset.material.shader&&icon.spriteAsset.material.shader.isSupported&&!ShaderUtil.ShaderHasError(icon.spriteAsset.material.shader),"Native hint key uses a supported UI sprite shader");
  }
  Render(camera,Out+"native-hud-gun.png",1920,1080);hints.Render(true,-1,100,0,0,"海钓竿",null);Render(camera,Out+"native-hud-rod.png",1920,1080);Object.DestroyImmediate(canvas);Object.DestroyImmediate(camera.gameObject);
 }
 static void Render(Camera camera,string path,int width,int height){
  Canvas.ForceUpdateCanvases();foreach(var label in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))label.ForceMeshUpdate();foreach(var g in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();
  var target=new RenderTexture(width,height,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Check(image.GetPixels32().Count(p=>p.r>210&&p.b>210&&p.g<65)<10,"Static artwork has no shader-error magenta: "+Path.GetFileName(path));RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
 }
}
