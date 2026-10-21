using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperPixelGames.Core.UI;

public static class CasinoHostInventory
{
    [Serializable] public class TextSample {public string text;public float preferredWidth,rectWidth,fontSize;public bool overflow;}
    [Serializable] public class Label {public string path;public bool autoSize;public float min,max,width,height;public List<TextSample> samples=new List<TextSample>();}
    [Serializable] public class Model {public string path;public Vector3 min,max;public int renderers,colliders,missingScripts;public List<Ground> entrance=new List<Ground>();}
    [Serializable] public class Ground {public float scale,z,ground,clearance;public bool supported,standingFits;public string collider;}
    [Serializable] public class Result {public string utc,error;public Label gold;public List<Model> models=new List<Model>();public int parcels;public Vector3[] parcelPositions;}
    static string Reports=>IslandRevision20261018.Reports;
    static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
    public static void Run()
    {
        var result=new Result{utc=DateTime.UtcNow.ToString("o")};
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity");
            var ui=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EconomyUI>(true)).Single();
            var label=(TextMeshProUGUI)new SerializedObject(ui).FindProperty("_shopBalanceText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();result.gold=new Label{path=P(label.transform),autoSize=label.enableAutoSizing,min=label.fontSizeMin,max=label.fontSizeMax,width=label.rectTransform.rect.width,height=label.rectTransform.rect.height};
            var old=label.text;
            foreach(var value in new[]{"100","1,000,000","21,474,836.46","1,000,000,000,000","9,223,372,036,854,775,807"})
            {
                label.text=value;label.ForceMeshUpdate(true,true);
                result.gold.samples.Add(new TextSample{text=value,preferredWidth=label.preferredWidth,rectWidth=label.rectTransform.rect.width,fontSize=label.fontSize,overflow=label.isTextOverflowing});
            }
            label.text=old;
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var parcels=all.Single(t=>t.name=="YellowParcelReservations");result.parcels=parcels.childCount;result.parcelPositions=parcels.Cast<Transform>().Select(t=>t.position).ToArray();
            foreach(var name in new[]{"CasinoLobbyAndExterior","CasinoBuilding"})
            {
                string path="Assets/SushiAndFish/Casino/Imported/CasinoLab/Prefabs/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var renders=root.GetComponentsInChildren<Renderer>(true);var bounds=renders[0].bounds;
                    foreach(var r in renders)bounds.Encapsulate(r.bounds);
                    var model=new Model{path=path,min=bounds.min,max=bounds.max,renderers=renders.Length,colliders=root.GetComponentsInChildren<Collider>(true).Length,missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))};
                    var physics=root.scene.GetPhysicsScene();
                    foreach(float scale in new[]{1f,.5f,.4f,.35f,.3f})
                    {
                        root.transform.localScale=Vector3.one*scale;Physics.SyncTransforms();
                        foreach(float z in new[]{40f,44f,48f,52f,56f,60f,63f})
                        {
                            var row=new Ground{scale=scale,z=z*scale};
                            var hits=new RaycastHit[32];int n=physics.Raycast(new Vector3(0,20,z*scale),Vector3.down,hits,80,~0,QueryTriggerInteraction.Ignore);
                            var hit=hits.Take(n).Where(h=>h.normal.y>.8f).OrderByDescending(h=>h.point.y).FirstOrDefault();
                            if(hit.collider){row.supported=true;row.ground=hit.point.y;row.collider=P(hit.collider.transform);var colliders=new Collider[64];int overlaps=physics.OverlapCapsule(new Vector3(0,hit.point.y+.32f,z*scale),new Vector3(0,hit.point.y+1.48f,z*scale),.3f,colliders,~0,QueryTriggerInteraction.Ignore);row.standingFits=overlaps==0;var up=new RaycastHit[16];int nu=physics.Raycast(hit.point+Vector3.up*.05f,Vector3.up,up,10,~0,QueryTriggerInteraction.Ignore);row.clearance=nu==0?10:up.Take(nu).Min(h=>h.distance)+.05f;}
                            model.entrance.Add(row);
                        }
                    }
                    result.models.Add(model);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        catch(Exception e){result.error=e.ToString();}
        finally
        {
            File.WriteAllText(Path.Combine(Reports,"casino-host-ui-and-building-inventory.json"),JsonUtility.ToJson(result,true));
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.Exit(string.IsNullOrEmpty(result.error)?0:1);
        }
    }
}
