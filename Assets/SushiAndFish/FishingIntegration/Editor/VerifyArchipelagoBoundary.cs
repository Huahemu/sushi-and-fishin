using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using SushiAndFish.FishingIntegration;
using PaperPixelGames.Core.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Actual prefab mesh reads, pure geographic maths and source/API audits only.
// Never constructs a player, runs trigger callbacks or steps gameplay/physics.
public static class VerifyArchipelagoBoundary
{
    const string Out="D:/UnityProjects/_CodexWork/ArchipelagoBoundary-20261027/";
    const string Native="Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Ocean/OpenWatersPenaltyTrigger.cs";
    static readonly List<string> checks=new List<string>();
    [Serializable]class BoundaryData{public BoundaryVolume[] volumes;}
    [Serializable]class BoundaryVolume{public string name;public float[] inverse,center,size;}
    static void Check(bool condition,string text){checks.Add((condition?"PASS ":"FAIL ")+text);File.WriteAllLines(Out+"Checks.txt",checks);if(!condition)throw new Exception(text);}
    static bool LegacyContains(BoundaryVolume b,Vector3 point){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=b.inverse[r*4+c];var p=m.MultiplyPoint3x4(point)-new Vector3(b.center[0],b.center[1],b.center[2]);return Mathf.Abs(p.x)<=b.size[0]/2&&Mathf.Abs(p.y)<=b.size[1]/2&&Mathf.Abs(p.z)<=b.size[2]/2;}
    public static void Run()
    {
        Check(!EditorApplication.isPlaying,"Static asset/data verification; no Play, player lifecycle, trigger callbacks or physical stepping");
        var settings=ArchipelagoSettings.Load();float sea=-4;
        Check(settings&&settings.enabled&&settings.regions.Length==3&&settings.regions.Skip(1).All(r=>r.prefab),"Both installed islands and all three configured ports are present");
        var native=JsonUtility.FromJson<BoundaryData>(File.ReadAllText(Out+"NativeBoundary.json"));var screenshot=settings.regions[2].center+Vector3.up*3.1f;
        Check(native.volumes.Length==4&&native.volumes.Any(v=>v.name=="Edge_1"&&LegacyContains(v,screenshot)),"Regression: island-two ground is inside the actual original Edge_1 open-water box");
        Check(ArchipelagoTravelArea.Contains(settings,screenshot,sea),"Island-two ground is now an authorised part of the archipelago");
        foreach(int index in new[]{1,2})
        {
            var region=settings.regions[index];var terrain=region.prefab.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Island1");var matrix=Matrix4x4.Translate(region.center)*terrain.transform.localToWorldMatrix;
            var dry=terrain.sharedMesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).Where(p=>p.y>sea+.1f).ToArray();
            Check(dry.Length>100&&dry.All(p=>ArchipelagoTravelArea.Contains(settings,p,sea)),region.title+": every exposed terrain vertex is inside the safe travel area");
            bool shore=true;for(int a=0;a<128;a++){float angle=a*Mathf.PI/64;var p=region.center+new Vector3(Mathf.Cos(angle)*(region.fishingRadius-.01f),1,Mathf.Sin(angle)*(region.fishingRadius-.01f));shore&=ArchipelagoTravelArea.Contains(settings,p,sea);}
            Check(shore,region.title+": all 128 perimeter samples of its fishing waters are authorised");
            Check(ArchipelagoTravelArea.Contains(settings,region.dock+Vector3.up,sea)&&ArchipelagoTravelArea.Contains(settings,region.center-Vector3.up*30,sea),region.title+": port and near-island diving remain safe from the unrelated open-water penalty");
        }
        int routeSamples=0;
        for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
        {
            var a=settings.regions[i].dock;var b=settings.regions[j].dock;var right=Vector3.Cross(Vector3.up,(b-a).normalized);bool pass=true;
            for(int k=0;k<=200;k++)foreach(float side in new[]{-59f,0,59f}){var point=Vector3.Lerp(a,b,k/200f)+right*side;point.y=sea+.3f;pass&=ArchipelagoTravelArea.Contains(settings,point,sea);routeSamples++;}
            Check(pass,"Ports "+i+" <-> "+j+": complete 120-metre-wide sailing corridor is safe in both directions");
        }
        Check(!ArchipelagoTravelArea.Contains(settings,new Vector3(2500,sea,2500),sea)&&!ArchipelagoTravelArea.Contains(settings,new Vector3(-2500,sea,-2500),sea),"Unrelated far ocean remains outside the authorised routes/islands");
        Check(new[]{new Vector3(2000,sea,0),new Vector3(-2000,sea,0),new Vector3(0,sea,2000),new Vector3(0,sea,-2000)}.All(p=>!ArchipelagoTravelArea.Contains(settings,p,sea)),"Map exterior remains restricted in all four directions");
        Check(!ArchipelagoTravelArea.Contains(settings,new Vector3(float.NaN,0,0),sea)&&!ArchipelagoTravelArea.Contains(settings,new Vector3(0,float.PositiveInfinity,0),sea)&&!ArchipelagoTravelArea.Contains(settings,new Vector3(0,0,float.NegativeInfinity),sea),"Invalid player coordinates are never exempted");
        Check(!ArchipelagoTravelArea.Contains(settings,screenshot,float.NaN),"An invalid sea datum cannot grant open-water exemption");
        var center=settings.regions[2].center;center.y=sea+ArchipelagoTravelArea.MinimumDepthOffset;
        Check(ArchipelagoTravelArea.Contains(settings,center,sea)&&!ArchipelagoTravelArea.Contains(settings,center-Vector3.up,sea),"Travel permission does not extend into unrelated scenes below the configured sea band");
        center.y=sea+ArchipelagoTravelArea.MaximumHeightOffset;
        Check(ArchipelagoTravelArea.Contains(settings,center,sea)&&!ArchipelagoTravelArea.Contains(settings,center+Vector3.up,sea),"Travel permission has a finite upper height boundary");
        var copy=Object.Instantiate(settings);
        try
        {
            copy.enabled=false;Check(!ArchipelagoTravelArea.Contains(copy,screenshot,sea),"Disabling the archipelago restores native open-water behaviour");
            copy.enabled=true;copy.regions=Array.Empty<FishingIslandRegion>();Check(!ArchipelagoTravelArea.Contains(copy,screenshot,sea),"Missing island configuration cannot disable map safety");
        }finally{Object.DestroyImmediate(copy);}
        CheckCorridorMath();CheckSource();
        File.WriteAllText(Out+"Routes.txt","ports=3\npairwise routes=3\nroute width=120m\nroute samples="+routeSamples+"\nisland radii="+settings.regions[1].fishingRadius+","+settings.regions[2].fishingRadius+"\nheight band="+(sea+ArchipelagoTravelArea.MinimumDepthOffset)+" to "+(sea+ArchipelagoTravelArea.MaximumHeightOffset));
        InstallFishingMigrationAudit.Verify();Debug.Log("ARCHIPELAGO_BOUNDARY_VERIFIED checks="+checks.Count+" play=false simulation=false savesWritten=false");
    }
    static void CheckCorridorMath()
    {
        var start=Vector2.zero;var end=new Vector2(100,0);
        Check(ArchipelagoTravelArea.WithinCorridor(new Vector2(50,59.9f),start,end,60)&&!ArchipelagoTravelArea.WithinCorridor(new Vector2(50,60.1f),start,end,60),"Corridor sides admit travel inside the width and reject points outside it");
        Check(!ArchipelagoTravelArea.WithinCorridor(new Vector2(161,0),start,end,60)&&ArchipelagoTravelArea.WithinCorridor(new Vector2(159,0),start,end,60),"Corridor ends are bounded capsules, not infinite strips");
        Check(ArchipelagoTravelArea.WithinCorridor(new Vector2(3,4),start,start,5)&&!ArchipelagoTravelArea.WithinCorridor(new Vector2(3,4),start,start,4),"Coincident ports have a finite, well-defined endpoint radius");
        Check(ArchipelagoTravelArea.WithinCorridor(new Vector2(50,50),start,new Vector2(100,100),1),"Diagonal island-to-island routes use perpendicular distance correctly");
    }
    static void CheckSource()
    {
        var source=File.ReadAllText(Native);var start=source.IndexOf("private bool RefreshPenaltyState()",StringComparison.Ordinal);var stop=source.IndexOf("private void OnTriggerExit",start,StringComparison.Ordinal);var state=source.Substring(start,stop-start);
        Check(source.Contains("else if (RefreshPenaltyState())")&&source.Contains("TickOpenWaterPenalty(Time.deltaTime)"),"Native oxygen penalty ticks only when the location actually requires an open-water penalty");
        Check(state.Contains("ArchipelagoTravelArea.AllowsCurrentWorld")&&!state.Contains("_localPlayerColliders.Clear()")&&!state.Contains("_isLocalPlayerInside = false"),"Safe-area entry preserves physical overlap tracking so walking beyond it re-arms the same trigger");
        Check(source.Contains("bool _isPenaltyActive")&&state.Contains("required == _isPenaltyActive")&&state.Contains("ResetOpenWaterPenalty"),"Alert and penalty timers change only on logical state transitions");
        Check(source.Contains("TryHideAlert(_alertHeader, _alertDescription)")&&!source.Contains("ForceHideAlert()"),"Returning to safety dismisses only this warning, preserving pressure/oxygen alerts");
        Check(source.Contains("StopPenalty();")&&source.Contains("HideRestaurantIndicator();")&&source.Contains("private void OnDisable()"),"Exit, disable and destroyed-player cleanup stop the penalty and remove its restaurant marker");
        Check(source.Contains("!player.IsOwner")&&source.Contains("LocalPlayer != player")&&source.Contains("_localPlayerColliders.Remove(other)"),"Existing local ownership and multi-collider overlap checks remain intact");
        var area=File.ReadAllText("Assets/SushiAndFish/FishingIntegration/Runtime/ArchipelagoTravelArea.cs");
        Check(area.Contains("if (!FishingMigration.Enabled) return false")&&area.Contains("ArchipelagoSettings.Load()")&&!area.Contains("Physics.")&&!area.Contains("Time."),"The small pure-data policy respects feature enablement and is available during world loading");
        Check(typeof(OpenWatersPenaltyTrigger).GetMethod("RefreshPenaltyState",BindingFlags.NonPublic|BindingFlags.Instance)?.ReturnType==typeof(bool),"Native trigger compiles against the new area policy without reflection hooks");
    }
}
