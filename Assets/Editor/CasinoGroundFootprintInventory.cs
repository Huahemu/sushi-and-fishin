using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class CasinoGroundFootprintInventory
{
    [Serializable] public class Piece {public string path;public Vector3 min,max;public int lowVertices;}
    [Serializable] public class Script {public string asset,guid,type;public long fileID;public bool resolved;}
    [Serializable] public class Result {public string utc,error;public Vector3 lowMin,lowMax;public List<Piece> lowGeometry=new List<Piece>();public List<Script> scripts=new List<Script>();}
    static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
    public static void Run()
    {
        var result=new Result{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            const string folder="Assets/SushiAndFish/Casino/Imported/CasinoLab/Prefabs/";
            var root=PrefabUtility.LoadPrefabContents(folder+"CasinoLobbyAndExterior.prefab");bool any=false;Bounds bounds=default;
            try
            {
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh)continue;
                    var low=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).Where(v=>v.y>=-.5f&&v.y<=5).ToArray();
                    if(low.Length==0)continue;var box=new Bounds(low[0],Vector3.zero);foreach(var v in low)box.Encapsulate(v);
                    result.lowGeometry.Add(new Piece{path=P(filter.transform),min=box.min,max=box.max,lowVertices=low.Length});
                    if(!any){bounds=box;any=true;}else bounds.Encapsulate(box);
                }
                result.lowMin=bounds.min;result.lowMax=bounds.max;
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var matches=Regex.Matches(File.ReadAllText(folder+"CasinoBuilding.prefab"),@"m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32})");
            foreach(var pair in matches.Cast<Match>().Select(m=>new{guid=m.Groups[2].Value,id=long.Parse(m.Groups[1].Value)}).Distinct())
            {
                var path=AssetDatabase.GUIDToAssetPath(pair.guid);MonoScript script=null;
                foreach(var item in AssetDatabase.LoadAllAssetsAtPath(path).OfType<MonoScript>())if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item,out string _,out long id)&&id==pair.id){script=item;break;}
                result.scripts.Add(new Script{asset=path,guid=pair.guid,fileID=pair.id,type=script&&script.GetClass()!=null?script.GetClass().FullName:"",resolved=script&&script.GetClass()!=null});
            }
        }
        catch(Exception e){result.error=e.ToString();}
        File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"casino-low-geometry-and-script-inventory.json"),JsonUtility.ToJson(result,true));
        EditorApplication.Exit(string.IsNullOrEmpty(result.error)?0:1);
    }
}
