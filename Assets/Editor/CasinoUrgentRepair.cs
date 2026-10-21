using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using SushiAndFish.Casino;
public static class CasinoUrgentRepair{
 [Serializable]public class Receipt{public string utc,error;public Vector3 origin,front;public float yaw;public int disabledRoads,disabledPlanes,disabledLights;public List<string> roads=new List<string>();public bool saved;}
 public static void Apply(){var q=new Receipt{utc=DateTime.UtcNow.ToString("o")};try{
 var s=EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
 var a=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ParkCasinoIntegration>(true)).Single();
 a.parkOrigin=new Vector3(-66,3.36f,-27.5f);a.parkYaw=-90;
 var e=a.permanentExterior;e.transform.SetPositionAndRotation(a.parkOrigin,Quaternion.Euler(0,a.parkYaw,0));
 foreach(var t in e.GetComponentsInChildren<Transform>(true)){var n=t.name.ToLowerInvariant();if(n.Contains("road")||n.Contains("asphalt")||n=="street"){q.roads.Add(t.name);q.disabledRoads++;}}
 foreach(var c in e.GetComponentsInChildren<Collider>(true))if(c.name=="Ground"&&c.transform.parent&&c.transform.parent.name=="Colliders")q.disabledPlanes++;
 q.disabledLights=e.GetComponentsInChildren<Light>(true).Length;CasinoHostRepair.TrimExterior(e);
 foreach(var rr in e.GetComponentsInChildren<MeshRenderer>(true))if(rr.sharedMaterials.Any(m=>m&&m.name.ToLowerInvariant().Contains("road"))){q.roads.Add(rr.name+"/Road material");q.disabledRoads++;}
 // The permanent exterior is visual only; runtime geometry owns all collision.
 foreach(var c in e.GetComponentsInChildren<Collider>(true))c.enabled=false;
 var old=s.GetRootGameObjects().FirstOrDefault(g=>g.name=="CasinoFrontRoadOnly");if(old)UnityEngine.Object.DestroyImmediate(old);
 var road=GameObject.CreatePrimitive(PrimitiveType.Cube);road.name="CasinoFrontRoadOnly";road.transform.position=new Vector3(-60.5f,2.975f,-27.5f);road.transform.localScale=new Vector3(21,.1f,6);
 var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="CasinoFrontRoadMaterial";mat.color=new Color(.18f,.19f,.20f);string mp="Assets/SushiAndFish/Casino/CasinoFrontRoadMaterial.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(mp);if(existing){UnityEngine.Object.DestroyImmediate(mat);mat=existing;}else AssetDatabase.CreateAsset(mat,mp);road.GetComponent<Renderer>().sharedMaterial=mat;
 q.origin=a.parkOrigin;q.yaw=a.parkYaw;q.front=Quaternion.Euler(0,a.parkYaw,0)*Vector3.back;
 EditorUtility.SetDirty(a);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();q.saved=true;EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
 }catch(Exception ex){q.error=ex.ToString();Debug.LogException(ex);}File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"casino-urgent-repair-saved.json"),JsonUtility.ToJson(q,true));}
}
