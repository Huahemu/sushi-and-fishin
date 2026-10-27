using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HowToFish;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;

// Saved-asset inspection and closed-form motion equations only. Does not enter
// Play, instantiate actors, run input, move rigidbodies or step physics.
public static class InstallFishingMotion {
 const string Out="D:/UnityProjects/_CodexWork/FishingMotion-20261026/";
 static readonly List<string> checks=new List<string>();
 static void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);if(!value){File.WriteAllLines(Out+"Checks.txt",checks);throw new Exception(text);}}
 public static void ConfigureAndVerify(){
  var p=MigrationConfig.Load().profile;p.hookedReelSpeed=32;p.hookedNearReelSpeed=10;p.hookedReelSlowdownDistance=3;p.hookedLiftDistance=6;p.hookedLandingDistance=2.8f;p.landGravity=28;p.shoreMoveSpeedMultiplier=1.25f;p.landHopRestMin=.12f;p.landHopRestMax=.22f;EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();Verify();
 }
 public static void Verify(){
  Check(!EditorApplication.isPlaying,"No Play, input replay, actor lifecycle, fishing or physics simulation");var p=MigrationConfig.Load().profile;var world=Physics.gravity;
  Check(p.hookedReelSpeed==32&&p.hookedNearReelSpeed==10&&p.hookedLiftDistance==6&&p.hookedLandingDistance==2.8f,"Rapid haul and near-shore lift/drop distances are configured");
  Check(p.HookedReelSpeedAtDistance(60)>=30&&p.HookedReelSpeedAtDistance(.5f)>=10,"Long retrieval and final approach both avoid the old slow reel");
  Check(p.landGravity==28&&p.landHopRestMax<=.22f&&p.shoreMoveSpeedMultiplier==1.25f,"Snappy local fall, short hop rhythm and moderate planar speed are configured");
  var source=File.ReadAllText("Assets/HowToFish/Runtime/FishingCatch.cs");var haul=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleBoundedLine.cs");var landing=File.ReadAllText("Assets/HowToFish/Runtime/FishingModuleLanding.cs");
  Check(haul.Contains("shoreDistance>profile.hookedLiftDistance")&&haul.Contains("target.y=SurfaceHeight-.3f"),"Far catches are reeled through water, rather than lifted into the sky first");
  Check(haul.Contains("magnitude<=profile.hookedLandingDistance")&&haul.Contains("fish.BottomY>SurfaceHeight+.05f")&&landing.Contains("TryFindDryLanding"),"A raised near catch drops over dry support before hovering at the rod tip");
  Check(haul.Contains("if(haulRaised){target=WorldRodTip;target.y=clearance;}"),"Looking down cannot pull a raised large fish back under the shoreline before its dry landing");
  Check(landing.Contains("FishingMotionMath.LandingPosition")&&!landing.Contains("Mathf.Sin(t*Mathf.PI)"),"Landing uses weighted acceleration instead of the old added float arc");
  Check(source.Contains("Vector3.down*ShoreGravity-Physics.gravity")&&source.Contains("ForceMode.Acceleration")&&source.Contains("part.gameObject.activeInHierarchy"),"Equal gravity correction acts on all active free fish bodies");
  Check(source.Contains("if(!fishing.BoundedLineMotion)return")&&!source.Contains("Physics.gravity="),"Fish weight correction leaves global gravity and the legacy spring mode unchanged");
  Check(source.Contains("if(nextLandHopAt<0)")&&source.Contains("if(Time.fixedTime<nextLandHopAt)return")&&!source.Contains("if(!wasGrounded)"),"Intermittent support cannot repeatedly reset the next-hop deadline");
  Check(source.Contains("part.linearDamping=0")&&source.Contains("part.useGravity=true"),"Both restored and released catches clear damping and restore free gravity");
  Check(source.Contains("fishing.Hooked==this&&!IsDead")&&source.IndexOf("FishingMotionMath.LineWriggle")<source.IndexOf("var delta=point-"),"A live attached catch visibly wriggles while keeping its hook anchored");
  Check(source.Contains("close?Vector3.Cross")&&source.Contains("shoreMoveSpeedMultiplier"),"Pursuing fish keep moving near the player instead of becoming stationary between attacks");
  Check(source.Contains("maxDepenetrationVelocity=2")&&source.Contains("maxLinearVelocity=16")&&source.Contains("Mathf.Clamp(speed,-220,220)"),"Connected-body and tail stability limits remain in place");
  float gravity=FishingMotionMath.ShoreGravity(p,true,-9.81f);Check(FishingMotionMath.ShoreGravity(p,false,-9.81f)==9.81f,"Legacy mode still uses ordinary world gravity");
  foreach(var d in p.fish){
   float height=p.landHopHeight*d.shore.hopHeightMultiplier;float speed=FishingMotionMath.HopSpeed(gravity,height);float duration=FishingMotionMath.HopSeconds(gravity,height);float legacy=FishingMotionMath.HopSeconds(9.81f,height);
   Check(Mathf.Abs(speed*speed/(2*gravity)-height)<.001f&&duration<1&&duration<legacy*.65f,d.displayName+": keeps its authored high hop, with a much shorter airborne time");
   float maximumPlanar=d.shore.attackSpeed*p.shoreMoveSpeedMultiplier;
   Check(Mathf.Sqrt(speed*speed+maximumPlanar*maximumPlanar)<16,d.displayName+": hop and attack launch fit within the existing stable velocity limit");
   Check(d.shore.escapeSpeed*p.shoreMoveSpeedMultiplier<2,d.displayName+": escape remains slower and shootable despite faster-looking movement");
   Check(d.catchPrefab.GetComponentsInChildren<Rigidbody>(true).All(b=>(b.constraints&RigidbodyConstraints.FreezePositionY)==0),d.displayName+": no body has its falling axis locked");
  }
  foreach(float drop in new[]{.05f,.5f,1.5f,3f}){
   var start=new Vector3(0,drop,0);var end=new Vector3(2,0,1);float seconds=FishingMotionMath.LandingSeconds(start,end,gravity);
   Check(seconds>=.2f&&seconds<=.46f&&FishingMotionMath.LandingPosition(start,end,0,seconds,gravity)==start&&FishingMotionMath.LandingPosition(start,end,1,seconds,gravity)==end,"Weighted drop preserves its endpoints with a brief visible duration: "+drop);
   float vertical=(end.y-start.y)/seconds+.5f*gravity*seconds;float apex=Mathf.Max(0,vertical)*Mathf.Max(0,vertical)/(2*gravity);
   Check(apex<.2f&&vertical-gravity*seconds<0,"Drop cannot add the old 0.65 m float-up arc and reaches shore descending: "+drop);
  }
  var rest=Quaternion.Euler(-90,0,0);foreach(float time in new[]{0f,.17f,1f,20f,1000f})Check(Quaternion.Angle(rest,FishingMotionMath.LineWriggle(rest,time))<23,"Suspended whole-body wriggle stays bounded without accumulating spin: "+time);
  Check(p.maxLine==90&&FishingCastMath.Distance(p,1)==65&&p.hookedMinLine==.12f,"Previous charged-cast range and short hooked line remain intact");
  Check(!FishingShoreRules.CanEscape(false,false,true,-1,0,.15f)&&FishingShoreRules.CanEscape(true,false,true,-1,0,.15f),"Only a released fish really returning to water can escape");
  Check(!FishingHudPresentation.IsLive(new FishingSnapshot{health=0}),"Killed-fish HUD stays hidden");
  Check(Physics.gravity==world,"Validation leaves world gravity unchanged");InstallFishingMigrationAudit.Verify();
  File.WriteAllLines(Out+"Checks.txt",checks);Debug.Log("FISHING_MOTION_PASS checks="+checks.Count+" play=false simulation=false");
 }
}
