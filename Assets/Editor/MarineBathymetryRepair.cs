using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class IslandRevision20261018
{
    [Serializable] public class BathymetryTile {public string source,terrain,output;public bool recreated,drySamplesUnchanged;public int sourceSubmergedSamples,changedSamples,cappedRemovedLand;}
    [Serializable] public class MarineSupport {public string scene,path,support;public float originalGround,beforeGround,afterGround,bottom,beforeGap,afterGap;public bool sourceCovered,sourceGrounded,poseUnchanged;}
    [Serializable] public class BathymetryResult {public string utc,stage;public float seaLevel;public bool artificialCapDisabled,restaurantUnchanged,marinePosesUnchanged,dryLandUnchanged;public int originalGrounded,stillFloating;public BathymetryTile[] tiles;public MarineSupport[] marine;}
    static string BathymetryPose(Transform t)=>t.position.ToString("R")+t.rotation.ToString("R")+t.lossyScale.ToString("R");
    static bool SolidTerrainAt(TerrainData data,Vector3 position,Vector3 point) {
        float x=(point.x-position.x)/data.size.x,z=(point.z-position.z)/data.size.z;
        return x>=0&&x<=1&&z>=0&&z<=1&&!data.IsHole(Mathf.Clamp(Mathf.FloorToInt(x*data.holesResolution),0,data.holesResolution-1),Mathf.Clamp(Mathf.FloorToInt(z*data.holesResolution),0,data.holesResolution-1));
    }
    static float MarineFloor(Terrain[] lands,Transform prop,Bounds b,out string support) {
        float height=-100000;support="NoSolidTerrain";
        foreach(var land in lands)if(SolidTerrainAt(land.terrainData,land.transform.position,b.center)){height=Mathf.Max(height,land.SampleHeight(b.center)+land.transform.position.y);support="TerrainHeight";}
        foreach(var hit in Physics.RaycastAll(new Vector3(b.center.x,b.max.y+.5f,b.center.z),Vector3.down,1000,~(1<<4),QueryTriggerInteraction.Ignore)) {
            if(hit.collider.transform==prop||hit.collider.transform.IsChildOf(prop)||hit.normal.y<.5f||hit.point.y<=height)continue;
            if(!(hit.collider is TerrainCollider)&&!P(hit.collider.transform).StartsWith("---Ocean/Underwater Models"))continue;
            height=hit.point.y;support=P(hit.collider.transform);
        }
        return height;
    }
    public static void RestoreMarineBathymetry() {
        if(File.Exists(Path.Combine(Reports,"marine-bathymetry-repair.json")))throw new Exception("Repair already saved; inspect receipt before replay");
        var scenes=LoadAll();var game=scenes[0];var all=scenes.SelectMany(All).ToArray();Physics.SyncTransforms();
        var sources=JsonUtility.FromJson<SourceTerrainReport>(File.ReadAllText(Path.Combine(Reports,"island-trim-heights.json"))).terrains;
        var sourceData=sources.ToDictionary(x=>x.data,x=>AssetDatabase.LoadAssetAtPath<TerrainData>(x.data));
        float sea=RestaurantCoastSourceRepair.OriginalWaterLevel;
        var lands=All(game).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var props=new HashSet<Transform>(all.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.gameObject.scene.name,@"^[2-5]\d\d_")&&t.GetComponent<MeshFilter>()).Select(AtomicRoot));
        var poses=props.ToDictionary(t=>t,BathymetryPose);var marine=new List<MarineSupport>();
        foreach(var t in props) {
            var b=IslandComprehensiveRepair.BoundsOf(t);float original=OriginalTerrainAt(sources,sourceData,b.center,out bool covered);float before=MarineFloor(lands,t,b,out _);
            marine.Add(new MarineSupport{scene=t.gameObject.scene.name,path=P(t),bottom=b.min.y,originalGround=original,sourceCovered=covered,sourceGrounded=covered&&original<sea-.1f&&b.min.y-original>=-.8f&&b.min.y-original<=.3f,beforeGround=before,beforeGap=b.min.y-before});
        }
        var restaurant=All(game).Single(t=>P(t)=="---Restaurant/Restaurant/RestaurantStatic");string restaurantBefore=Signature(restaurant);
        var rows=new List<BathymetryTile>();const string folder="Assets/SushiAndFish/Modules/OriginalIsland/TerrainData/SourceBathymetry";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var reference=lands.First(t=>t.name!="IslandOuterSeabed");
        foreach(var source in sources) {
            var row=new BathymetryTile{source=source.data,terrain=source.path,drySamplesUnchanged=true};var terrain=lands.FirstOrDefault(t=>P(t.transform)==source.path);
            var original=sourceData[source.data];if(!original)throw new Exception("Missing original bathymetry: "+source.data);
            row.recreated=!terrain;var current=terrain?terrain.terrainData:original;
            row.output=folder+"/"+Path.GetFileNameWithoutExtension(source.data)+"_Restored.asset";
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(row.output))throw new Exception("Output exists: "+row.output);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(current),row.output);var data=AssetDatabase.LoadAssetAtPath<TerrainData>(row.output);
            int n=data.heightmapResolution;var old=current.GetHeights(0,0,n,n);var h=(float[,])old.Clone();var originalHeights=original.GetHeights(0,0,n,n);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {
                float oldY=old[z,x]*data.size.y+source.position.y,originalY=originalHeights[z,x]*data.size.y+source.position.y;
                if(!row.recreated&&oldY>=sea-.01f)continue;
                if(originalY<sea-.01f){h[z,x]=originalHeights[z,x];row.sourceSubmergedSamples++;}
                else if(row.recreated){h[z,x]=(sea-1-source.position.y)/data.size.y;row.cappedRemovedLand++;}
                if(h[z,x]!=old[z,x])row.changedSamples++;
            }
            data.SetHeights(0,0,h);data.treeInstances=Array.Empty<TreeInstance>();data.detailPrototypes=Array.Empty<DetailPrototype>();EditorUtility.SetDirty(data);
            if(!terrain) {
                var root=Terrain.CreateTerrainGameObject(data);root.name=source.path.Split('/').Last();var parentPath=source.path.Substring(0,source.path.LastIndexOf('/'));
                var parent=All(game).FirstOrDefault(t=>P(t)==parentPath)??All(game).Single(t=>P(t)=="---World/Terrain");SceneManager.MoveGameObjectToScene(root,game);root.transform.SetParent(parent);root.transform.position=source.position;root.layer=reference.gameObject.layer;terrain=root.GetComponent<Terrain>();terrain.materialTemplate=reference.materialTemplate;terrain.drawInstanced=reference.drawInstanced;
            } else {terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;}
            terrain.Flush();var after=data.GetHeights(0,0,n,n);
            if(!row.recreated)for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(old[z,x]*data.size.y+source.position.y>=sea-.01f&&after[z,x]!=old[z,x])row.drySamplesUnchanged=false;
            if(!row.drySamplesUnchanged)throw new Exception("Dry land changed: "+source.path);rows.Add(row);
        }
        var cap=All(game).FirstOrDefault(t=>t.name=="IslandOuterSeabed");if(cap)cap.gameObject.SetActive(false);
        lands=All(game).Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();foreach(var land in lands)land.allowAutoConnect=true;
        Physics.SyncTransforms();int index=0;
        foreach(var t in props) {var row=marine[index++];var b=IslandComprehensiveRepair.BoundsOf(t);row.afterGround=MarineFloor(lands,t,b,out row.support);row.afterGap=b.min.y-row.afterGround;row.poseUnchanged=poses[t]==BathymetryPose(t);}
        var result=new BathymetryResult{utc=DateTime.UtcNow.ToString("o"),stage="SAVED_NEEDS_RUNTIME_VERIFICATION",seaLevel=sea,artificialCapDisabled=!cap||!cap.gameObject.activeSelf,restaurantUnchanged=Signature(restaurant)==restaurantBefore,marinePosesUnchanged=marine.All(x=>x.poseUnchanged),dryLandUnchanged=rows.All(x=>x.drySamplesUnchanged),tiles=rows.ToArray(),marine=marine.ToArray(),originalGrounded=marine.Count(x=>x.sourceGrounded),stillFloating=marine.Count(x=>x.sourceGrounded&&x.afterGap>.5f)};
        if(!result.restaurantUnchanged||!result.marinePosesUnchanged||!result.dryLandUnchanged||result.stillFloating>0)throw new Exception("Marine restoration invariant failed: floating="+result.stillFloating);
        EditorSceneManager.SaveScene(game);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Reports,"marine-bathymetry-repair.json"),JsonUtility.ToJson(result,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("ORIGINAL_SUBMERGED_BATHYMETRY_RESTORED "+result.originalGrounded+" props grounded");
    }
}
