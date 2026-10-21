using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using SushiAndFish.Casino;using SushiAndFish.Casino.Original;using Object=UnityEngine.Object;
public static class CasinoDetailedDiagnosis {
 [Serializable]public class Record{public string path,type,mesh,materials;public Vector3 pos,scale,min,max;public bool active,enabled,trigger;public int submesh;}
 [Serializable]public class Receipt{public string utc,error;public List<Record> exterior=new List<Record>(),native=new List<Record>(),world=new List<Record>();}
 static string PathOf(Transform t){var s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
 static void Add(GameObject root,List<Record> list){
  foreach(var c in root.GetComponentsInChildren<Collider>(true)){var b=c.bounds;list.Add(new Record{path=PathOf(c.transform),type=c.GetType().Name,pos=c.transform.position,scale=c.transform.lossyScale,min=b.min,max=b.max,active=c.gameObject.activeInHierarchy,enabled=c.enabled,trigger=c.isTrigger});}
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)){var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var m=f.sharedMesh;var mats=r.sharedMaterials;
   for(int i=0;i<m.subMeshCount;i++){var b=m.GetSubMesh(i).bounds;var w=new Bounds(r.transform.TransformPoint(b.center),Vector3.zero);for(int k=0;k<8;k++)w.Encapsulate(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));list.Add(new Record{path=PathOf(r.transform),type="MeshRenderer",pos=r.transform.position,scale=r.transform.lossyScale,min=w.min,max=w.max,active=r.gameObject.activeInHierarchy,enabled=r.enabled,submesh=i,mesh=m.name,materials=i<mats.Length&&mats[i]?mats[i].name:""});}
  }
 }
 public static void Inspect(){var q=new Receipt{utc=DateTime.UtcNow.ToString("o")};try{
  var s=EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");var a=Object.FindFirstObjectByType<ParkCasinoIntegration>();Add(a.permanentExterior,q.exterior);
  foreach(var r in s.GetRootGameObjects())if(r!=a.permanentExterior){foreach(var c in r.GetComponentsInChildren<Collider>(true)){var b=c.bounds;if(c.enabled&&!c.isTrigger&&b.size.x>20&&b.size.z>20&&b.max.y>0&&b.min.y<10)q.world.Add(new Record{path=PathOf(c.transform),type=c.GetType().Name,min=b.min,max=b.max,active=c.gameObject.activeInHierarchy,enabled=c.enabled});}if(r.name.IndexOf("Road",StringComparison.OrdinalIgnoreCase)>=0)Add(r,q.world);}
  EditorSceneManager.OpenScene("Assets/SushiAndFish/Casino/Imported/Scenes/CasinoScene.unity");foreach(var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())Add(r,q.native);
 }catch(Exception e){q.error=e.ToString();}finally{File.WriteAllText(System.IO.Path.Combine(IslandRevision20261018.Reports,"casino-detailed-static-diagnosis.json"),JsonUtility.ToJson(q,true));EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}}
}
