using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class OriginalHeightRow {public string data,heights;}
    [Serializable] public class OriginalHeightRows {public OriginalHeightRow[] terrains;}
    [Serializable] public class TownSavedResult {public string utc,stage;public bool passed,originalHeightSnapshotsMatch,marinePositionsUnchanged;public int protectedNodes,mainRoads,npcFurniture,removedPresent,marineProps,meshSupports,noBelowFloorRayHit,sourceGrounded,sourceGroundedFloating,restoredOriginalHoles;public string[] protectionFailures,sourceHeightFailures;public MarineSupport[] marine;}
    public static void VerifyTownSaved() {
        PlayerSettings.productName="Deep Blue Sushi";
        var scenes=LoadAll();var all=scenes.SelectMany(All).ToArray();revisionObjects=null;revisionIds=null;IndexRevisionObjects(all);
        var plan=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-module-export.json")));
        var result=new TownSavedResult{utc=DateTime.UtcNow.ToString("o"),stage="REOPENED_SAVED_ASSETS",protectedNodes=plan.protectedNodes.Length};
        result.protectionFailures=plan.protectedNodes.Where(p=>{var t=Resolve<Transform>(p.globalId);return !t||Signature(t)!=p.signature;}).Select(p=>p.path).ToArray();
        result.mainRoads=plan.objects.Count(r=>r.action=="KEEP_MAIN_ROAD"&&Resolve<Transform>(r.id));result.npcFurniture=plan.objects.Count(r=>r.action=="KEEP_NPC_FURNITURE"&&Resolve<Transform>(r.id));result.removedPresent=plan.objects.Count(r=>r.action=="REMOVE_AFTER_EXPORT"&&Resolve<Transform>(r.id));
        var sourceRows=JsonUtility.FromJson<OriginalHeightRows>(File.ReadAllText(Path.Combine(Reports,"island-trim-heights.json"))).terrains;var failed=new List<string>();
        foreach(var source in sourceRows) {var data=AssetDatabase.LoadAssetAtPath<TerrainData>(source.data);var h=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);var bytes=new byte[h.Length*4];Buffer.BlockCopy(h,0,bytes,0,bytes.Length);if(Convert.ToBase64String(bytes)!=source.heights)failed.Add(source.data);}
        result.sourceHeightFailures=failed.ToArray();result.originalHeightSnapshotsMatch=failed.Count==0;
        var sources=JsonUtility.FromJson<SourceTerrainReport>(File.ReadAllText(Path.Combine(Reports,"island-trim-heights.json"))).terrains;var dataByPath=sources.ToDictionary(s=>s.data,s=>AssetDatabase.LoadAssetAtPath<TerrainData>(s.data));
        var lands=all.Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();Physics.SyncTransforms();
        var props=all.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.gameObject.scene.name,@"^[2-5]\d\d_")&&t.GetComponent<MeshFilter>()).Select(AtomicRoot).Distinct().ToArray();
        var baselines=JsonUtility.FromJson<TownPlan>(File.ReadAllText(Path.Combine(Reports,"town-simplification-plan.json"))).marine.GroupBy(r=>r.scene+"|"+r.path).ToDictionary(g=>g.Key,g=>g.ToList());var support=new List<MarineSupport>();result.marinePositionsUnchanged=true;
        foreach(var prop in props) {
            var matches=baselines[prop.gameObject.scene.name+"|"+P(prop)];var row=matches.OrderBy(r=>Vector3.Distance(r.position,prop.position)).First();matches.Remove(row);
            bool unchanged=Vector3.Distance(row.position,prop.position)<.0001f;if(!unchanged)result.marinePositionsUnchanged=false;
            var b=IslandComprehensiveRepair.BoundsOf(prop);float floor=MarineFloor(lands,prop,b,out string name);float original=OriginalTerrainAt(sources,dataByPath,b.center,out bool covered);bool solid=sources.Any(s=>SolidTerrainAt(dataByPath[s.data],s.position,b.center));
            support.Add(new MarineSupport{scene=prop.gameObject.scene.name,path=P(prop),support=name,bottom=b.min.y,originalGround=original,sourceCovered=covered,sourceGrounded=solid&&original<-4.1f&&b.min.y-original>=-.8f&&b.min.y-original<=.3f,afterGround=floor,afterGap=b.min.y-floor,poseUnchanged=unchanged});
        }
        result.marine=support.ToArray();result.marineProps=props.Length;result.meshSupports=support.Count(x=>x.support.StartsWith("---Ocean/Underwater Models"));result.noBelowFloorRayHit=support.Count(x=>x.afterGround<-999);result.sourceGrounded=support.Count(x=>x.sourceGrounded);result.sourceGroundedFloating=support.Count(x=>x.sourceGrounded&&x.afterGap>.5f);
        result.restoredOriginalHoles=JsonUtility.FromJson<HoleResult>(File.ReadAllText(Path.Combine(Reports,"marine-source-holes-repair.json"))).tiles.Sum(x=>x.restoredHoles);
        result.passed=result.protectionFailures.Length==0&&result.originalHeightSnapshotsMatch&&result.marinePositionsUnchanged&&result.marineProps==2493&&result.mainRoads==9&&result.npcFurniture==24&&result.removedPresent==0&&result.sourceGroundedFloating==0;
        File.WriteAllText(Path.Combine(Reports,"town-simplification-saved-verification.json"),JsonUtility.ToJson(result,true));revisionObjects=null;revisionIds=null;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");if(!result.passed)throw new Exception("Saved town verification failed");IslandFrontEndBuilder.RefreshMenuPresentation();AssetDatabase.SaveAssets();Debug.Log("TOWN_SAVED_VERIFICATION_PASSED "+result.protectedNodes);
    }
}
