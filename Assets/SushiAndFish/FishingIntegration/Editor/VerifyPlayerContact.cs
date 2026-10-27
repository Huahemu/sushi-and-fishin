using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HowToFish;
using ECM2;
using PaperPixelGames.Core.Player;
using SDQQ1234;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// No Play, game inputs, movement simulation, physics stepping or save writes.
// Physics calls below are static shape queries on an isolated collider fixture.
public static class VerifyPlayerContact {
 const string Out="D:/UnityProjects/_CodexWork/PlayerContact-20261026/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string title){checks.Add((value?"PASS ":"FAIL ")+title);File.WriteAllLines(Out+"Checks.txt",checks);if(!value)throw new Exception(title);}
 static string Read(string name)=>File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/"+name+".cs").Replace("\r","");
 public static void Run(){
  Check(!EditorApplication.isPlaying,"Inspection is outside Play; no game input or physics stepping");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var gravity=Physics.gravity;float contact=Physics.defaultContactOffset;
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PlayerNGO.prefab");var capsule=player.GetComponent<CapsuleCollider>();var movement=player.GetComponent<CharacterMovement>();var boids=player.GetComponentsInChildren<BoidsCollider>(true);
  Check(capsule&&movement&&Mathf.Approximately(capsule.radius,.4f)&&Mathf.Approximately(movement.radius,.4f)&&Mathf.Approximately(capsule.height,1.85f),"Real body remains the native 0.4 m radius / 1.85 m height capsule");
  Check(boids.Length==1&&boids[0].GetComponent<SphereCollider>().radius==5&&boids[0].GetComponent<Collider>().isTrigger,"Five-metre fish-school avoidance sphere keeps its radius but is now a trigger");
  Check(player.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger).SequenceEqual(new Collider[]{capsule}),"The body capsule is the player's only enabled solid collider; no distant physical bubble remains");
  var before=File.ReadAllBytes("D:/UnityProjects/_CodexBackups/sushi-player-contact-20261026/Project/Assets/GameObject/PlayerNGO.prefab");var after=File.ReadAllBytes("Assets/GameObject/PlayerNGO.prefab");Check(before.Length==after.Length&&before.Zip(after,(a,b)=>a==b?0:1).Sum()==1,"Player prefab changes exactly one byte: avoidance sphere trigger flag, without altering body, rig, hands or movement settings");
  string boidSource=File.ReadAllText("Assets/Scripts/Assembly-CSharp/SDQQ1234/CS_BoidsVFX.cs");Check(boidSource.Contains("BoidsCollider.AllObstacles")&&boidSource.Contains("radius = sphereCollider.radius * num4")&&!boidSource.Contains("isTrigger"),"Native fish-school avoidance reads radius directly, so becoming a trigger preserves its obstacle data");
  CheckStaticQueries();CheckSources();ReportBounds();
  Check(Physics.gravity==gravity&&Physics.defaultContactOffset==contact,"Inspection leaves gravity and global contact settings unchanged");InstallFishingMigrationAudit.Verify();File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("PLAYER_CONTACT_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
 }
 static void CheckStaticQueries(){
  var player=new GameObject("Static capsule fixture");var target=new GameObject("Static boat/corpse fixture");var other=new GameObject("Static ordinary item fixture");var sensor=new GameObject("Static five-metre avoidance sensor");
  try{
   player.transform.position=new Vector3(8000,0,0);var capsule=player.AddComponent<CapsuleCollider>();capsule.radius=.4f;capsule.height=1.85f;capsule.center=Vector3.up*.925f;player.AddComponent<Rigidbody>().isKinematic=true;
   target.transform.position=player.transform.position+new Vector3(0,.925f,3);var box=target.AddComponent<BoxCollider>();box.size=new Vector3(2,1.85f,2);target.AddComponent<Rigidbody>().isKinematic=true;
   other.transform.position=player.transform.position+Vector3.right*10;var ordinary=other.AddComponent<BoxCollider>();
   sensor.transform.position=player.transform.position;var sphere=sensor.AddComponent<SphereCollider>();sphere.radius=5;sphere.isTrigger=true;Physics.SyncTransforms();
   var bottom=player.transform.position+Vector3.up*.4f;var top=player.transform.position+Vector3.up*1.45f;
   var hits=Physics.CapsuleCastAll(bottom,top,.4f,Vector3.forward,6,~0,QueryTriggerInteraction.Ignore);var before=hits.Single(h=>h.collider==box);Check(Mathf.Abs(before.distance-1.6f)<.03f&&!hits.Any(h=>h.collider==sphere),"Static sweep meets the target at actual 0.4 m body clearance; five-metre sensor does not block");
   Physics.IgnoreCollision(capsule,box,true);Check(Physics.GetIgnoreCollision(capsule,box),"PhysX capsule/target contact pair can be suppressed without freezing the target");
   hits=Physics.CapsuleCastAll(bottom,top,.4f,Vector3.forward,6,~0,QueryTriggerInteraction.Ignore);var after=hits.Single(h=>h.collider==box);Check(Mathf.Abs(after.distance-before.distance)<.0001f,"Capsule sweep still blocks at the identical surface after PhysX pair suppression");
   Check(Physics.Raycast(target.transform.position+Vector3.up*3,Vector3.down,out var support,6,~0,QueryTriggerInteraction.Ignore)&&support.collider==box,"Downward support query still detects the deck for boarding and platform support");
   var set=new HashSet<Collider>{box};var characterImpulse=new Vector3(-1,2,-3);var saved=characterImpulse;var otherImpulse=new Vector3(2,3,5);var collision=new CollisionResult{collider=box};FishingWalkCollisionGuard.FilterResponse(set,ref collision,ref characterImpulse,ref otherImpulse);
   Check(otherImpulse==Vector3.zero&&characterImpulse==saved,"Protected boat/corpse loses only the walking impulse; player's collision response is preserved");
   otherImpulse=new Vector3(2,3,5);var original=otherImpulse;collision.collider=ordinary;FishingWalkCollisionGuard.FilterResponse(set,ref collision,ref characterImpulse,ref otherImpulse);Check(otherImpulse==original&&characterImpulse==saved,"Unprotected ordinary props retain their existing impulse behaviour");
   collision.collider=null;FishingWalkCollisionGuard.FilterResponse(set,ref collision,ref characterImpulse,ref otherImpulse);Check(otherImpulse==original,"Empty contact remains a safe no-op");
   Check(!Physics.GetIgnoreCollision(capsule,ordinary),"Targeted contact suppression leaves unrelated physical pairs unchanged");
  }finally{Object.DestroyImmediate(player);Object.DestroyImmediate(target);Object.DestroyImmediate(other);Object.DestroyImmediate(sensor);}
 }
 static void CheckSources(){
  string guard=Read("FishingWalkCollisionGuard"),world=Read("MigrationWalkCollisions");
  Check(guard.Contains("movement.CapsuleIgnoreCollision(collider)")&&!guard.Contains("movement.IgnoreCollision("),"Adapter uses the physical-pair-only ECM2 API, not its query exclusion API");
  Check(guard.Contains("collisionResponseCallback+=OnResponse")&&guard.Contains("collisionResponseCallback-=OnResponse"),"Collision callback composes with existing callbacks and is removed on cleanup");
  Check(guard.Contains("ownedPairs.Add(collider)")&&guard.Contains("Physics.GetIgnoreCollision(capsule,collider)")&&guard.Contains("Physics.IgnoreCollision(capsule,collider,false)"),"Only pairs introduced by this guard are restored, preserving pre-existing ignores");
  Check(world.Contains("fish.IsAvailable&&fish.IsDead")&&world.Contains("state.health<=0"),"Portable and replicated fish receive corpse-only walk protection; live attack contacts remain physical");
  Check(world.Contains("FindObjectsByType<DeadFish>")&&world.Contains("Archipelago.Boat.hull"),"Native dropped dead fish and the complete boat hull are both included");
  Check(world.Contains("walkGuards.ContainsKey(actor.player.OwnerClientId)")&&world.Contains("obstacle.GetComponent<Collider>()")&&world.Contains("collider.isTrigger=true"),"Existing/new players get the runtime guard and avoidance-sensor correction");
  Check(Read("FishingActor").Contains("module.KilledFish+=fish=>world.ProtectWalkBody")&&Read("MigrationHarvest").Contains("ProtectWalkBody(native.gameObject)"),"Death and native conversion register protection immediately, not only in a later object scan");
  Check(world.Contains("guard.guard.Protect(body)")&&guard.Contains("!Physics.GetIgnoreCollision"),"Dropped/re-enabled colliders have their physical ignore pairs reapplied");
  string service=Read("FishingMigration");Check(service.Contains("}else TickArchipelago();\n            TickWalkCollisions();")&&service.Contains("SaveWorld();ClearWalkCollisions();UnbindWire()"),"Guard runs on host and clients and cleans up on service teardown");
  Check(Read("MigrationShotPresentation").Contains("collider.isTrigger||collider.GetComponent<SDQQ1234.BoidsCollider>()"),"Avoidance sensors and helper triggers cannot count as fish attacks on the player");
  Check(!guard.Contains("isKinematic=")&&!guard.Contains("constraints=")&&!guard.Contains("useGravity=")&&!world.Contains("IgnoreLayerCollision"),"Fix keeps boat/corpse dynamics, gravity and the global collision matrix intact");
 }
 static Bounds LocalBounds(Collider c){if(c is BoxCollider b)return new Bounds(b.center,b.size);if(c is SphereCollider s)return new Bounds(s.center,Vector3.one*s.radius*2);if(c is CapsuleCollider k){var size=Vector3.one*k.radius*2;size[k.direction]=Mathf.Max(k.height,k.radius*2);return new Bounds(k.center,size);}return new Bounds();}
 static Bounds Combined(Transform root,IEnumerable<(Transform node,Bounds bounds)> parts){var total=new Bounds();bool any=false;foreach(var part in parts)for(int i=0;i<8;i++){var p=part.bounds.center+Vector3.Scale(part.bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=root.InverseTransformPoint(part.node.TransformPoint(p));if(!any){total=new Bounds(p,Vector3.zero);any=true;}else total.Encapsulate(p);}return total;}
 static void ReportBounds(){var boat=ArchipelagoSettings.Load().boatPrefab;var body=Combined(boat.transform,boat.hull.Select(c=>(c.transform,LocalBounds(c))));var visual=Combined(boat.transform,boat.GetComponentsInChildren<MeshFilter>().Select(m=>(m.transform,m.sharedMesh.bounds)));File.WriteAllText(Out+"CollisionBounds.txt","Boat physical: "+body+"\nBoat rendered: "+visual+"\nPlayer body radius 0.4; fish-school sensor radius 5 (trigger only).\n");Check(body.size.x<visual.size.x+1&&body.size.z<visual.size.z+1&&body.size.magnitude<12,"Authored boat hull matches the visible boat envelope; no hidden five-metre expansion on the boat");}
}
