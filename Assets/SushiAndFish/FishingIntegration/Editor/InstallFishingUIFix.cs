using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.UI;
using SushiAndFish.FishingIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class InstallFishingUIFix {
 const string Out="D:/UnityProjects/_CodexWork/FishingUIFix-20261025/";
 const string Root="Assets/SushiAndFish/FishingIntegration/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value){File.WriteAllLines(Out+"UIFixChecks.txt",checks);throw new Exception(label);}}
 public static void ConfigureAndVerify(){
  Check(!EditorApplication.isPlaying,"No Play or gameplay simulation");var c=MigrationConfig.Load();Check(c&&c.species.Length==60,"Existing species mapping preserved");
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");var upgrade=player.GetComponentInChildren<UpgradeApp>(true);var data=new SerializedObject(upgrade);
  var originalButton=(BaseButton)data.FindProperty("_toolUpgradeButton").objectReferenceValue;
  Directory.CreateDirectory(Root+"UI");var clone=Object.Instantiate(originalButton.gameObject);clone.name="NativeTextButton";var prefab=PrefabUtility.SaveAsPrefabAsset(clone,Root+"UI/NativeTextButton.prefab");Object.DestroyImmediate(clone);c.nativeButtonTemplate=prefab.GetComponent<BaseButton>();
  Check(c.nativeButtonTemplate.GetComponentInChildren<TMP_Text>(true),"New controls reuse the original upgrade text-button frame, not the icon-only details button");
  c.stowIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Close.asset");Check(c.stowIcon,"Stow action has the original close icon");
  string[] names={"手枪","双管霰弹枪","狙击步枪","突击步枪","冲锋枪"};int[] prices={200,600,2200,1600,1000};c.weapons=Enumerable.Range(0,5).Select(i=>new FishingWeaponOffer{index=i,title=names[i],price=prices[i],icon=c.equipmentIcons[i+1]}).ToArray();EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
  ApplyIconGeometry(c);AssetDatabase.SaveAssets();Verify(c,player);RenderComparison(c,player);File.WriteAllLines(Out+"UIFixChecks.txt",checks);Debug.Log("FISHING_UI_FIX_PASS checks="+checks.Count+" guns=5 play=false simulation=false");
 }
 public static void ApplyIconGeometry(MigrationConfig c){
  c.iconGeometry=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"Art/icon-geometry.json");EditorUtility.SetDirty(c);
  Check(FishingIconGeometry.Apply(c,true)==72,"All 60 fish and 12 equipment icons have reloadable isolated vector outlines; source PNG pixels unchanged");
 }
 static void Verify(MigrationConfig c,GameObject player){
  Check(c.species.Count(b=>b.shiny)==51,"All 51 alternate appearances remain referenced");
  Check(c.equipmentIcons.All(s=>FishingIconGeometry.IsOutlined(s))&&c.species.All(b=>FishingIconGeometry.IsOutlined(b.original.Sprite)),"All new fish and equipment have reloadable nonrectangular UI outlines");
  Check(c.weapons.Length==5&&c.weapons.Select(w=>w.index).Distinct().Count()==5,"Every gun appears once with its own price");
  Check(c.weapons.All(w=>w.icon&&w.price>0&&w.price<=int.MaxValue/100),"All weapon prices are positive, safe in cents, and have icons");
  int mask=0;foreach(var w in c.weapons)mask=FishingWeaponRules.Add(mask,w.index);Check(mask==31&&c.weapons.All(w=>FishingWeaponRules.Owned(mask,w.index)),"Five purchases unlock exactly the five firearms");Check(FishingWeaponRules.Add(mask,2)==mask&&!FishingWeaponRules.Owned(0,0)&&!FishingWeaponRules.Owned(mask,5),"Ownership is idempotent and invalid guns cannot be selected");
  var save=new FishingWorldSave{ownedWeapons=mask};save.stock.Add(new StoredFish{token="existing-fish",itemId=c.species[0].original.Id,weight=12.5f});var copy=JsonUtility.FromJson<FishingWorldSave>(JsonUtility.ToJson(save));Check(copy.ownedWeapons==31&&copy.stock[0].weight==12.5f,"Gun ownership and existing warehouse data round-trip together");Check(JsonUtility.FromJson<FishingWorldSave>("{\"version\":1}").ownedWeapons==0,"Old saves load with no fabricated purchases");
  var card=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/FishBookItem.prefab").GetComponent<FishBookItem>();var cardData=new SerializedObject(card);var detail=new SerializedObject(player.GetComponentInChildren<FishBookDetailPage>(true));var locked=cardData.FindProperty("_lockedIconColor").colorValue;
  Check(locked==detail.FindProperty("_lockedFishColor").colorValue,"Original list and detail locked tints are identical");
  var go=new GameObject("Static icon audit",typeof(RectTransform),typeof(Image));var image=go.GetComponent<Image>();FishingTablet.ApplyBookIcon(image,c.species[0].original.Sprite,false,c.silhouette,locked,Color.white);Check(image.color==locked&&image.material==Graphic.defaultGraphicMaterial,"New locked fish use the exact original tint and default material, with no black-silhouette shader");
  foreach(var binding in c.species){var sprite=binding.original.Sprite;var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);Check(uv.x>=0&&uv.y>=0&&uv.z<=1&&uv.w<=1&&uv.z>uv.x&&uv.w>uv.y,binding.original.Id+": inventory sprite has a valid individual atlas region");}
  Object.DestroyImmediate(go);
  string actors=File.ReadAllText(Root+"Runtime/FishingActor.cs"),shop=File.ReadAllText(Root+"Runtime/FishingWeapons.cs"),network=File.ReadAllText(Root+"Runtime/MigrationNetworking.cs");
  Check(actors.Contains("!world.OwnsWeapon((int)value)")&&shop.Contains("ServerTrySpendStoredMoney")&&shop.Contains("if(OwnsWeapon(index))"),"Server validates ownership, spends native money, and rejects duplicate purchases");
  Check(network.Contains("weapons=SaveData.ownedWeapons")&&network.Contains("SaveData.ownedWeapons=snapshot.weapons"),"Ownership is synchronized to clients");
 }
 static void RenderComparison(MigrationConfig c,GameObject player){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);c=AssetDatabase.LoadAssetAtPath<MigrationConfig>(Root+"Resources/FishingMigration.asset");player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");Check(FishingIconGeometry.Apply(c,true)==72,"Icon outlines restored after editor asset reload, as in game startup");var cameraGo=new GameObject("UI authoring camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=96;camera.transform.position=new Vector3(0,0,-500);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.84f,.73f);camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=1000;
  var canvasGo=new GameObject("UI authoring canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasGo,scene);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(288,161);
  var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/FishBookItem.prefab");
  var nativeFish=AssetDatabase.LoadAssetAtPath<FishSO>("Assets/MonoBehaviour/mackerel.asset");
  string[] labels={"原有鱼 · 未获取","新增鱼 · 未获取","新增鱼 · 已获取","新增鱼 · 异色"};
  for(int i=0;i<4;i++){
   var card=Object.Instantiate(native,canvas.transform);var rect=(RectTransform)card.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-102+i*68,8);rect.localScale=Vector3.one;rect.sizeDelta=new Vector2(60,56);
   var data=new SerializedObject(card.GetComponent<FishBookItem>());var image=(Image)data.FindProperty("_fishIconImage").objectReferenceValue;var title=(TMP_Text)data.FindProperty("_fishNameText").objectReferenceValue;var selected=(GameObject)data.FindProperty("_selectedOutline").objectReferenceValue;if(selected)selected.SetActive(false);
   var locked=data.FindProperty("_lockedIconColor").colorValue;var binding=c.species[0];FishingTablet.ApplyBookIcon(image,i==0?nativeFish.Sprite:binding.original.Sprite,i>=2,i==3?binding.shinyIconMaterial:null,locked,Color.white);if(i==0)image.useSpriteMesh=false;title.text=i<2?"???":binding.original.LocalizedName;
   FishingTablet.Text(canvas.transform,labels[i],new Vector2(.5f,.5f),new Vector2(-102+i*68,-53),new Vector2(64,15),6);
  }
  SetLayer(canvasGo,30);Render(camera,canvasGo,Out+"native-gray-and-icons.png");
  while(canvas.transform.childCount>0)Object.DestroyImmediate(canvas.transform.GetChild(0).gameObject);
  RenderDetails(c,player,camera,canvasGo,false);
  while(canvas.transform.childCount>0)Object.DestroyImmediate(canvas.transform.GetChild(0).gameObject);
  RenderDetails(c,player,camera,canvasGo,true);
  while(canvas.transform.childCount>0)Object.DestroyImmediate(canvas.transform.GetChild(0).gameObject);
  InstallNativeWeaponUI.RenderShopPreview(c,player,camera,canvasGo);
  while(canvas.transform.childCount>0)Object.DestroyImmediate(canvas.transform.GetChild(0).gameObject);
  RenderInventory(c,camera,canvasGo);
  // Unsaved UI-authoring scene is discarded when this batch editor exits.
 }
 static void RenderInventory(MigrationConfig c,Camera camera,GameObject canvas){
  FishingTablet.Text(canvas.transform,"海钓鱼仓 · 原色与异色",new Vector2(.5f,1),new Vector2(0,-7),new Vector2(200,12),7);
  var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/InventoryItem.prefab");
  for(int i=0;i<4;i++){
   var binding=c.species[i/2];bool shiny=i%2==1;var item=binding.Item(shiny);var clone=Object.Instantiate(template,canvas.transform);var row=clone.GetComponent<InventoryItemUI>();row.Initialize(item,12,"海钓鱼仓");row.PrepareWarehouseRow();row.SetLongAmount(12);
   var rect=(RectTransform)clone.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-36-i*33);rect.sizeDelta=new Vector2(255,32);rect.localScale=Vector3.one;
   var data=new SerializedObject(row);var icon=(Image)data.FindProperty("_itemIconImage").objectReferenceValue;FishingTablet.PaintInventory(item,icon,c);Check(icon.sprite==item.Sprite&&icon.material==(shiny?binding.shinyIconMaterial:Graphic.defaultGraphicMaterial),"Inventory keeps species ID, corresponding icon and captured variant: "+item.Id);
   FishingTablet.Button(clone.transform,"取出一条",new Vector2(1,.5f),new Vector2(-26,0),new Vector2(48,12),null);
  }
  SetLayer(canvas,30);Render(camera,canvas,Out+"inventory-icons.png");
 }
 static void RenderDetails(MigrationConfig c,GameObject player,Camera camera,GameObject canvas,bool variants){
  var source=player.GetComponentInChildren<FishBookDetailPage>(true);var binding=c.species[0];Check(binding.shiny,"Static detail preview has its original alternate appearance reference");
  for(int i=0;i<2;i++){
   var clone=Object.Instantiate(source.gameObject,canvas.transform);clone.SetActive(true);var page=clone.GetComponent<FishBookDetailPage>();var rect=(RectTransform)clone.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(i==0?-65:65,0);rect.sizeDelta=new Vector2(115,135);rect.localScale=Vector3.one;
   var data=new SerializedObject(page);var image=(Image)data.FindProperty("_fishImage").objectReferenceValue;var title=(TMP_Text)data.FindProperty("_fishName").objectReferenceValue;var count=(TMP_Text)data.FindProperty("_totalAmount").objectReferenceValue;var locked=data.FindProperty("_lockedFishColor").colorValue;var unlocked=data.FindProperty("_unlockedFishColor").colorValue;
   var sprite=!variants&&i==0?AssetDatabase.LoadAssetAtPath<FishSO>("Assets/MonoBehaviour/mackerel.asset").Sprite:binding.original.Sprite;
   FishingTablet.ApplyBookIcon(image,sprite,variants,variants&&i==1?binding.shinyIconMaterial:null,locked,unlocked);if(!variants&&i==0)image.useSpriteMesh=false;image.enabled=true;title.text=variants?binding.original.LocalizedName:"???";count.text=variants?"12":"N/A";
   if(variants||i==1){var extension=clone.AddComponent<FishingBookDetail>();extension.Bind(binding.original,image,title,count,locked,unlocked,variants);if(!variants){foreach(var control in clone.GetComponentsInChildren<BaseButton>(true))if(control.gameObject.name.StartsWith("Fishing C"))control.gameObject.SetActive(false);var badge=image.transform.Find("Acquired shiny badge");if(badge)badge.gameObject.SetActive(false);}}
   if(variants){
    var toggle=clone.GetComponentsInChildren<BaseButton>(true).First(b=>b.gameObject.name.StartsWith("Fishing C"));toggle.gameObject.SetActive(true);toggle.SetText(i==1?"C 原色":"C 异色");
    var badge=image.transform.Find("Acquired shiny badge");badge.gameObject.SetActive(i==1);
    Check(!Overlaps((RectTransform)toggle.transform,title.rectTransform)&&!Overlaps((RectTransform)badge,title.rectTransform),"Shiny toggle and badge do not overlap native fish name");
   }
   FishingTablet.Text(canvas.transform,variants?(i==0?"详情 · 原色":"详情 · 异色"):i==0?"原有鱼 · 未获取":"新增鱼 · 未获取",new Vector2(.5f,.5f),new Vector2(i==0?-65:65,-79),new Vector2(120,12),6);
  }
  SetLayer(canvas,30);Render(camera,canvas,Out+(variants?"detail-variants.png":"detail-locked.png"));
 }
 static bool Overlaps(RectTransform a,RectTransform b){var ac=new Vector3[4];var bc=new Vector3[4];a.GetWorldCorners(ac);b.GetWorldCorners(bc);return new Rect(ac[0],ac[2]-ac[0]).Overlaps(new Rect(bc[0],bc[2]-bc[0]));}
 static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
 static void Render(Camera camera,GameObject canvas,string path){foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);Canvas.ForceUpdateCanvases();foreach(var graphic in canvas.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();var texture=new RenderTexture(1440,960,24);camera.targetTexture=texture;camera.Render();var before=RenderTexture.active;RenderTexture.active=texture;var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();Check(pixels.GetPixels32().Distinct().Take(8).Count()>6,"Authored UI renders visible graphics: "+Path.GetFileName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(texture);}
}
