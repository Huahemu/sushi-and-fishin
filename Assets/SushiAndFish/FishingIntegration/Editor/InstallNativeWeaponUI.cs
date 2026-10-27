using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Framework.UI;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Saved assets and static UI rendering only: no player initialization, purchase
// commands, Play mode, input events, fishing or physics are run here.
public static class InstallNativeWeaponUI {
 const string Out="D:/UnityProjects/_CodexWork/WeaponNativePage-20261025/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition){File.WriteAllLines(Out+"NativeWeaponChecks.txt",checks);throw new Exception(message);}}
 public static void ConfigureAndVerify(){
  const string path="Assets/SushiAndFish/FishingIntegration/Resources/FishingWhiteWeaponIcon.mat";var shader=Shader.Find("SushiAndFish/Native white tool glyph");if(!shader)throw new Exception("Missing native white tool glyph shader");
  var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;material.SetFloat("_EdgePixels",5);material.SetFloat("_OutlineLuma",.30f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();Verify();
 }
 public static void Verify(){
  Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Outside Play; no gameplay or physics simulation");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var config=MigrationConfig.Load();var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");
  var cameraGo=new GameObject("Native UI authoring camera",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=96;camera.transform.position=new Vector3(0,0,-500);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.84f,.73f);camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=1000;
  var canvasGo=new GameObject("Native UI authoring canvas",typeof(RectTransform),typeof(Canvas));var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(288,161);
  RenderShopPreview(config,player,camera,canvasGo);
  string native=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Tablet/UpgradeApp.cs");
  Check(native.Contains("TrySelectWeapon(displayLevel-1)")&&native.Contains("TryBuySelected()==true")&&native.Contains("TryRefresh()==true"),"Native level, purchase and live refresh callbacks route to firearms without native tool upgrades");
  Check(native.Contains("Select(false);")&&native.Contains("RefreshLevelButtonLabels();"),"Selecting a native tool restores its original levels and rendering");
  Check(config.weapons.OrderBy(w=>w.price).Select(w=>w.price).SequenceEqual(new[]{200,600,1000,1600,2200}),"Existing five prices preserved");
  InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"NativeWeaponChecks.txt",checks);Debug.Log("NATIVE_WEAPON_UI_PASS checks="+checks.Count+" play=false simulation=false");
 }
 public static void RenderShopPreview(MigrationConfig config,GameObject player,Camera camera,GameObject canvas){
  var source=player.GetComponentInChildren<UpgradeApp>(true);var clone=Object.Instantiate(source.gameObject,canvas.transform);clone.SetActive(true);var root=(RectTransform)clone.transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.localScale=Vector3.one;
  var app=clone.GetComponent<UpgradeApp>();var view=app.CreateFishingUpgradeView();view.content.SetActive(true);var appData=new SerializedObject(app);((GameObject)appData.FindProperty("_emptyUpgradePage").objectReferenceValue).SetActive(false);
  view.diving.gameObject.SetActive(false);view.harpoon.gameObject.SetActive(false);view.scooter.gameObject.SetActive(true);view.rod.gameObject.SetActive(true);
  foreach(var localizer in clone.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))localizer.enabled=false;
  for(int i=0;i<view.choices.Length;i++)view.choices[i].SetText((i+1).ToString());
  var scooter=((SeaScooterData)appData.FindProperty("_seaScooterData").objectReferenceValue).Levels[0];view.preview.sprite=scooter.Sprite;view.title.text="水下推进器 v1";view.title.font=config.font;
  view.specs[0].text="速度："+scooter.MaxSeaScooterSpeed;view.specs[1].text="操控："+scooter.BrakingDecelerationSeaScooter;view.specs[2].text="稳定："+scooter.SeaScooterFriction;foreach(var label in view.specs)label.font=config.font;
  var costParent=view.cost.transform.parent;var previewSize=view.preview.rectTransform.sizeDelta;var previewPosition=view.preview.rectTransform.anchoredPosition;var previewAspect=view.preview.preserveAspect;var previewMesh=view.preview.useSpriteMesh;var previewColor=view.preview.color;var previewMaterial=view.preview.material;
  var shop=clone.AddComponent<FishingWeaponShop>();shop.Build(view);shop.Select(true);shop.RenderPurchaseState(false,true,true);SetLayer(canvas,30);Render(camera,canvas,Out+"firearms-native.png");
  Check(view.content.GetComponent<RectTransform>().rect.height==161,"Original 288 x 161 content area retained; no extra tabs or separate page");
  Check(shop.FirearmsButton.transform.parent==view.rod.transform.parent&&shop.FirearmsButton.transform.GetSiblingIndex()>view.rod.transform.GetSiblingIndex(),"Firearms is a peer category immediately to the right of fishing rod");
  var row=new[]{view.scooter,view.rod,shop.FirearmsButton};var centers=row.Select(b=>view.content.transform.InverseTransformPoint(((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center)).x).ToArray();
  Check(Mathf.Abs(centers.Average())<.1f&&centers[0]<centers[1]&&centers[1]<centers[2],"Scooter, fishing rod and firearms form a centered three-button row");
  Check(row.All(b=>Mathf.Approximately(((RectTransform)b.transform).rect.width,24)&&Mathf.Approximately(((RectTransform)b.transform).rect.height,24)),"All three native category buttons retain matching 24 x 24 dimensions");
  Check(shop.FirearmsButton.transform.Find("Selected").GetComponent<Image>().sprite==view.rod.transform.Find("Selected").GetComponent<Image>().sprite,"Firearms reuses the exact native circular selection frame");
  var categoryIcon=shop.FirearmsButton.GetComponent<Image>();var white=Resources.Load<Material>("FishingWhiteWeaponIcon");Check(categoryIcon.sprite==config.gunIcon&&categoryIcon.color==Color.white&&categoryIcon.material==white&&white&&!ShaderUtil.ShaderHasError(white.shader),"Top firearms icon keeps the pistol silhouette with native white fill and gray contour");Check(shop.FirearmsButton.GetComponent<Shadow>()!=null,"White weapon glyph has the same soft offset shadow treatment as native tool icons");
  var sorted=config.weapons.OrderBy(w=>w.price).ToArray();
  for(int i=0;i<5;i++){
   Check(view.choices[i].GetComponentInChildren<TMP_Text>(true).text==sorted[i].title,"Native level slot "+(i+1)+" displays "+sorted[i].title);
   Check(shop.TrySelectWeapon(i)&&shop.SelectedOffer.index==sorted[i].index&&view.preview.sprite==sorted[i].icon,"Selection "+(i+1)+" displays the corresponding real gun image and offer");
   var gun=config.systemPrefab.GetComponent<HowToFish.FishCombat>().guns[sorted[i].index];Check(view.specs[0].text.Contains(gun.damage.ToString())&&view.specs[1].text.Contains(gun.magazine.ToString()),sorted[i].title+": parameters come from the real weapon prefab");
  }
  shop.RenderPurchaseState(true,true,true);Check(!view.action.interactable&&view.actionLabel.text=="已拥有","Owned gun is shown without another purchase or upgrade");Render(camera,canvas,Out+"firearms-owned.png");
  shop.RenderPurchaseState(false,false,true);Check(!view.action.interactable&&view.actionLabel.text=="余额不足"&&view.price.color==view.insufficient,"Insufficient funds use the native disabled button and price color");
  shop.Select(false);Check(view.choices.Select(b=>b.GetComponentInChildren<TMP_Text>(true).text).SequenceEqual(new[]{"1","2","3","4","5"}),"Leaving firearms restores all five original tool level labels");
  Check(view.cost.transform.parent==costParent&&view.preview.rectTransform.sizeDelta==previewSize&&view.preview.rectTransform.anchoredPosition==previewPosition,"Leaving firearms restores original price parent and preview layout");
  Check(view.preview.preserveAspect==previewAspect&&view.preview.useSpriteMesh==previewMesh&&view.preview.color==previewColor&&view.preview.material==previewMaterial,"Leaving firearms restores the native tool image material, tint and aspect settings");
  view.preview.sprite=scooter.Sprite;view.actionLabel.text="升级";view.actionLabel.font=config.font;foreach(var button in view.choices){var selected=button.transform.Find("Selected");if(selected)selected.gameObject.SetActive(button==view.choices[0]);}view.scooter.transform.Find("Selected").gameObject.SetActive(true);
  Render(camera,canvas,Out+"scooter-native.png");
 }
 static void SetLayer(GameObject root,int layer){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
 static void Render(Camera camera,GameObject canvas,string path){foreach(var label in canvas.GetComponentsInChildren<TMP_Text>())label.ForceMeshUpdate();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);Canvas.ForceUpdateCanvases();foreach(var graphic in canvas.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();var target=new RenderTexture(1440,960,24);camera.targetTexture=target;camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();Check(pixels.GetPixels32().Distinct().Take(8).Count()>6,"Static native UI render contains visible graphics: "+Path.GetFileName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);}
}
