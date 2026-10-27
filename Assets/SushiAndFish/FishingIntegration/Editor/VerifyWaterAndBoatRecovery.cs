using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ECM2;
using SushiAndFish.FishingIntegration;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Deterministic velocity math, static geometry queries and DTOs only.
// No Play, character lifecycle, gameplay input or physics stepping.
public static class VerifyWaterAndBoatRecovery {
 const string Out="D:/UnityProjects/_CodexWork/WaterAndBoatRecovery-20261027/";static readonly List<string> checks=new List<string>();
 static void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines(Out+"Checks.txt",checks);if(!value)throw new Exception(text);}
 static string Read(string name)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+name+".cs").Replace("\r","");
 public static void Run(){Check(!EditorApplication.isPlaying,"No Play, gameplay input, character lifecycle or physics stepping");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var gravity=Physics.gravity;var settings=ArchipelagoSettings.Load();
  var incoming=new Vector3(3,-25,4);var limited=ArchipelagoWaterMath.EntryVelocity(incoming,Vector3.up,0,0,true);Check(limited==new Vector3(3,-2.2f,4),"Water entry caps downward impact speed at 2.2 m/s and keeps horizontal momentum");
  var slowed=ArchipelagoWaterMath.EntryVelocity(limited,Vector3.up,0,.1f,false);var settled=ArchipelagoWaterMath.EntryVelocity(slowed,Vector3.up,0,.15f,false);Check(Mathf.Abs(slowed.y+1.2f)<.001f&&settled.y==0&&settled.x==3&&settled.z==4,"Brief entry damping removes passive sinking without freezing horizontal swimming");
  Check(ArchipelagoWaterMath.EntryVelocity(new Vector3(1,3,2),Vector3.up,0,.1f,false)==new Vector3(1,3,2),"Upward swimming and surfacing jumps are not clamped");
  Check(ArchipelagoWaterMath.EntryVelocity(limited,Vector3.up,-1,.1f,false)==limited,"Explicit downward swimming is not damped by the passive-entry assist");
  var bounds=ArchipelagoWaterMath.WaterBounds(settings,-4);Check(Mathf.Abs(bounds.max.y+3.9f)<.001f&&settings.regions.All(r=>bounds.Contains(new Vector3(r.dock.x,-6,r.dock.z))),"Fallback water has the native surface datum and covers every dock and connecting sea");
  var parent=new GameObject("Static water geometry");var volume=ArchipelagoWaterTraversal.CreateWater(settings,-4,parent.transform);Check(volume.waterVolume&&volume.priority==-100&&volume.friction==1&&volume.maxFallSpeed==2&&volume.boxCollider.isTrigger,"Fallback uses native swim friction/fall limits and lower priority than existing or dry volumes");Object.DestroyImmediate(parent);
  foreach(string name in new[]{"AddPhysicsVolume","RemovePhysicsVolume"}){var method=typeof(Character).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);Check(method!=null&&method.ReturnType==typeof(void)&&method.GetParameters().Single().ParameterType==typeof(Collider),"Native volume registry bridge is valid: "+name);}
  var from=new Vector3(0,-1,0);var to=new Vector3(0,.4f,1);Check(ArchipelagoWaterMath.ClimbPosition(from,to,0)==from&&ArchipelagoWaterMath.ClimbPosition(from,to,1)==to,"Climb motion has exact start and supported final endpoints");Check(ArchipelagoWaterMath.ClimbPosition(from,to,.45f).z==from.z&&ArchipelagoWaterMath.ClimbPosition(from,to,.55f).y>to.y,"Climb raises the body above the edge before moving over it");
  CheckShoreQueries();CheckBoatQueries();CheckSources();
  Check(Physics.gravity==gravity,"Validation leaves global gravity unchanged");InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("WATER_BOAT_RECOVERY_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
 }
 static GameObject Box(string name,Vector3 center,Vector3 size){var go=new GameObject(name);go.transform.position=center;go.AddComponent<BoxCollider>().size=size;return go;}
 static void CheckShoreQueries(){var origin=new Vector3(8000,-1,0);var platform=Box("Static climbable dock",new Vector3(8000,-.3f,3),new Vector3(6,1.4f,2));GameObject roof=null;
  try{Physics.SyncTransforms();bool found=ArchipelagoShoreQueries.TryFind(origin,Vector3.forward,.4f,1.85f,null,out var end,out var support);Check(found&&support==platform.transform&&end.y>.4f&&end.z>2,"Broad shore search finds a dry dock top within reach from the water");
   Check(ArchipelagoShoreQueries.Clear(end,.4f,1.85f,null),"Climb endpoint has full standing-capsule clearance");
   Check(!ArchipelagoShoreQueries.TryFind(origin-Vector3.forward*3,Vector3.forward,.4f,1.85f,null,out _,out _),"Shore assist cannot reach distant platforms");
   roof=Box("Static blocked headroom",new Vector3(8000,1.35f,2),new Vector3(6,.25f,6));Physics.SyncTransforms();Check(!ArchipelagoShoreQueries.TryFind(origin,Vector3.forward,.4f,1.85f,null,out _,out _),"Overhead obstruction prevents climbing through a ceiling");Object.DestroyImmediate(roof);roof=null;
   platform.GetComponent<BoxCollider>().size=new Vector3(6,7,2);platform.transform.position=new Vector3(8000,2.5f,3);Physics.SyncTransforms();Check(!ArchipelagoShoreQueries.TryFind(origin,Vector3.forward,.4f,1.85f,null,out _,out _),"High cliff tops remain outside the two-metre climb limit");
  }finally{if(roof)Object.DestroyImmediate(roof);Object.DestroyImmediate(platform);}
 }
 static void CheckBoatQueries(){var origin=new Vector3(9000,2,0);var land=Box("Static stranded-boat land",new Vector3(9000,-.2f,0),new Vector3(8,1,14));var settings=ScriptableObject.CreateInstance<ArchipelagoSettings>();settings.boatSpawn=new Vector3(9020,.25f,0);settings.regions=new[]{new FishingIslandRegion{center=new Vector3(9000,0,0),dock=settings.boatSpawn}};
  try{Physics.SyncTransforms();Check(!ArchipelagoBoatSafety.ClearWater(origin,Quaternion.identity,0),"Boat footprint rejects dry land under the hull");Check(ArchipelagoBoatSafety.ClearWater(settings.boatSpawn,Quaternion.identity,0),"Open sea with room for the full hull is accepted");
   Check(!ArchipelagoBoatSafety.SavedPoseUsable(origin,Quaternion.identity,0)&&!ArchipelagoBoatSafety.SavedPoseUsable(new Vector3(9000,-3,0),Quaternion.identity,0)&&!ArchipelagoBoatSafety.SavedPoseUsable(settings.boatSpawn,Quaternion.Euler(180,0,0),0),"Elevated, sunk and capsized old saved poses require recovery");
   Check(ArchipelagoBoatSafety.TryFind(settings,origin,70,0,null,out var restored,out var rotation)&&restored.y==.25f&&Vector3.Distance(restored,origin)<30&&Vector3.Dot(rotation*Vector3.up,Vector3.up)>.99f,"Stranded old boat resolves to nearby water and upright heading");
   Check(ArchipelagoBoatSafety.ClearWater(restored,rotation,0),"Recovered pose has clearance at the complete hull footprint");
   land.GetComponent<BoxCollider>().size=new Vector3(2000,1,2000);Physics.SyncTransforms();Check(!ArchipelagoBoatSafety.TryFind(settings,origin,0,0,null,out _,out _),"If every search candidate is obstructed, recovery fails without placing a boat on land");
  }finally{Object.DestroyImmediate(land);Object.DestroyImmediate(settings);}
  var dto=new SailingSnapshot{position=new Vector3(4,0,8),rotation=Quaternion.identity,driver=ulong.MaxValue,relocation=3};var round=JsonUtility.FromJson<SailingSnapshot>(JsonUtility.ToJson(dto));Check(round.relocation==3&&round.position==dto.position,"Relocation revision survives the existing network DTO");Check(JsonUtility.FromJson<SailingSnapshot>("{\"position\":{\"x\":1,\"y\":0,\"z\":2}}").relocation==0,"Snapshots without relocation revision keep backwards-compatible zero default");
 }
 static void CheckSources(){string traversal=Read("ArchipelagoWaterTraversal"),world=Read("ArchipelagoWorld"),rig=Read("OriginalBoatRig"),link=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/link.xml");
  Check(traversal.Contains("volume.priority>selected.priority")&&traversal.Contains("else removeVolume(volume.boxCollider)")&&traversal.Contains("addVolume(volume.boxCollider)"),"Water registry reconciles actual containment and respects dry-volume priorities");
  Check(traversal.Contains("c.physicsVolume==selected&&c.IsFalling()")&&traversal.Contains("c.GetVelocity().y<=0"),"Descending characters already inside water return from erroneous falling to swimming");
  Check(traversal.Contains("MovementModeChanged+=OnModeChanged")&&traversal.Contains("previous==Character.MovementMode.SeaScooter"),"Entry handling listens to the native transition and preserves scooter transitions");
  Check(traversal.Contains("player.IsTrapped||world.IsDriver(player)")&&traversal.Contains("c.HasActiveMotion"),"Swimming assist does not override helm control, traps or existing character motions");
  Check(traversal.Contains("jumpUntil=Time.time+.4f")&&traversal.Contains("Time.time<jumpUntil")&&traversal.Contains("jump||"),"Buffered or held jump allows generous shore access instead of a single-frame trigger");
  Check(traversal.Contains("support.TransformPoint(supportLocal)")&&traversal.Contains("ArchipelagoShoreQueries.ClearPath"),"Climb follows moving boat support and rechecks the path against new obstacles");
  Check(traversal.Contains("TryRestoreCharacterControlAfterModeExit")&&traversal.Contains("MovementModeChanged-=OnModeChanged")&&traversal.Contains("removeVolume?.Invoke"),"Completion, cancellation and teardown restore native controls and volume subscriptions");
  Check(traversal.Contains("if(!disposed&&c.characterMovement.collider.enabled)world.Service.RefreshWalkContacts()")&&!traversal.Contains("characterMovement.enabled="),"Climb completion immediately restores no-push pairs without overriding external motor-disable states");
  Check(link.Contains("AddPhysicsVolume")&&link.Contains("RemovePhysicsVolume"),"IL2CPP preserves the native water-registry bridge");
  Check(world.Contains("if(Server&&valid)")&&world.Contains("SavedPoseUsable")&&world.Contains("ArchipelagoBoatSafety.TryFind"),"Host validates old boat poses before instantiating them");
  Check(world.Contains("Boat.driver!=ulong.MaxValue||Vector3.Distance")&&world.Contains(">12||actor.equipped")&&world.Contains("recoveryCooldown=Time.unscaledTime+3"),"Manual recovery is host-authoritative, nearby, empty-handed, unoccupied and rate limited");
  Check(world.Contains("Time.unscaledTime-recoveryHold>=2")&&world.Contains("InputActionPhase.Canceled&&recoveryHold>=0"),"Hold-to-recover requires two seconds and supports cancel on release");
  Check(world.Contains("CarryCargo(turn,position,old)")&&world.Contains("if(riding)MovePassenger")&&world.Contains("lastBoatPosition=position;lastBoatRotation=rotation"),"Relocation carries deck contents and riders without a second boat-delta translation");
  Check(world.Contains("snapshot.relocation!=remoteRelocation")&&rig.Contains("snap||!remoteReceived"),"Observers snap to a recovery pose instead of interpolating through terrain");
  Check(Read("FishingNativeHints").Contains("RenderWaterAccess")&&world.Contains("长按 E 2秒")&&traversal.Contains("按住空格"),"Shore and recovery prompts reuse native hint rows and existing controls");
 }
}
