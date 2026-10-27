using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Analytic launch equations, immutable DTOs and static UI geometry/rendering.
// Never enters Play or invokes fishing, input, Rigidbody movement or save writes.
public static class InstallFishingCastHud {
 const string Out="D:/UnityProjects/_CodexWork/FishingCastHud-20261025/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(message);}}
 public static void ConfigureAndVerify(){
  var profile=MigrationConfig.Load().profile;profile.maxLine=90;profile.castChargeSeconds=1;profile.boundedCastNearDistance=14;profile.boundedCastFarDistance=65;profile.boundedCastNearSeconds=.8f;profile.boundedCastFarSeconds=1.4f;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();Verify();
 }
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"Outside Play: no gameplay, input, physics steps or player saves");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var config=MigrationConfig.Load();var profile=config.profile;
  Check(profile.maxLine>=90&&profile.castChargeSeconds==1,"Line has room for full cast and depth; charge reaches full in one second");
  Check(FishingCastMath.Distance(profile,0)==14&&FishingCastMath.Distance(profile,1)==65,"Light and full charge have clearly distinct configured ranges");
  Check(profile.maxLine-FishingCastMath.Distance(profile,1)>=20,"Full-charge landing is not cut short by the line-length sphere");
  float last=0;
  foreach(float power in new[]{0f,.25f,.5f,.75f,1f}){
   float distance=FishingCastMath.Distance(profile,power),seconds=FishingCastMath.FlightSeconds(profile,power);Check(distance>last,"Increasing charge increases range: "+power);last=distance;
   foreach(float height in new[]{.4f,1.8f,4f,12f}){
    // Evaluate the closed-form equation at its specified time. No trajectory
    // stepping, physics scene, player movement or gameplay loop is involved.
    var launch=FishingCastMath.Launch(new Vector3(0,-.8f,1),distance,seconds,config.seaHeight+height,config.seaHeight,-9.81f);
    var end=new Vector3(0,config.seaHeight+height,0)+launch*seconds+Vector3.down*(.5f*9.81f*seconds*seconds);
    Check(Mathf.Abs(end.y-config.seaHeight)<.001f&&Mathf.Abs(end.z-distance)<.001f,"Analytic water-intersection range at charge "+power+", origin height "+height);
   }
  }
  Check(!FishingCastMath.CanStartReeling(true,true,false,false),"Reel held in free flight cannot truncate a charged cast");
  Check(FishingCastMath.CanStartReeling(true,true,true,false)&&FishingCastMath.CanStartReeling(true,true,false,true),"Water entry or real obstacle contact permits retrieval");
  Check(FishingCastMath.CanStartReeling(false,true,false,false)&&FishingCastMath.CanStartReeling(true,false,false,false),"Legacy spring mode and an already retrieved line retain their controls");
  string module=File.ReadAllText("Assets/HowToFish/Runtime/FishingModule.cs"),line=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleBoundedLine.cs"),hud=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/FishingHud.cs");
  Check(module.Contains("FishingCastMath.CanStartReeling")&&line.Contains("castTouchedWater||boundedFlightBlocked"),"Runtime guards early retrieval and resumes a held input after water/solid contact");
  Check(line.Contains("SurfaceHeight+.4f")&&line.Contains("SphereCastNonAlloc")&&line.Contains("boundedFlightBlocked=true"),"Release clears the water surface and keeps solid-obstacle sweep detection");
  Check(module.Contains("displayCatch.IsAvailable&&!displayCatch.IsDead")&&module.Contains("hooked.IsAvailable&&!hooked.IsDead")&&!module.Contains("return loose["),"Native live target focus excludes killed fish and does not cycle through old loose catches");
  Check(hud.Contains("FishingHudPresentation.LiveFocus")&&!hud.Contains("FishSnapshots.Values.LastOrDefault")&&hud.Contains("panel.gameObject.SetActive(fish!=null)"),"HUD uses the current live focus, hides an empty panel and never reselects an old unrelated catch");
  Check(!hud.Contains("health.fillAmount")&&hud.Contains("FishingHudPresentation.SetHealth(health,fish.health"),"The actual HUD uses sprite-independent proportional health geometry");
  Check(!FishingHudPresentation.IsLive(null)&&!FishingHudPresentation.IsLive(new FishingSnapshot{health=0})&&FishingHudPresentation.IsLive(new FishingSnapshot{health=1}),"Zero HP and missing fish hide the bar; one HP remains a live target");
  var focuses=new Dictionary<string,FishingSnapshot>{{"killed",new FishingSnapshot{health=0}},{"other",new FishingSnapshot{health=195}}};
  Check(FishingHudPresentation.LiveFocus(focuses,"killed")==null&&FishingHudPresentation.LiveFocus(focuses,"removed")==null&&FishingHudPresentation.LiveFocus(focuses,"other")==focuses["other"],"Killing or removing the focused fish hides its bar even when another unrelated live catch exists");
  var dto=new ActorSnapshot{charging=true,castPower=1,castRange=65};var copy=JsonUtility.FromJson<ActorSnapshot>(JsonUtility.ToJson(dto));Check(copy.charging&&copy.castPower==1&&copy.castRange==65,"Network DTO preserves charge percentage and planned cast range");
  RenderHud(config);InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_CAST_HUD_PASS checks="+checks.Count+" play=false simulation=false");
 }
 static void RenderHud(MigrationConfig config){
  var camera=new GameObject("Static health presentation camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=450;camera.aspect=1280f/900;camera.transform.position=new Vector3(0,0,-500);camera.farClipPlane=1000;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.4f,.65f,.66f);camera.cullingMask=1<<30;
  var root=new GameObject("Static native fishing presentation",typeof(RectTransform),typeof(Canvas));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)root.transform).sizeDelta=new Vector2(1280,900);
  var hints=root.AddComponent<FishingNativeHints>();hints.Build(config);hints.Render(true,-1,100,0,0,"海钓竿",null,true,1,65);
  int row=0;
  foreach(int value in new[]{195,97,1,0}){
   var panel=new GameObject("Native paper fish panel "+value,typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);var pr=(RectTransform)panel.transform;pr.sizeDelta=new Vector2(530,98);pr.anchoredPosition=new Vector2(0,220-row*116);var paper=panel.GetComponent<Image>();paper.sprite=config.panelSprite;paper.type=Image.Type.Sliced;paper.raycastTarget=false;
   var background=new GameObject("Health background",typeof(RectTransform),typeof(Image));background.transform.SetParent(pr,false);var br=(RectTransform)background.transform;br.sizeDelta=new Vector2(390,11);br.anchoredPosition=new Vector2(42,0);background.GetComponent<Image>().color=new Color(.22f,.16f,.12f);
   var bar=new GameObject("Actual proportional health",typeof(RectTransform),typeof(Image)).GetComponent<Image>();bar.transform.SetParent(br,false);bar.color=new Color(.78f,.21f,.16f);FishingHudPresentation.SetHealth(bar,value,195);Canvas.ForceUpdateCanvases();
   float expected=390*Mathf.Clamp01(value/195f);Check(Mathf.Abs(bar.rectTransform.rect.width-expected)<.01f,"Rendered health width follows actual HP "+value+"/195");Check(bar.enabled==(value>0),"Zero health renders no red fill; positive health remains visible: "+value);
   var title=new GameObject("Native fish health text",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();title.transform.SetParent(pr,false);((RectTransform)title.transform).sizeDelta=new Vector2(450,30);((RectTransform)title.transform).anchoredPosition=new Vector2(0,28);title.font=config.font;title.fontSize=20;title.color=new Color(.2f,.13f,.08f);title.alignment=TextAlignmentOptions.Center;title.text="普通 · 针鱼                  "+value+" / 195";
   panel.SetActive(FishingHudPresentation.IsLive(new FishingSnapshot{health=value}));Check(panel.activeSelf==(value>0),"Killed-fish panel visibility in the saved static UI: "+value);row++;
  }
  foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate();foreach(var g in root.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();
  var target=new RenderTexture(1280,900,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1280,900,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,1280,900),0,0);texture.Apply();File.WriteAllBytes(Out+"health-and-charge-native.png",texture.EncodeToPNG());Check(texture.GetPixels32().Count(p=>p.r>210&&p.b>210&&p.g<65)<10,"Static native health/charge HUD has no shader-error magenta");RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(target);Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);
 }
}
