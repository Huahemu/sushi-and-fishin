using System;
using System.IO;
using System.Linq;
using System.Text;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Asset construction and static geometry inspection only. No player, boat
// controller, gameplay session, physics step or input is started here.
public static class InstallNativeDocks
{
    const string Out = "D:/UnityProjects/_CodexWork/OriginalIslandDocks-20261028/";
    const string NativeCity = "Assets/SushiAndFish/RevisionReference_20261021/OriginalCityBeforeTrim.unity";
    const string GameState = "Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
    const string CurrentCity = "Assets/SushiAndFish/Modules/OriginalIsland/Scenes/DressingScenes/100_Dressing_City_01.unity";
    const string Prefabs = "Assets/SushiAndFish/FishingIntegration/Archipelago/Prefabs/";
    const string Access = Prefabs + "HowToFishAccessDocks.prefab";
    public const string Dock = Prefabs + "NativeCityDock.prefab";
    static readonly StringBuilder report = new StringBuilder();
    static Vector3 mainFrame;static Quaternion mainRotation;static float length,width,deckHeight;
    static GameObject boardTemplate,poleTemplate;

    static void Require(bool value,string text) { report.AppendLine((value?"PASS ":"FAIL ")+text);File.WriteAllText(Out+"DockChecks.txt",report.ToString());if(!value)throw new Exception(text); }
    static Bounds Geometry(GameObject root,Func<MeshRenderer,bool> select=null)
    {
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>select==null||select(r)).ToArray();
        if(renderers.Length==0)throw new Exception("Missing native dock geometry");
        var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static void Strip(GameObject root,bool keepColliders)
    {
        foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(c);
        if(!keepColliders)foreach(var c in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))r.enabled=true;
    }
    public static void Build()
    {
        Require(!EditorApplication.isPlaying,"Static source/prefab construction only; no Play or gameplay simulation");
        var scene=EditorSceneManager.OpenScene(NativeCity,OpenSceneMode.Single);
        var source=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Dock (1)");
        Require(source.GetComponentsInChildren<MeshRenderer>(true).Length>20,"Complete original city wooden pier, posts and ladder found in preserved native scene");
        var root=new GameObject("Native city wooden dock");
        var visual=Object.Instantiate(source.gameObject,root.transform);visual.name="Original city dock model";
        visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=source.lossyScale;Strip(visual,true);
        var deck=Geometry(visual,r=>r.name.StartsWith("Object",StringComparison.Ordinal)&&r.bounds.size.y<.6f&&r.bounds.size.x*r.bounds.size.z>.5f);
        var pivot=new Vector3(deck.center.x,deck.max.y,deck.min.z);
        mainFrame=source.position+source.rotation*pivot;mainRotation=source.rotation;
        visual.transform.localPosition=-pivot;length=deck.size.z;width=deck.size.x;deckHeight=mainFrame.y+4f;
        // One continuous floor under the native planks prevents snagging in seams.
        var floor=new GameObject("Continuous native dock walking surface",typeof(BoxCollider));floor.transform.SetParent(root.transform,false);
        floor.GetComponent<BoxCollider>().center=new Vector3(0,-.12f,length*.5f);floor.GetComponent<BoxCollider>().size=new Vector3(width,.14f,length);
        Require(width>3&&length>10&&deckHeight>.5f&&deckHeight<2,"Original dock dimensions and water-to-deck height retained: width="+width+" length="+length+" aboveSea="+deckHeight);
        PrefabUtility.SaveAsPrefabAsset(root,Dock);Object.DestroyImmediate(root);
        var old=AssetDatabase.LoadAssetAtPath<GameObject>(Access);var settings=ArchipelagoSettings.Load();
        var land=new Vector3[3];
        for(int i=1;i<3;i++)
        {
            var frame=old.transform.Cast<Transform>().Single(t=>t.name==settings.regions[i].title+" · 靠岸栈桥");
            var collider=frame.GetComponent<BoxCollider>();land[i]=frame.position-frame.forward*((collider.size.z-.8f)*.5f);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var access=new GameObject("Original city docks and island access");var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dock);
        var board=model.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Object32491");
        boardTemplate=Object.Instantiate(board.gameObject);boardTemplate.name="Native textured ramp board";Strip(boardTemplate,false);
        boardTemplate.transform.SetPositionAndRotation(Vector3.zero,board.transform.rotation);boardTemplate.transform.localScale=board.transform.lossyScale;
        var bb=Geometry(boardTemplate);boardTemplate.transform.position-=new Vector3(bb.center.x,bb.max.y,bb.center.z);
        var pole=model.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="pCylinder1");
        poleTemplate=Object.Instantiate(pole.gameObject);poleTemplate.name="Native wooden support pile";Strip(poleTemplate,false);
        poleTemplate.transform.SetPositionAndRotation(Vector3.zero,pole.transform.rotation);poleTemplate.transform.localScale=pole.transform.lossyScale;
        var pb=Geometry(poleTemplate);poleTemplate.transform.position-=new Vector3(pb.center.x,pb.max.y,pb.center.z);
        // No second main-island pier: use the existing city pier at its authored location.
        for(int i=1;i<3;i++)
        {
            var region=settings.regions[i];var outward=new Vector3(-1,0,-1).normalized;var rotation=Quaternion.LookRotation(outward);
            var end=region.dock;end.y=-4+deckHeight;var start=end-outward*length;
            var dock=(GameObject)PrefabUtility.InstantiatePrefab(model);dock.name=region.title+" · 原城镇木码头";dock.transform.SetParent(access.transform,false);dock.transform.SetPositionAndRotation(start,rotation);
            MakeRamp(access.transform,region.title,land[i],start+outward*.3f);
            var low=end+outward*9;low.y=-5.6f;
            MakeRamp(access.transform,region.title+" · 水面登岸",low,end-outward*.35f,3.2f);
            report.AppendLine("ISLAND "+i+" deckStart="+start.ToString("F4")+" deckEnd="+end.ToString("F4")+" land="+land[i].ToString("F4"));
        }
        Object.DestroyImmediate(boardTemplate);boardTemplate=null;Object.DestroyImmediate(poleTemplate);poleTemplate=null;
        PrefabUtility.SaveAsPrefabAsset(access,Access);Object.DestroyImmediate(access);
        // Native city and all collision are inspected without saving either scene.
        EditorSceneManager.OpenScene(GameState,OpenSceneMode.Single);
        EditorSceneManager.OpenScene(CurrentCity,OpenSceneMode.Additive);
        settings=AssetDatabase.LoadAssetAtPath<ArchipelagoSettings>("Assets/SushiAndFish/FishingIntegration/Resources/FishingArchipelago.asset");
        model=AssetDatabase.LoadAssetAtPath<GameObject>(Dock);
        var native=Object.Instantiate(model);native.transform.SetPositionAndRotation(mainFrame,mainRotation);
        var accessPreview=Object.Instantiate(settings.accessPrefab);
        Physics.SyncTransforms();bool found=false;
        foreach(float offset in new[]{4.5f,5f,5.5f,6f,6.5f})
        {
            var candidate=mainFrame+mainRotation*new Vector3(offset,0,length*.55f);candidate.y=-3.75f;
            if(!ArchipelagoBoatSafety.ClearWater(candidate,mainRotation,-4))continue;
            settings.boatSpawn=candidate;settings.boatRotation=mainRotation;settings.regions[0].dock=new Vector3(candidate.x,-4,candidate.z);found=true;break;
        }
        Require(found,"Boat berth has full-hull clearance beside the existing native city dock");
        Object.DestroyImmediate(native);Object.DestroyImmediate(accessPreview);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        report.AppendLine("MAIN frame="+mainFrame.ToString("F4")+" yaw="+mainRotation.eulerAngles.y+" berth="+settings.boatSpawn.ToString("F4"));
        File.WriteAllText(Out+"DockChecks.txt",report.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Verify();Debug.Log("NATIVE_DOCKS_BUILT play=false simulation=false");
    }
    static void MakeRamp(Transform parent,string title,Vector3 start,Vector3 end,float pathWidth=5f)
    {
        var delta=end-start;float distance=delta.magnitude,slope=Vector3.Angle(delta,Vector3.ProjectOnPlane(delta,Vector3.up));
        Require(slope<25,title+": preserved plateau landing and native dock joined by walkable ramp, slope="+slope);
        var frame=new GameObject(title+" · 靠岸栈桥",typeof(BoxCollider));frame.transform.SetParent(parent,false);frame.transform.SetPositionAndRotation((start+end)*.5f,Quaternion.LookRotation(delta));
        var floor=frame.GetComponent<BoxCollider>();floor.center=Vector3.down*.1f;floor.size=new Vector3(pathWidth,.2f,distance+.8f);
        int count=Mathf.CeilToInt(distance/2.8f);float step=distance/count;
        var b=Geometry(boardTemplate);
        for(int i=0;i<count;i++)
        {
            var section=new GameObject("Original wooden plank section");section.transform.SetParent(frame.transform,false);section.transform.localPosition=new Vector3(0,0,-distance*.5f+(i+.5f)*step);
            section.transform.localScale=new Vector3(pathWidth/b.size.x,1,(step+.025f)/b.size.z);
            var part=Object.Instantiate(boardTemplate,section.transform);part.transform.localPosition=boardTemplate.transform.position;part.transform.localRotation=boardTemplate.transform.rotation;part.transform.localScale=boardTemplate.transform.localScale;
        }
        for(float z=-distance*.5f;z<=distance*.5f;z+=4)foreach(float x in new[]{-pathWidth*.5f+.2f,pathWidth*.5f-.2f})
        {
            var post=Object.Instantiate(poleTemplate);post.transform.SetParent(frame.transform,true);
            post.transform.position=frame.transform.TransformPoint(new Vector3(x,-.17f,z))+poleTemplate.transform.position;
            post.transform.rotation=poleTemplate.transform.rotation;post.transform.localScale=poleTemplate.transform.localScale;
        }
    }
    public static void Verify()
    {
        Require(!EditorApplication.isPlaying,"Verification reads prefab geometry only");
        var settings=ArchipelagoSettings.Load();var dock=AssetDatabase.LoadAssetAtPath<GameObject>(Dock);var access=AssetDatabase.LoadAssetAtPath<GameObject>(Access);
        Require(dock&&dock.GetComponentsInChildren<MeshRenderer>(true).Length>20,"Native dock prefab retains the complete original pier geometry");
        Require(dock.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&dock.GetComponentsInChildren<Rigidbody>(true).Length==0,"Static pier contains no copied gameplay, NPC or network simulation");
        Require(access.transform.childCount==6,"Two native docks, two island paths and two water-entry ramps; restaurant-side temporary dock removed");
        var all=dock.GetComponentsInChildren<MeshRenderer>(true).Concat(access.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        Require(all.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported&&!m.shader.name.Contains("Error"))),"Dock and ramp reuse supported original native materials");
        Require(all.All(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh).StartsWith("Assets/Mesh/",StringComparison.Ordinal)),"Every visible pier and ramp mesh comes from the original sushi game assets");
        for(int i=1;i<3;i++)
        {
            var region=settings.regions[i];var instance=access.transform.Cast<Transform>().Single(t=>t.name.StartsWith(region.title,StringComparison.Ordinal)&&t.name.Contains("原城镇木码头"));
            Require(PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject)==dock,"Island "+i+" directly instantiates the shared original city dock prefab");
            var frame=access.transform.Cast<Transform>().Single(t=>t.name==region.title+" · 靠岸栈桥");
            Require(frame.GetComponent<BoxCollider>().size.x==5,"Island "+i+" keeps a five-metre continuous clear boarding path");
        }
        File.WriteAllText(Out+"DockChecks.txt",report.ToString());
    }
}
