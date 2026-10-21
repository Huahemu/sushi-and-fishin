using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class IslandRevision20261018
{
    [Serializable] public class TownRoadTerrainGrid {public string utc;public float xMin=-142,zMin=-84,step=.5f;public int width=509,height=217;public float[] ground,grass;}
    public static void ExportTownRoadTerrainGrid() {
        var scenes=LoadIsland();var lands=scenes.SelectMany(All).Select(t=>t.GetComponent<Terrain>()).Where(t=>t&&t.gameObject.activeInHierarchy).ToArray();var alpha=lands.ToDictionary(t=>t,t=>t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapWidth,t.terrainData.alphamapHeight));var result=new TownRoadTerrainGrid{utc=DateTime.UtcNow.ToString("o")};result.ground=new float[result.width*result.height];result.grass=new float[result.ground.Length];
        for(int iz=0;iz<result.height;iz++)for(int ix=0;ix<result.width;ix++){float x=result.xMin+ix*result.step,z=result.zMin+iz*result.step;var land=lands.FirstOrDefault(t=>x>=t.transform.position.x&&z>=t.transform.position.z&&x<=t.transform.position.x+t.terrainData.size.x&&z<=t.transform.position.z+t.terrainData.size.z);int index=iz*result.width+ix;if(!land){result.ground[index]=-999;continue;}var d=land.terrainData;int layer=Array.FindIndex(d.terrainLayers,l=>l&&l.name=="SWS_Grass");int ax=Mathf.Clamp((int)((x-land.transform.position.x)/d.size.x*d.alphamapWidth),0,d.alphamapWidth-1),az=Mathf.Clamp((int)((z-land.transform.position.z)/d.size.z*d.alphamapHeight),0,d.alphamapHeight-1);result.ground[index]=land.SampleHeight(new Vector3(x,0,z))+land.transform.position.y;result.grass[index]=layer>=0?alpha[land][az,ax,layer]:0;}
        File.WriteAllText(Path.Combine(Reports,"town-road-terrain-grid.json"),JsonUtility.ToJson(result,false));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");Debug.Log("TOWN_TERRAIN_GRID "+result.ground.Length);
    }
}
