using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.Casino.Original;
using UnityEngine;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    [Serializable] public sealed class CasinoTarget
    {
        public string id,layout,stamp,child,type,prefab;
        public int floor;
        public long minBet,maxBet;
        public bool instantiated,available;
        [NonSerialized] public GameBase machine;
    }
    public sealed class CasinoClusterIdentity:MonoBehaviour
    {public string layout,stamp;public int floor;}
    // Observes the SAME original assignment result consumed by ObjectStamp.
    // It never invokes preview selection, replays layout RNG or simulates play.
    public static class NativeCasinoTargetDirectory
    {
        static readonly Dictionary<string,CasinoTarget> rows=new Dictionary<string,CasinoTarget>(StringComparer.Ordinal);
        static readonly Dictionary<string,(string layout,int floor,GameObject prefab)> clusters=new Dictionary<string,(string,int,GameObject)>();
        public static string Layout{get;private set;}
        public static event Action Changed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){rows.Clear();clusters.Clear();Layout=null;Changed=null;}
        public static string Path(Transform value,Transform stop=null)
        {
            var parts=new List<string>();for(var t=value;t&&t!=stop;t=t.parent)parts.Add(t.name+"["+t.GetSiblingIndex()+"]");parts.Reverse();return string.Join("/",parts);
        }
        public static void BeginOriginalAssignment(int seed,int quota)
        {
            string key=seed+"/"+quota;
            Layout=key;rows.Clear();clusters.Clear();Changed?.Invoke();
        }
        public static void RecordOriginalAssignment(ObjectStamp stamp,GameObject prefab,int seed,int quota)
        {
            if(!stamp||!stamp.Floor||!prefab)return;if(Layout!=seed+"/"+quota)BeginOriginalAssignment(seed,quota);
            string stampKey=Path(stamp.transform,stamp.Floor.transform);
            clusters[stamp.Floor.floorIndex+"/"+stampKey]=(Layout,stamp.Floor.floorIndex,prefab);
            foreach(var child in prefab.GetComponentsInChildren<GameStamp>(true))
            {
                var gamePrefab=child.AssignedGamePrefab;if(!gamePrefab)continue;
                string childKey=Path(child.transform,prefab.transform);
                foreach(var game in gamePrefab.GetComponentsInChildren<GameBase>(true))
                {
                    if(game.GameType==CasinoGameType.CoinFlip)continue;
                    string machinePath=childKey+"/"+Path(game.transform,gamePrefab.transform);
                    string id=Layout+"/"+stamp.Floor.floorIndex+"/"+stampKey+"/"+machinePath;
                    if(!rows.ContainsKey(id))rows[id]=new CasinoTarget{id=id,layout=Layout,stamp=stampKey,child=machinePath,
                        floor=stamp.Floor.floorIndex,type=game.GameType.ToString(),prefab=gamePrefab.name};
                }
            }
            Changed?.Invoke();
        }
        public static void BindOriginalCluster(ObjectStamp stamp,GameObject prefab,GameObject spawned)
        {
            if(!stamp||!stamp.Floor||!spawned)return;
            string key=Path(stamp.transform,stamp.Floor.transform);
            if(!clusters.TryGetValue(stamp.Floor.floorIndex+"/"+key,out var plan)||plan.prefab!=prefab)return;
            var identity=spawned.GetComponent<CasinoClusterIdentity>();if(!identity)identity=spawned.AddComponent<CasinoClusterIdentity>();
            identity.layout=plan.layout;identity.floor=plan.floor;identity.stamp=key;
        }
        public static void BindOriginalGame(GameStamp stamp,GameObject prefab,GameObject spawned)
        {
            if(!stamp||!prefab||!spawned)return;var cluster=stamp.GetComponentInParent<CasinoClusterIdentity>();if(!cluster)return;
            string child=Path(stamp.transform,cluster.transform);
            foreach(var game in spawned.GetComponentsInChildren<GameBase>(true))
            {
                string machinePath=child+"/"+Path(game.transform,spawned.transform);
                string id=cluster.layout+"/"+cluster.floor+"/"+cluster.stamp+"/"+machinePath;
                if(!rows.TryGetValue(id,out var row)||row.type!=game.GameType.ToString())continue;
                row.machine=game;row.instantiated=true;row.minBet=game.MinBet;row.maxBet=game.MaxBet;
            }
            Changed?.Invoke();
        }
        public static CasinoTarget[] Observe()=>rows.Values.OrderBy(r=>r.floor).ThenBy(r=>r.id,StringComparer.Ordinal).Select(Clone).ToArray();
        static CasinoTarget Clone(CasinoTarget r)=>new CasinoTarget{id=r.id,layout=r.layout,stamp=r.stamp,child=r.child,type=r.type,prefab=r.prefab,floor=r.floor,
            instantiated=r.machine,available=r.machine&&!r.machine.isPlaying&&!r.machine.NativeHasActiveLease,minBet=r.machine?r.machine.MinBet:r.minBet,maxBet=r.machine?r.machine.MaxBet:r.maxBet,machine=r.machine};
        public static bool TryGet(string id,out CasinoTarget target)
        {target=null;if(!rows.TryGetValue(id??"",out var row))return false;target=Clone(row);return target.layout==Layout;}
    }
}
