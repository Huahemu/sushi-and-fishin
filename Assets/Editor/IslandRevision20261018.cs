using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bounded editor operations for chat 01a13e18-e061-7930-8a81-af528c1d37d5.
[InitializeOnLoad]
public static partial class IslandRevision20261018
{
    public static string Reports => IslandFrontEndInventory.Reports;
    static double next;
    static IslandRevision20261018() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 2;
        File.WriteAllText(Path.Combine(Reports, "island-revision-editor-state.json"), JsonUtility.ToJson(new EditorState {
            utc=DateTime.UtcNow.ToString("o"), revision="20261018-v5", playing=EditorApplication.isPlaying,
            scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).path).ToArray()
        }, true));
        var request=Path.Combine(Reports,"island-revision.request");
        if(!File.Exists(request)) return;
        var command=File.ReadAllText(request).Trim();
        File.Delete(request);
        try {
            if(command=="stop") EditorApplication.ExitPlaymode();
            else if(command=="runtime-state") RuntimeState();
            else if(command=="inventory" && !EditorApplication.isPlayingOrWillChangePlaymode) Inventory();
            else if(command=="reflect") ReflectWater();
            else if(command=="plan" && !EditorApplication.isPlayingOrWillChangePlaymode) BuildDecorationPlan();
            else if(command=="export" && !EditorApplication.isPlayingOrWillChangePlaymode) ExportDecorations();
            else if(command=="terrain-export" && !EditorApplication.isPlayingOrWillChangePlaymode) ExportTerrainDetails();
            else if(command=="apply" && !EditorApplication.isPlayingOrWillChangePlaymode) ApplyRevision();
            else if(command=="verify-saved" && !EditorApplication.isPlayingOrWillChangePlaymode) VerifySaved();
            else if(command=="residual-plan" && !EditorApplication.isPlayingOrWillChangePlaymode) PlanResiduals();
            else if(command=="residual-export" && !EditorApplication.isPlayingOrWillChangePlaymode) ExportResiduals();
            else if(command=="residual-apply" && !EditorApplication.isPlayingOrWillChangePlaymode) ApplyResiduals();
            else if(command=="revision-navigation" && !EditorApplication.isPlayingOrWillChangePlaymode) RebuildRevisionNavigation();
            else if(command=="restaurant-alignment-play" && !EditorApplication.isPlayingOrWillChangePlaymode) RestaurantAlignmentPlayAudit.Start();
            else if(command=="restaurant-alignment-after" && !EditorApplication.isPlayingOrWillChangePlaymode) RestaurantAlignmentDiagnosis.After();
            else if(command=="town-inventory" && !EditorApplication.isPlayingOrWillChangePlaymode) TownInventory();
            else if(command=="town-export" && !EditorApplication.isPlayingOrWillChangePlaymode) TownExport();
            else if(command=="town-clear" && !EditorApplication.isPlayingOrWillChangePlaymode) TownClear();
            else if(command=="unified-store-paving-repair" && !EditorApplication.isPlayingOrWillChangePlaymode) RepairUnifiedStorePaving();
            else if(command=="unified-paving-diagnostic" && !EditorApplication.isPlayingOrWillChangePlaymode) DiagnoseUnifiedPavingPhysics();
            else if(command=="source-road-study" && !EditorApplication.isPlayingOrWillChangePlaymode) StartNativeRoadVisualStudy();
            else if(command=="town-play" && !EditorApplication.isPlayingOrWillChangePlaymode) TownSimplificationPlayAudit.Start();
            else if(command=="play" && !EditorApplication.isPlayingOrWillChangePlaymode) IslandRevisionPlayAudit.Start();
            else throw new InvalidOperationException("Unavailable bounded revision command: "+command);
        } catch(Exception e) { File.WriteAllText(Path.Combine(Reports,"island-revision-command-error.txt"),e.ToString()); Debug.LogException(e); }
    }
    [Serializable] class EditorState {public string utc,revision;public bool playing;public string[] scenes;}
    [Serializable] public class Row {
        public string scene,path,name,components,mesh,materials,json;
        public int id,parent; public bool active;
        public Vector3 position,scale,min,max;public Quaternion rotation;
    }
    [Serializable] public class InventoryReport {public string utc;public Row[] objects;}
    static string P(Transform t)=>IslandFrontEndInventory.PathOf(t);
    public static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    static string MaterialInfo(Renderer r)=>r?string.Join(";",r.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m)+"|"+m.name+"|"+(m.shader?m.shader.name:"NULL")+"|supported="+(m.shader&&m.shader.isSupported)+"|passes="+m.passCount:"NULL")):"";
    static Row Describe(Transform t,bool detailed=false) {
        var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
        var b=f&&f.sharedMesh?IslandComprehensiveRepair.BoundsOf(t):new Bounds(t.position,Vector3.zero);
        return new Row{scene=t.gameObject.scene.path,path=P(t),name=t.name,id=t.GetInstanceID(),parent=t.parent?t.parent.GetInstanceID():0,active=t.gameObject.activeSelf,position=t.position,scale=t.lossyScale,rotation=t.rotation,min=b.min,max=b.max,mesh=f&&f.sharedMesh?AssetDatabase.GetAssetPath(f.sharedMesh):"",materials=MaterialInfo(r),components=string.Join(";",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().FullName)),json=detailed?string.Join("\n",t.GetComponents<MonoBehaviour>().Where(c=>c).Select(c=>c.GetType().FullName+"="+EditorJsonUtility.ToJson(c))):""};
    }
    public static void RuntimeState() {
        var all=Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>All(SceneManager.GetSceneAt(i))).ToArray();
        var rows=all.Where(t=>t.name.IndexOf("water",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("droplet",StringComparison.OrdinalIgnoreCase)>=0||t.GetComponent<Camera>()||t.GetComponents<MonoBehaviour>().Any(m=>m&&(m.GetType().Name.Contains("Resident")||m.GetType().Namespace=="StylizedWater3"))).Select(t=>Describe(t,true)).ToArray();
        File.WriteAllText(Path.Combine(Reports,"island-revision-runtime-state.json"),JsonUtility.ToJson(new InventoryReport{utc=DateTime.UtcNow.ToString("o"),objects=rows},true));
    }
    public static Scene[] LoadAll() {
        var list=new List<Scene>{EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene,OpenSceneMode.Single)};
        foreach(var p in Directory.GetFiles(IslandFrontEndBuilder.DressingRoot,"*.unity").OrderBy(p=>p))list.Add(EditorSceneManager.OpenScene(p,OpenSceneMode.Additive));
        Physics.SyncTransforms();return list.ToArray();
    }
    public static void Inventory() {
        var scenes=LoadAll();
        var rows=scenes.SelectMany(All).Select(t=>Describe(t,t.GetComponents<MonoBehaviour>().Any(m=>m&&m.GetType().Name!="DressingSceneGroup"))).ToArray();
        File.WriteAllText(Path.Combine(Reports,"island-revision-inventory.json"),JsonUtility.ToJson(new InventoryReport{utc=DateTime.UtcNow.ToString("o"),objects=rows},true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
        Debug.Log("ISLAND_REVISION_INVENTORY_READY "+rows.Length);
    }
    public static void ReflectWater() {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var types=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.Contains("stylizedwater")).SelectMany(a=>a.GetTypes()).Where(t=>t.FullName.Contains("Waterline")||t.FullName.Contains("Height")||t.FullName.Contains("Underwater")||t.FullName.Contains("Shader")).ToArray();
        File.WriteAllText(Path.Combine(Reports,"island-revision-water-api.txt"),string.Join("\n\n",types.Select(t=>t.FullName+"\n"+string.Join("\n",t.GetFields(flags).Select(f=>" FIELD "+f.FieldType+" "+f.Name))+"\n"+string.Join("\n",t.GetMethods(flags).Select(m=>" METHOD "+m)))));
    }
}
