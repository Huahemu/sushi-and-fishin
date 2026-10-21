using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;

public static class ParkCasinoInstallation
{
    const string Imported="Assets/SushiAndFish/Casino/Imported/";
    const string HostScene="Assets/SushiAndFish/Modules/OriginalIsland/Scenes/GameState.unity";
    static bool continueToPlay;
    public static void ApplyAndVerify(){continueToPlay=true;Apply();ParkCasinoPlayAudit.Start();}
    [Serializable] public class Report
    {
        public string utc,error;public int missingScripts,exteriorRenderers,exteriorColliders;
        public Vector3 origin,lowMin,lowMax;public float scale,terrainHeight,entryFloorY;
        public bool parkFootprintInside,buildSceneRegistered;
        public List<WalkPoint> walking=new List<WalkPoint>();
    }
    [Serializable]public class WalkPoint {public float z,ground;public bool support,clear;public string obstacle;}

    public static void Apply()
    {
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),scale=.47f};
        try
        {
            OriginalNativeShaderRestoration.RestoreSerializedReferences();
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Imported}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material){material.SetOverrideTag(CasinoShaderOwnership.Tag,"1");EditorUtility.SetDirty(material);}
            }
            CasinoLayerBridge.Apply();
            var scene=EditorSceneManager.OpenScene(HostScene);
            foreach(var root in scene.GetRootGameObjects().Where(g=>g.name=="ParkCasino"))UnityEngine.Object.DestroyImmediate(root);
            var rootObject=new GameObject("ParkCasino");
            var adapter=rootObject.AddComponent<ParkCasinoIntegration>();
            adapter.networkPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"CasinoLab/Prefabs/NetworkManager.prefab").GetComponent<CustomNetworkManager>();
            adapter.supportServicesPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"CasinoLab/Prefabs/SupportServices.prefab");
            // Sample only the actual park surface. Roofs and unrelated props
            // are excluded by looking close to the known flat ground height.
            Physics.SyncTransforms();
            var hits=Physics.RaycastAll(new Vector3(-101,8,-38.4f),Vector3.down,10,~0,QueryTriggerInteraction.Ignore);
            var support=hits.Where(h=>h.normal.y>.9f&&h.point.y>2&&h.point.y<4).OrderByDescending(h=>h.point.y).FirstOrDefault();
            if(!support.collider)throw new Exception("No measured park support surface at casino entrance.");
            report.terrainHeight=support.point.y;
            adapter.parkOrigin=new Vector3(-101,report.terrainHeight-.19885f*adapter.exteriorScale,-72);
            report.origin=adapter.parkOrigin;report.entryFloorY=adapter.parkOrigin.y+.19885f*adapter.exteriorScale;
            var exterior=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"CasinoLab/Prefabs/CasinoLobbyAndExterior.prefab"),scene);
            exterior.name="OriginalCasinoPermanentExterior";
            exterior.transform.SetParent(rootObject.transform,true);
            exterior.transform.SetPositionAndRotation(adapter.parkOrigin,Quaternion.identity);
            exterior.transform.localScale=Vector3.one*adapter.exteriorScale;
            // This permanent copy only supplies the visible building while the
            // elevator's active map is a native indoor floor.
            foreach(var behaviour in exterior.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            adapter.permanentExterior=exterior;
            report.exteriorRenderers=exterior.GetComponentsInChildren<Renderer>(true).Length;
            report.exteriorColliders=exterior.GetComponentsInChildren<Collider>(true).Length;
            report.missingScripts=exterior.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);var max=new Vector3(float.MinValue,float.MinValue,float.MinValue);
            foreach(var filter in exterior.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var local=exterior.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if(local.y>5)continue;
                    var world=filter.transform.TransformPoint(vertex);min=Vector3.Min(min,world);max=Vector3.Max(max,world);
                }
            }
            report.lowMin=min;report.lowMax=max;
            report.parkFootprintInside=min.x>=-152&&max.x<=-50&&min.z>=-74&&max.z<=16;
            if(!report.parkFootprintInside)throw new Exception("Native exterior ground footprint exceeds the reserved park.");
            var build=EditorBuildSettings.scenes.ToList();string casino=Imported+"Scenes/CasinoScene.unity";
            if(!build.Any(s=>s.path==casino))build.Add(new EditorBuildSettingsScene(casino,true));
            EditorBuildSettings.scenes=build.ToArray();report.buildSceneRegistered=true;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText(Path.Combine(IslandRevision20261018.Reports,"casino-park-installation.json"),JsonUtility.ToJson(report,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        if(!continueToPlay || !string.IsNullOrEmpty(report.error))EditorApplication.Exit(string.IsNullOrEmpty(report.error)?0:1);
    }
}
