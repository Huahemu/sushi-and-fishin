using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Framework.States.MainMenu;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Static UI authoring and saved-data inspection only. No Play, game bootstrap,
// input simulation, balance mutation, purchases, network sessions or save writes.
public static class InstallTestingCheats
{
 const string Out="D:/UnityProjects/_CodexWork/TestingCheats-20261025/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);if(!value){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(text);}}
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"Outside Play; game and currency commands never invoked");
  var scene=EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity",OpenSceneMode.Single);
  var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
  var settings=all.Select(t=>t.GetComponent<SettingsScreen>()).First(s=>s);
  var gameplay=(RectTransform)new SerializedObject(settings).FindProperty("_gameplayContainer").objectReferenceValue;
  var staging=new GameObject("Uninitialized settings UI only",typeof(RectTransform));staging.SetActive(false);
  var clone=Object.Instantiate(gameplay.gameObject,staging.transform);
  // The static preview cannot connect any original setting to user preferences.
  foreach(var resolver in clone.GetComponentsInChildren<ToggleUGUIResolver>(true))resolver.enabled=false;
  foreach(var localizer in clone.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))localizer.enabled=false;
  var ui=staging.AddComponent<TestingCheatSettings>();ui.Build((RectTransform)clone.transform);ui.Build((RectTransform)clone.transform);
  var row=clone.transform.Find("Content/TestingCheatModeToggle");var template=clone.transform.Find("Content/ShowHudToggle");
  Check(row&&ui.Control&&ui.Status,"Native gameplay settings gain a visible cheat toggle and explanatory status");
  Check(clone.GetComponentsInChildren<Toggle>(true).Count(t=>t.name=="TestingCheatModeToggle")==1,"Repeated settings setup creates exactly one toggle");
  Check(!row.GetComponent<ToggleUGUIResolver>(),"New toggle is disconnected from the original showHud setting");
  Check(template.GetComponent<ToggleUGUIResolver>().ID=="showHud","Original HUD resolver keeps its native setting id");
  Check(ui.Control.onValueChanged.GetPersistentEventCount()==0,"New toggle has no inherited settings callbacks");
  Check(row.GetComponent<Animator>().runtimeAnimatorController==template.GetComponent<Animator>().runtimeAnimatorController,"Cheat toggle uses the exact native switch animator and artwork");
  Check(row.Find("Background").GetComponent<Image>().sprite==template.Find("Background").GetComponent<Image>().sprite,"Native switch background sprite is preserved");
  Check(row.Find("Label").GetComponent<TMP_Text>().text=="作弊模式","Cheat setting is labeled in Chinese");
  Check(TestingCheatMode.DisplayBalance==999999&&TestingCheatMode.StoredBalance==99999900,"Native cents convert to exactly 999999 displayed coins and parts");
  Check(!ui.Control.isOn&&!ui.Control.interactable,"Default/main-menu state is off and disabled without a host save");
  Check(!TestingCheatMode.TryGetHostEconomy(out _),"Static verification cannot obtain an active host economy");
  var modeSource=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/TestingCheatMode.cs");
  Check(modeSource.Contains("economy.SetBalance(StoredBalance, StoredBalance)")&&modeSource.Contains("mode.Enabled == enabled"),"Explicit off-to-on action sets both native balances once, with duplicate protection");
  Check(!modeSource.Contains("PlayerPrefs")&&!modeSource.Contains("void Update")&&!modeSource.Contains("void Awake"),"No persistent auto-enable, startup grant or continuous balance overwrite");
  Check(all.Count(t=>t.GetComponent<Bed>())==0,"Current island scene contains no native end-of-day Bed component");
  var rest=File.ReadAllText("Assets/SushiAndFish/ResidentialIsland/Runtime/HousingRestInteractable.cs");
  Check(rest.Contains("InputKind.Interact")&&rest.Contains("session.Begin")&&!rest.Contains("ServerTrySleep")&&!rest.Contains("HandleWakeUp"),"Residential bed interaction starts a rest pose without ending the day");
  var source=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Framework/States/MainMenu/SettingsScreen.cs");
  Check(source.Contains("_testingCheats.Build(_gameplayContainer)")&&source.Contains("_testingCheats?.Refresh()"),"Both settings entry points use the shared SettingsScreen hook");
  // Render only cloned UI. The game scene is never saved or entered in Play.
  var camera=new GameObject("Static UI camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=540;camera.transform.position=new Vector3(0,0,-500);camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.88f,.83f,.71f);
  var canvas=new GameObject("Static settings canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;canvas.GetComponent<Canvas>().worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
  staging.transform.SetParent(canvas.transform,false);staging.SetActive(true);var rect=(RectTransform)clone.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,485);rect.localScale=Vector3.one;
  var header=clone.transform.Find("Header/HeaderTitle").GetComponent<TMP_Text>();header.text="游戏";header.font=MigrationConfig.Load().font;
  foreach(var label in clone.GetComponentsInChildren<TMP_Text>(true))if(label.name=="Label")label.font=MigrationConfig.Load().font;
  foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
  ui.RefreshLayout();ui.Render(true,false);Render(camera,canvas,"settings-off.png");
  ui.Render(true,true);Check(ui.Control.isOn&&ui.Control.interactable&&ui.Status.text.Contains("关闭再开启"),"Host enabled state explains how to replenish again");Render(camera,canvas,"settings-on.png");
  var corners=new Vector3[4];ui.Status.rectTransform.GetWorldCorners(corners);Check(corners.All(p=>rect.InverseTransformPoint(p).y>=-rect.rect.height),"Description is contained in the expanded gameplay category");
  ui.Render(false,false);Check(!ui.Control.interactable&&ui.Status.text.Contains("仅房主"),"Clients and main menu have a clear disabled-state explanation");
  InstallFishingMigrationAudit.Verify();
  File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("TESTING_CHEATS_PASS checks="+checks.Count+" play=false simulation=false currenciesChanged=false savesChanged=false");
 }
 static void Render(Camera camera,GameObject canvas,string name){
  LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);Canvas.ForceUpdateCanvases();
  foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();foreach(var g in canvas.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();
  var target=new RenderTexture(1920,1080,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Out+name,image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
 }
}
