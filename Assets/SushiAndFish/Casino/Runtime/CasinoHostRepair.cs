using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PaperPixelGames.Core.Player;
using SushiAndFish.Casino.Original;
namespace SushiAndFish.Casino {
public static class CasinoHostRepair {
 public static void ProtectHostEnvironment(GameObject ownedLocalManager){
  // The source's default global exposure/bloom profile competes with the
  // host weather profile. Gameplay-specific blindness/drunk/immunity volumes
  // retain their own weights and remain available to the imported items.
  foreach(var v in ownedLocalManager.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true))
   if(v.name=="PP_MainGame")v.enabled=false;
 }
 public static void TrimExterior(GameObject root){
  foreach(var t in root.GetComponentsInChildren<Transform>(true)){
   string n=t.name.ToLowerInvariant();
   if(n.Contains("road")||n.Contains("asphalt")||n=="street")t.gameObject.SetActive(false);
  }
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)){
   var mats=r.sharedMaterials;
   if(mats.Length>0 && mats.All(m=>m&&m.name.ToLowerInvariant().Contains("road")))r.gameObject.SetActive(false);
  }
  foreach(var c in root.GetComponentsInChildren<Collider>(true)){
   var s=c.bounds.size;
   // The imported Base is a 300 x 300 x 10 solid, rather than a thin plane.
   // It must never become a second invisible island beneath the host world.
   bool baseSlab=c.name=="BoxCollider"&&c.transform.parent&&c.transform.parent.name=="Base";
   if(baseSlab || (c.name=="Ground"&&c.transform.parent&&c.transform.parent.name=="Colliders") || (s.x>180&&s.z>180&&s.y<15)) c.enabled=false;
  }
  foreach(var l in root.GetComponentsInChildren<Light>(true))l.enabled=false;
  foreach(var v in root.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true))v.enabled=false;
 }
 public static void ProtectHostUI(PPG_Player player){
  if(!player||!player.IsTabletActive)return;
  Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
 }
 public static void RepairKeypad(Keypad pad){
  var cam=pad.keypadCamera;if(!cam)return;
  RepairOffscreenCamera(cam);
  // Native keypad text has its own isolated render layer. Keep it visible after host layer remapping.
  int textMask=0;foreach(var field in typeof(Keypad).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic))if(typeof(TMPro.TMP_Text).IsAssignableFrom(field.FieldType)&&field.GetValue(pad) is TMPro.TMP_Text t)textMask|=1<<t.gameObject.layer;if(textMask!=0)cam.cullingMask=textMask;
 }
 public static void RepairOffscreenCamera(Camera cam){
  if(!cam)return;
  var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
  if(pipeline){var list=(ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pipeline);int index=System.Array.FindIndex(list,r=>r&&r.name=="CasinoOffscreenRenderer");if(index>=0)cam.GetUniversalAdditionalCameraData().SetRenderer(index);}
  cam.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Base;
  cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  cam.GetUniversalAdditionalCameraData().volumeLayerMask=0;
 }
}}
