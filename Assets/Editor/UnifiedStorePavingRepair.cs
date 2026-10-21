using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class PavingScaleChange {public string path;public Vector3 before,after;}
    [Serializable] public class StorePavingRepairReceipt {public string utc,sourceGeometrySha256;public bool saved;public PavingScaleChange[] normalizedPavingScales;}
    public static void RepairUnifiedStorePaving() {
        string flag=Path.Combine(Reports,"town-unified-store-paving-repair.json");if(File.Exists(flag))throw new Exception("Paving repair already saved; inspect receipt");
        var data=JsonUtility.FromJson<UnifiedGeometry>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-geometry.json")));var build=JsonUtility.FromJson<UnifiedRoadReceipt>(File.ReadAllText(Path.Combine(Reports,"town-unified-road-build.json")));
        var scenes=LoadIsland();var all=scenes.SelectMany(All).ToArray();IndexRevisionObjects(all);var root=all.Single(t=>t.name=="TownRoadNetwork20261019");var lands=all.Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();var baseline=JsonUtility.FromJson<PlanningInventory>(File.ReadAllText(Path.Combine(Reports,"town-unified-inventory.json")));
        foreach(var p in baseline.protectedNodes){var t=Resolve<Transform>(p.globalId);if(!t||Signature(t)!=p.signature)throw new Exception("Repair baseline drift "+p.path);}
        var allowed=new HashSet<string>(data.housePoses.Select(s=>s.path).Concat(data.housePoses.SelectMany(s=>s.replacedPavingPaths)).Concat(build.gameplayMoved.Select(s=>s.path)));
        foreach(var store in data.housePoses){var old=build.stores.Single(s=>s.path==store.path);var delta=store.position-old.position;var target=all.Single(t=>P(t)==store.path);if(Vector3.Distance(target.position,old.position)>.001f)throw new Exception("Store already moved outside repair");target.position=store.position;string name=target.name;foreach(var point in build.gameplayMoved.Where(p=>p.store==name)){var t=all.Single(v=>P(v)==point.path);if(Vector3.Distance(t.position,point.position)>.001f)throw new Exception("Gameplay point drift "+point.path);point.position+=delta;t.position=point.position;}}
        var normalized=new List<PavingScaleChange>();foreach(var surface in data.surfaces.Where(s=>!string.IsNullOrEmpty(s.nativeOwner))){var owner=all.Single(t=>P(t)==surface.nativeOwner);var scale=owner.localScale;if(scale.x<0||scale.y<0||scale.z<0){if(owner.childCount!=0)throw new Exception("Cannot normalize a paving node with children");var positive=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));owner.localScale=positive;if(owner.localToWorldMatrix.determinant<0)throw new Exception("Reflected paving parent");normalized.Add(new PavingScaleChange{path=P(owner),before=scale,after=positive});}}
        foreach(var surface in data.surfaces){var owner=SurfaceOwner(surface,root,all);CopyUpdatedMesh(owner,UnifiedMesh(surface,owner,data,null));}
        var curbs=new RoadGeometry();foreach(var ring in data.curbInnerRings)for(int i=0;i<ring.points.Length-1;i++){var a=ring.points[i];var b=ring.points[i+1];a.y=b.y=data.curbTopY;curbs.Wall(a,b,data.asphaltY,data.asphaltY);}var kerbOwner=root.Find("ContinuousSurfaces/Unified_CurbFaces");CopyUpdatedMesh(kerbOwner,WallMesh(curbs,kerbOwner,"Unified_CurbFaces"));
        var walls=new RoadGeometry();foreach(var ring in data.outerPavingRings)for(int i=0;i<ring.points.Length-1;i++){var a=ring.points[i];var b=ring.points[i+1];int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.5f));for(int j=0;j<n;j++){var x=Vector3.Lerp(a,b,(float)j/n);var z=Vector3.Lerp(a,b,(float)(j+1)/n);x.y=z.y=data.curbTopY;walls.Wall(x,z,PlanningGround(lands,x.x,x.z)-.025f,PlanningGround(lands,z.x,z.z)-.025f);}}var wallOwner=root.Find("ContinuousSurfaces/Unified_OuterRoadbed");CopyUpdatedMesh(wallOwner,WallMesh(walls,wallOwner,"Unified_OuterRoadbed"));
        foreach(var p in baseline.protectedNodes){var t=Resolve<Transform>(p.globalId);var value=Signature(t);if(value==p.signature)continue;if(!allowed.Contains(p.path))throw new Exception("Unrelated object changed "+p.path);p.signature=value;var change=build.intentionalChanges.FirstOrDefault(c=>c.globalId==p.globalId);if(change!=null)change.after=value;}
        if(!build.terrains.All(before=>SameTerrain(before,FullTerrain(lands.Single(t=>P(t.transform)==before.path)))))throw new Exception("Terrain changed during paving repair");
        build.stores=data.housePoses;build.plan=data.plan;build.utc=DateTime.UtcNow.ToString("o");build.stage="STORE_CLEARANCE_AND_NATIVE_PHYSICS_REPAIRED_NEEDS_RUNTIME";
        foreach(var store in data.housePoses){var t=all.Single(v=>P(v)==store.path);PrefabUtility.SaveAsPrefabAsset(t.gameObject,build.assets+"/Prefabs/"+t.name+"_JoinedForecourt.prefab");}foreach(var scene in scenes)EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Reports,"town-unified-inventory.json"),JsonUtility.ToJson(baseline,true));File.WriteAllText(Path.Combine(Reports,"town-unified-road-build.json"),JsonUtility.ToJson(build,true));
        revisionObjects=null;revisionIds=null;RebuildRevisionNavigation();IslandFrontEndBuilder.RefreshMenuPresentation();File.WriteAllText(flag,JsonUtility.ToJson(new StorePavingRepairReceipt{utc=DateTime.UtcNow.ToString("o"),saved=true,sourceGeometrySha256=BytesHash(File.ReadAllBytes(Path.Combine(Reports,"town-unified-road-geometry.json"))),normalizedPavingScales=normalized.ToArray()},true));VerifyUnifiedRoadTownSaved();DiagnoseUnifiedPavingPhysics();Debug.Log("UNIFIED_NATIVE_PAVING_REPAIRED normalized="+normalized.Count);
    }
}
