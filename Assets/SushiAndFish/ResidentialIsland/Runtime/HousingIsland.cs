using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.CityExpansion;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingIsland : MonoBehaviour
    {
        public static HousingIsland Instance {get;private set;}
        public HousingCatalog Catalog {get;private set;}
        public readonly List<HousingPlot> Plots=new List<HousingPlot>();
        public PPG_Player Player=>Singleton<PlayersManager>.Instance?.LocalPlayer;
        public bool CanBuild=>NetworkManager.Singleton && NetworkManager.Singleton.IsServer && Player && Player.IsSpawned;
        public string LastFailure {get;private set;}
        public event Action Changed;
        NetworkManager registered;
        string loadedSave;
        int gameSceneHandle=-1;
        readonly Dictionary<Renderer,MaterialPropertyBlock> boundaryBlocks=new Dictionary<Renderer,MaterialPropertyBlock>();
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Instance || !Resources.Load<HousingCatalog>("Housing/Catalog"))return;
            var root=new GameObject("ResidentialIslandUpdate");DontDestroyOnLoad(root);root.AddComponent<HousingIsland>();
        }
        void Awake(){Instance=this;Catalog=Resources.Load<HousingCatalog>("Housing/Catalog");StartCoroutine(Watch());}
        IEnumerator Watch()
        {
            while(true)
            {
                var nm=NetworkManager.Singleton;
                if(nm && registered!=nm && Catalog.nativeSeatPrefab)
                {
                    // Registration is independent of host scene serialization.
                    if(!nm.NetworkConfig.Prefabs.Contains(Catalog.nativeSeatPrefab))nm.AddNetworkPrefab(Catalog.nativeSeatPrefab);
                    registered=nm;
                }
                var source=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(t=>Regex.IsMatch(t.name,@"^P\d+_Reserved_"));
                if(source && gameSceneHandle!=source.gameObject.scene.handle)
                {
                    Plots.Clear();loadedSave=null;gameSceneHandle=source.gameObject.scene.handle;
                    DiscoverPlots(source.gameObject.scene);Changed?.Invoke();
                }
                if(!source && gameSceneHandle!=-1){Plots.Clear();loadedSave=null;gameSceneHandle=-1;Changed?.Invoke();}
                foreach(var map in FindObjectsByType<PaperPixelGames.Core.Tablet.LiveSceneTabletMap>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(!map.GetComponent<HousingMap>())map.gameObject.AddComponent<HousingMap>();
                if(CanBuild && Plots.Count>0 && loadedSave==null && SavePath!=null)
                {
                    loadedSave=SavePath;
                    // Reusing a cleared save slot starts an empty housing world.
                    if(Singleton<SaveManager>.Instance.CurrentSaveSlotData.GameProgress.HasSave)Load();
                }
                yield return new WaitForSecondsRealtime(.75f);
            }
        }
        public void DiscoverPlots(Scene scene)
        {
            foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
            {
                var m=Regex.Match(t.name,@"^(P\d+)_Reserved_([\d.]+)x([\d.]+)m$");
                if(!m.Success || !t.gameObject.activeInHierarchy)continue; // P08/P13 already contain public buildings.
                float w=float.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
                float d=float.Parse(m.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture);
                Plots.Add(new HousingPlot{id=m.Groups[1].Value,source=t,rect=new Rect(t.position.x-w/2,t.position.z-d/2,w,d),
                    boundary=t.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("YellowDashedBoundary")).ToArray()});
            }
            Plots.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));
        }
        public void Highlight(HousingPlot selected)
        {
            foreach(var p in Plots)foreach(var r in p.boundary)
            {
                if(!r)continue;
                if(p==selected)
                {
                    if(!boundaryBlocks.ContainsKey(r)){var previous=new MaterialPropertyBlock();r.GetPropertyBlock(previous);boundaryBlocks[r]=previous;}
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
                    var color=new Color(1,1,.32f);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);block.SetColor("_EmissionColor",color*.55f);r.SetPropertyBlock(block);
                }
                else if(boundaryBlocks.TryGetValue(r,out var old)){r.SetPropertyBlock(old);boundaryBlocks.Remove(r);}
            }
        }
        public bool Fits(HousingPlot plot,HousingType type,float yaw,out string error)
        {
            error=null;
            if(plot==null || type?.prefab==null){error="住宅不可用";return false;}
            float normalized=Mathf.Repeat(yaw,360);
            if(Mathf.Abs(normalized-Mathf.Round(normalized/90)*90)>.01f){error="仅支持四个朝向";return false;}
            float c=Mathf.Abs(Mathf.Cos(yaw*Mathf.Deg2Rad)),s=Mathf.Abs(Mathf.Sin(yaw*Mathf.Deg2Rad));
            float w=type.footprint.x*c+type.footprint.y*s,d=type.footprint.x*s+type.footprint.y*c;
            if(w+.3f>plot.rect.width || d+.3f>plot.rect.height){error="该朝向超出地块";return false;}
            return true;
        }
        public bool Place(HousingPlot plot,HousingType type,float yaw,bool persist=true)
        {
            if(!CanBuild){LastFailure="等待房主进入游戏";return false;}
            if(plot.Occupied){LastFailure="地块已有住宅";return false;}
            if(!Fits(plot,type,yaw,out var failure)){LastFailure=failure;return false;}
            if(persist)
            {
                var inverse=Quaternion.Inverse(Quaternion.Euler(0,yaw,0));
                bool Inside(Vector3 point)
                {
                    var local=inverse*(point-plot.Center);
                    return local.y>-2&&local.y<5&&Mathf.Abs(local.x)<type.footprint.x/2+.6f&&Mathf.Abs(local.z)<type.footprint.y/2+.6f;
                }
                if(FindObjectsByType<PPG_Player>(FindObjectsSortMode.None).Any(p=>Inside(p.transform.position)))
                {LastFailure="请先让出地块再放置";return false;}
            }
            var privacy=GetComponent<HousingNpcExclusion>();if(!privacy)privacy=gameObject.AddComponent<HousingNpcExclusion>();
            if(!privacy.RelocateForPlacement(plot,type,yaw)){LastFailure="未找到可供角色站立的安全位置";return false;}
            var go=Instantiate(type.prefab);go.name="Residence_"+plot.id+"_"+type.id;
            SceneManager.MoveGameObjectToScene(go,plot.source.gameObject.scene);
            var center=plot.Center;
            var terrain=Terrain.activeTerrains.FirstOrDefault(t=>center.x>=t.transform.position.x&&center.x<=t.transform.position.x+t.terrainData.size.x&&center.z>=t.transform.position.z&&center.z<=t.transform.position.z+t.terrainData.size.z);
            if(terrain)center.y=terrain.SampleHeight(center)+terrain.transform.position.y;
            go.transform.SetPositionAndRotation(center+Vector3.up*.035f,Quaternion.Euler(0,yaw,0));
            var house=go.GetComponent<HousingHouse>();house.definition=type;house.plot=plot.id;plot.house=house;
            var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=Vector3.up*1.4f;
            obstacle.size=new Vector3(type.footprint.x,2.8f,type.footprint.y);obstacle.carving=true;obstacle.carveOnlyStationary=true;obstacle.carvingTimeToStationary=.1f;
            foreach(var a in house.chairs)SpawnNativeSeat(house,a);
            foreach(var rest in house.GetComponentsInChildren<HousingRestInteractable>(true))
            {
                var anchor=rest.gameObject.AddComponent<HousingSeatAnchor>();anchor.pose=rest.exit;anchor.exit=rest.exit;
                rest.area=SpawnNativeSeat(house,anchor);
                foreach(var c in rest.area.GetComponentsInChildren<Collider>())c.enabled=false;
                rest.area.GetComponent<CityDiningSeat>().enabled=false;
                rest.area.WorldUIAnchor.SetVisible(false);
                rest.area.WorldUIAnchor.enabled=false;
            }
            privacy.Register(house);
            Physics.SyncTransforms();LastFailure=null;if(persist)Save();Changed?.Invoke();return true;
        }
        EmoteArea SpawnNativeSeat(HousingHouse house,HousingSeatAnchor anchor)
        {
            var go=Instantiate(Catalog.nativeSeatPrefab,anchor.pose.position,anchor.pose.rotation);
            go.name="NativeResidenceSeat_"+house.plot;SceneManager.MoveGameObjectToScene(go,house.gameObject.scene);
            var area=go.GetComponent<EmoteArea>();SetField(area,"_targetPos",anchor.pose.position);SetField(area,"_targetRot",anchor.pose.eulerAngles);
            var dining=go.GetComponent<CityDiningSeat>();dining.seatedPose=anchor.pose;dining.approach=anchor.exit;
            var net=go.GetComponent<NetworkObject>();net.Spawn(destroyWithScene:true);house.spawnedSeats.Add(go);
            return area;
        }
        public static void SetField(object owner,string field,object value)=>owner.GetType().GetField(field,Fields)?.SetValue(owner,value);
        public void ClearFailure(){LastFailure=null;}
        string SavePath
        {
            get
            {
                var save=Singleton<SaveManager>.Instance;if(!save||!save.HasValidCurrentSlot)return null;
                string host=Regex.Replace(save.CurrentSaveSlotHostSteamID??"local",@"[^\w-]","_");
                return Path.Combine(Application.persistentDataPath,"ResidentialIsland",host+"_slot"+save.CurrentSaveSlotIndex+".json");
            }
        }
        public void Save()
        {
            if(!CanBuild||SavePath==null)return;
            var data=new HousingSave{homes=Plots.Where(p=>p.house).Select(p=>new HousingPlacement{plot=p.id,type=p.house.definition.id,yaw=p.house.transform.eulerAngles.y}).ToArray()};
            string path=SavePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(data,true));
            if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
        }
        void Load()
        {
            if(!File.Exists(loadedSave))return;
            try
            {
                var data=JsonUtility.FromJson<HousingSave>(File.ReadAllText(loadedSave));
                if(data==null||data.version!=1)return;
                foreach(var row in data.homes??Array.Empty<HousingPlacement>())
                {
                    var p=Plots.FirstOrDefault(x=>x.id==row.plot);var t=Catalog.homes.FirstOrDefault(x=>x.id==row.type);
                    if(p!=null && !p.Occupied && t?.prefab)Place(p,t,row.yaw,false);
                }
            }
            catch(Exception e){Debug.LogWarning("ResidentialIsland save: "+e.Message);}
        }
        void OnDestroy(){Highlight(null);if(Instance==this)Instance=null;}
    }
}
