using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class HoleTile {public string terrain,source;public int sourceHoles,restoredHoles,preservedDryCells;public bool dryCellsUnchanged;}
    [Serializable] public class HoleResult {public string utc,stage;public bool dryLandUnchanged,marinePosesUnchanged;public int sourceGrounded,stillFloating,meshFloorSupports;public HoleTile[] tiles;public MarineSupport[] marine;}
    public static void RestoreMarineSourceHoles() {
        if(File.Exists(Path.Combine(Reports,"marine-source-holes-repair.json")))throw new Exception("Hole repair already completed");
        var scenes=LoadAll();var game=scenes[0];var all=scenes.SelectMany(All).ToArray();var lands=All(game).Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.GetComponent<Terrain>()).Where(t=>t).ToArray();
        var sources=JsonUtility.FromJson<SourceTerrainReport>(File.ReadAllText(Path.Combine(Reports,"island-trim-heights.json"))).terrains;var rows=new List<HoleTile>();float sea=RestaurantCoastSourceRepair.OriginalWaterLevel;
        foreach(var source in sources) {
            var land=lands.Single(t=>t.transform.position==source.position&&t.terrainData.size==source.size);var original=AssetDatabase.LoadAssetAtPath<TerrainData>(source.data);var data=land.terrainData;int n=data.holesResolution;
            var holes=data.GetHoles(0,0,n,n);var before=(bool[,])holes.Clone();var sourceHoles=original.GetHoles(0,0,n,n);var row=new HoleTile{terrain=P(land.transform),source=source.data,dryCellsUnchanged=true};
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {
                if(!sourceHoles[z,x])row.sourceHoles++;float nx=(x+.5f)/n,nz=(z+.5f)/n;
                float currentY=data.GetInterpolatedHeight(nx,nz)+source.position.y,originalY=original.GetInterpolatedHeight(nx,nz)+source.position.y;
                if(currentY>=sea-.01f){row.preservedDryCells++;continue;}
                if(originalY<sea-.01f&&holes[z,x]!=sourceHoles[z,x]){holes[z,x]=sourceHoles[z,x];row.restoredHoles++;}
            }
            data.SetHoles(0,0,holes);EditorUtility.SetDirty(data);land.Flush();var after=data.GetHoles(0,0,n,n);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(data.GetInterpolatedHeight((x+.5f)/n,(z+.5f)/n)+source.position.y>=sea-.01f&&after[z,x]!=before[z,x])row.dryCellsUnchanged=false;
            if(!row.dryCellsUnchanged)throw new Exception("Dry terrain holes changed");rows.Add(row);
        }
        Physics.SyncTransforms();var old=JsonUtility.FromJson<BathymetryResult>(File.ReadAllText(Path.Combine(Reports,"marine-bathymetry-repair.json")));var props=all.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.gameObject.scene.name,@"^[2-5]\d\d_")&&t.GetComponent<MeshFilter>()).Select(AtomicRoot).Distinct().GroupBy(t=>t.gameObject.scene.name+"|"+P(t)).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var row in old.marine) {
            var candidates=props[row.scene+"|"+row.path];var t=candidates.OrderBy(p=>Mathf.Abs(IslandComprehensiveRepair.BoundsOf(p).min.y-row.bottom)).First();candidates.Remove(t);var b=IslandComprehensiveRepair.BoundsOf(t);if(Mathf.Abs(b.min.y-row.bottom)>.001f)throw new Exception("Marine instance geometry changed: "+row.path);row.afterGround=MarineFloor(lands,t,b,out row.support);row.afterGap=b.min.y-row.afterGround;
            if(row.sourceGrounded&&!sources.Any(s=>SolidTerrainAt(AssetDatabase.LoadAssetAtPath<TerrainData>(s.data),s.position,b.center)))row.sourceGrounded=false;
        }
        var result=new HoleResult{utc=DateTime.UtcNow.ToString("o"),stage="SAVED_NEEDS_RUNTIME_VERIFICATION",dryLandUnchanged=rows.All(x=>x.dryCellsUnchanged),marinePosesUnchanged=old.marinePosesUnchanged,sourceGrounded=old.marine.Count(x=>x.sourceGrounded),stillFloating=old.marine.Count(x=>x.sourceGrounded&&x.afterGap>.5f),meshFloorSupports=old.marine.Count(x=>x.support.StartsWith("---Ocean/Underwater Models")),tiles=rows.ToArray(),marine=old.marine};
        if(result.stillFloating>0)throw new Exception("Source-grounded prop no longer grounded: "+string.Join(";",old.marine.Where(x=>x.sourceGrounded&&x.afterGap>.5f).Select(x=>x.path)));
        EditorSceneManager.SaveScene(game);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Reports,"marine-source-holes-repair.json"),JsonUtility.ToJson(result,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("SOURCE_UNDERWATER_HOLES_RESTORED "+rows.Sum(x=>x.restoredHoles));
    }
}
