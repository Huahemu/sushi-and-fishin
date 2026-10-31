#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Enums;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Open GameState.unity with 100_Dressing_City_01.unity loaded
// additively (activate its dressing groups), then run the menu items.
// 1) Audit: lists every scene EmoteArea whose player target has no furniture
//    renderer under it (floating E points / lying on grass).
// 2) Disable orphans: deactivates those EmoteArea GameObjects (scene edit).
// 3) Bind seats: adds a native EmoteArea on every SeasideBench / ParkBench
//    anchor and on every Sunbed_* lounger so all of them are interactable and
//    share occupancy with the NPC activity (OriginalNpcActivityPort.AreaFor).
public static class EmoteAreaBindingTool
{
    const string EmoteAreaPrefab="Assets/GameObject/EmoteArea.prefab";
    const float SupportRadius=.45f;

    static SerializedObject So(EmoteArea a)=>new SerializedObject(a);
    static Vector3 Target(EmoteArea a)=>So(a).FindProperty("_targetPos").vector3Value;

    static bool IsOwn(Renderer r,EmoteArea a)=>r.transform.IsChildOf(a.transform);
    static bool Supported(Vector3 target,EmoteArea self,Renderer[] renderers,out string by)
    {
        by=null;
        foreach(var r in renderers)
        {
            if(!r||!r.enabled||self&&IsOwn(r,self)||r is ParticleSystemRenderer||r is TrailRenderer||r is LineRenderer)continue;
            if(r.GetComponentInParent<Terrain>())continue;
            var b=r.bounds;if(b.size.y>6f||b.size.x>12f||b.size.z>12f)continue; // ground/building shells are not furniture
            var probe=target+Vector3.down*.25f;
            if(b.SqrDistance(probe)<=SupportRadius*SupportRadius){by=r.name;return true;}
        }
        return false;
    }

    static List<EmoteArea> SceneAreas()=>Object.FindObjectsByType<EmoteArea>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(a=>a.gameObject.scene.IsValid()&&!EditorUtility.IsPersistent(a)).ToList();

    [MenuItem("Tools/Deep Blue/EmoteArea - Audit furniture binding")]
    static void Audit()=>Run(false);
    [MenuItem("Tools/Deep Blue/EmoteArea - Disable orphan (floating) areas")]
    static void DisableOrphans()=>Run(true);

    static void Run(bool disable)
    {
        var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
        var log=new StringBuilder("[EmoteAreaBinding] audit\n");int orphan=0;
        foreach(var a in SceneAreas().Where(a=>a.gameObject.activeInHierarchy))
        {
            if(a.GetComponent<SushiAndFish.CityExpansion.CityDiningSeat>())continue; // dining/cinema seats are authored with their furniture
            var t=Target(a);bool ok=Supported(t,a,renderers,out var by);
            log.AppendLine($"{(ok?"BOUND ":"ORPHAN")} {Path(a.transform)} target={t} {(ok?"on "+by:"")}");
            if(!ok){orphan++;if(disable){Undo.RecordObject(a.gameObject,"Disable orphan EmoteArea");a.gameObject.SetActive(false);EditorSceneManager.MarkSceneDirty(a.gameObject.scene);}}
        }
        log.AppendLine($"orphans={orphan}{(disable?" (deactivated; save GameState.unity)":"")}");Debug.Log(log.ToString());
    }

    [MenuItem("Tools/Deep Blue/EmoteArea - Bind all benches and sunbeds")]
    static void BindSeats()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(EmoteAreaPrefab);if(!prefab){Debug.LogError("Missing "+EmoteAreaPrefab);return;}
        var areas=SceneAreas();var reference=areas.FirstOrDefault(a=>a.name=="EmoteArea (1)");
        var game=reference?reference.gameObject.scene:EditorSceneManager.GetActiveScene();
        var parent=reference?reference.transform.parent:null;int added=0;
        bool Taken(Vector3 p)=>areas.Any(a=>{var d=Target(a)-p;return Mathf.Abs(d.y)<.6f&&new Vector2(d.x,d.z).magnitude<SupportRadius;});
        void Add(string name,StaticEmotes emote,Vector3 target,Vector3 euler,Vector3 position)
        {
            if(Taken(target))return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,game);go.name=name;
            if(parent)go.transform.SetParent(parent,true);go.transform.position=position;
            var area=go.GetComponent<EmoteArea>();var so=So(area);
            so.FindProperty("_emote").enumValueIndex=(int)emote;so.FindProperty("_targetPos").vector3Value=target;so.FindProperty("_targetRot").vector3Value=euler;
            so.ApplyModifiedPropertiesWithoutUndo();Undo.RegisterCreatedObjectUndo(go,"Bind seat EmoteArea");areas.Add(area);added++;
        }
        // Benches: the NPC anchors are authored on the seat, so player and NPC share the exact pose.
        foreach(var anchor in Object.FindObjectsByType<WaypointAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(anchor.Category==ResidentActivityType.SeasideBench||anchor.Category==ResidentActivityType.ParkBench)
                Add("EmoteArea_"+anchor.Category+"_"+anchor.Index,StaticEmotes.Sit,anchor.transform.position,new Vector3(0,anchor.transform.eulerAngles.y,0),anchor.transform.position+Vector3.up*.7f);
        // Sunbeds: replicate the authored EmoteArea (1) <-> Sunbed_04 (2) offset on every lounger.
        var refBed=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Sunbed_04 (2)");
        if(reference&&refBed)
        {
            var refSo=So(reference);var refTarget=refSo.FindProperty("_targetPos").vector3Value;var refRot=refSo.FindProperty("_targetRot").vector3Value;
            var toLocal=Quaternion.Inverse(Quaternion.Euler(0,refBed.eulerAngles.y,0));
            var targetOffset=toLocal*(refTarget-refBed.position);var objectOffset=toLocal*(reference.transform.position-refBed.position);
            foreach(var bed in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Sunbed_")&&t.GetComponentInChildren<Renderer>(true)))
            {
                var yaw=Quaternion.Euler(0,bed.eulerAngles.y,0);float dy=bed.eulerAngles.y-refBed.eulerAngles.y;
                Add("EmoteArea_"+bed.name,StaticEmotes.Lay,bed.position+yaw*targetOffset,new Vector3(refRot.x,refRot.y+dy,refRot.z),bed.position+yaw*objectOffset);
            }
        }
        else Debug.LogWarning("[EmoteAreaBinding] Load 100_Dressing_City_01 additively so Sunbed_* are found.");
        if(added>0)EditorSceneManager.MarkSceneDirty(game);
        // Report sunbathe anchors that no lounger area now covers (move them onto a lounger).
        foreach(var anchor in Object.FindObjectsByType<WaypointAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(a=>a.Category==ResidentActivityType.BeachSunbathe))
            if(!Taken(anchor.transform.position))Debug.LogWarning($"[EmoteAreaBinding] BeachSunbathe {anchor.Index} at {anchor.transform.position} has no lounger EmoteArea: move it onto a free Sunbed target.");
        Debug.Log($"[EmoteAreaBinding] added {added} EmoteAreas to {game.name}; save the scene.");
    }

    static string Path(Transform t){var s=t.name;for(var p=t.parent;p;p=p.parent)s=p.name+"/"+s;return s;}
}
#endif
