using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Static camera/volume blending and asset queries. No Play, player/trigger
// lifecycle, input simulation or physics stepping.
public static class VerifyBoundaryVolumeVisibility
{
    const string Out="D:/UnityProjects/_CodexWork/IslandBoundaryVisuals-20261028/";
    static readonly List<string> checks=new List<string>();
    [Serializable]class Data{public Row[] volumes;}
    [Serializable]class Row{public string name;public float[] inverse,center,size;public bool island2Inside;}
    static void Check(bool condition,string label){checks.Add((condition?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!condition)throw new Exception(label);}
    static Matrix4x4 Matrix(Row row){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=row.inverse[r*4+c];return m.inverse;}
    public static void Run()
    {
        Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Static post-processing blend checks only; no gameplay or physics simulation");
        var settings=ArchipelagoSettings.Load();var data=JsonUtility.FromJson<Data>(File.ReadAllText(Out+"NativeBoundary.json"));var edge=data.volumes.Single(v=>v.name=="Edge_1");
        Check(edge.island2Inside,"Actual native Edge_1 still physically contains island two, reproducing the remaining visual overlap");
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/MonoBehaviour/UnderwaterEdge.asset");
        Check(profile&&profile.TryGet<ColorAdjustments>(out var authoredColor)&&authoredColor.postExposure.overrideState&&Mathf.Approximately(authoredColor.postExposure.value,-.95f),"Native boundary profile lowers exposure by 0.95 EV");
        Check(profile.TryGet<Vignette>(out var authoredVignette)&&authoredVignette.intensity.overrideState&&Mathf.Approximately(authoredVignette.intensity.value,.433f),"Native boundary profile also adds the screenshot's strong dark edge vignette");
        // Initialize only the rendering pipeline, without a game scene/player.
        // Camera.Render is the same static editor rendering path used for assets.
        if(!VolumeManager.instance.isInitialized){var warm=new GameObject("Static pipeline initialization camera");var camera=warm.AddComponent<Camera>();var texture=new RenderTexture(4,4,0);camera.enabled=false;camera.cullingMask=0;camera.targetTexture=texture;
            try{camera.Render();}finally{camera.targetTexture=null;Object.DestroyImmediate(warm);texture.Release();Object.DestroyImmediate(texture);}}
        var go=new GameObject("Static original boundary volume");go.layer=31;var matrix=Matrix(edge);go.transform.SetPositionAndRotation(matrix.MultiplyPoint3x4(Vector3.zero),matrix.rotation);go.transform.localScale=matrix.lossyScale;
        var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.center=new Vector3(edge.center[0],edge.center[1],edge.center[2]);box.size=new Vector3(edge.size[0],edge.size[1],edge.size[2]);
        var volume=go.AddComponent<Volume>();volume.isGlobal=false;volume.priority=2;volume.blendDistance=10;volume.weight=1;volume.sharedProfile=profile;
        var view=new GameObject("Static camera sample");view.transform.position=settings.regions[2].center+Vector3.up*3.1f;
        var guard=new BoundaryVolumeVisibility();var manager=VolumeManager.instance;var stack=manager.CreateStack();
        try{
            Physics.SyncTransforms();Check((box.ClosestPoint(view.transform.position)-view.transform.position).sqrMagnitude<.0001f,"Actual scene box geometry includes island-two standing camera position");
            volume.weight=0;manager.Update(stack,view.transform,1<<31);float baselineExposure=stack.GetComponent<ColorAdjustments>().postExposure.value,baselineVignette=stack.GetComponent<Vignette>().intensity.value;
            volume.weight=1;manager.Update(stack,view.transform,1<<31);float before=stack.GetComponent<ColorAdjustments>().postExposure.value;float vignette=stack.GetComponent<Vignette>().intensity.value;
            Check(before<-.9f&&vignette>.42f,"Regression: real URP VolumeManager applies the old boundary darkening on the island");
            bool allowed=ArchipelagoTravelArea.Contains(settings,view.transform.position,-4);Check(allowed,"Island-two camera is inside the existing authorised travel area");guard.Apply(volume,allowed);
            manager.Update(stack,view.transform,1<<31);Check(Mathf.Abs(stack.GetComponent<ColorAdjustments>().postExposure.value-baselineExposure)<.001f&&Mathf.Abs(stack.GetComponent<Vignette>().intensity.value-baselineVignette)<.001f,"URP stack returns to its original baseline, removing both incorrect exposure and vignette in the authorised area");
            Check(volume.weight==0&&volume.enabled&&volume.sharedProfile==profile&&box.enabled,"Only local boundary contribution is suppressed; original collider, component and shared profile remain intact");
            guard.Apply(volume,true);guard.Apply(volume,true);guard.Apply(volume,false);Check(volume.weight==1,"Repeated safe-area updates restore the original weight on leaving, without capturing zero");
            volume.weight=.37f;guard.Apply(volume,true);guard.Release();Check(Mathf.Approximately(volume.weight,.37f),"Disable/reload cleanup restores an arbitrary authored boundary weight exactly");
            volume.weight=1;guard.Apply(volume,true);guard.Apply(null,false);Check(volume.weight==1,"Losing the player/camera/world restores the old boundary contribution");
            volume.isGlobal=true;volume.weight=.6f;guard.Apply(volume,true);Check(volume.weight==.6f,"A global lighting/weather volume is never suppressed");volume.isGlobal=false;
            foreach(int i in new[]{1,2}){view.transform.position=settings.regions[i].center+Vector3.up*3.1f;guard.Apply(volume,ArchipelagoTravelArea.Contains(settings,view.transform.position,-4));Check(volume.weight==0,settings.regions[i].title+": same visual policy covers the other island too");}
            var far=new Vector3(3000,-1,3000);guard.Apply(volume,ArchipelagoTravelArea.Contains(settings,far,-4));Check(Mathf.Approximately(volume.weight,.6f),"Leaving recognised islands/routes restores the original ocean-boundary effect");
            string source=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Ocean/OpenWatersPenaltyTrigger.cs");
            Check(source.Contains("private void LateUpdate()")&&source.Contains("player.GameplayCamera.transform.position")&&source.Contains("_boundaryVisibility.Apply(_boundaryPostProcessing, authorised)"),"Native trigger reconciles visual state from the owned gameplay camera independently of overlap callbacks");
            Check(source.Contains("_boundaryVisibility.Release();")&&source.Contains("TickOpenWaterPenalty(Time.deltaTime)")&&source.Contains("RefreshPenaltyState()"),"Cleanup is wired and original unsafe-water penalties remain present");
            Check(profile.components.OfType<ColorAdjustments>().Single().postExposure.value==-.95f&&profile.components.OfType<Vignette>().Single().intensity.value==.433f,"Shared darkening profile is unchanged after the checks");
            File.WriteAllText(Out+"VolumeBlend.txt","native exposure="+before+"\nnative vignette="+vignette+"\nauthorised-area exposure="+baselineExposure+"\nauthorised-area vignette="+baselineVignette+"\nplay=false\nphysicsSimulation=false\n");
        }finally{guard.Release();manager.DestroyStack(stack);Object.DestroyImmediate(view);Object.DestroyImmediate(go);}
        VerifyArchipelagoBoundary.Run();Debug.Log("BOUNDARY_VOLUME_VISIBILITY_VERIFIED checks="+checks.Count+" realUrpBlend=true play=false simulation=false");
    }
}
