using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.CityExpansion;
using UnityEngine;
using UnityEngine.AI;
namespace SushiAndFish.ResidentialIsland
{
    // A private home is not an NPC home, patrol point, dining destination or
    // teleport target. Carving covers walking; this guard covers direct spawns/warps.
    [DefaultExecutionOrder(32000)]
    public sealed class HousingNpcExclusion : MonoBehaviour
    {
        readonly List<HousingHouse> homes=new List<HousingHouse>();
        ResidentManager manager;
        float nextTargets;
        public int Relocations {get;private set;}
        public int RedirectedTargets {get;private set;}
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        public bool Excludes(Vector3 point,float margin=.55f)
        {
            foreach(var home in homes)
            {
                if(!home||home.definition==null)continue;
                var p=home.transform.InverseTransformPoint(point);var size=home.definition.footprint;
                if(p.y>-3&&p.y<8&&Mathf.Abs(p.x)<size.x/2+margin&&Mathf.Abs(p.z)<size.y/2+margin)return true;
            }
            return false;
        }
        public void Register(HousingHouse house)
        {if(!homes.Contains(house))homes.Add(house);BindManager();SanitizeTargets();ProtectNow();}
        void BindManager()
        {
            var current=Singleton<ResidentManager>.Instance;if(current==manager)return;
            if(manager)manager.RuntimeResidentRegistered-=Registered;
            manager=current;if(manager)manager.RuntimeResidentRegistered+=Registered;
        }
        public bool TrySafePosition(Vector3 origin,out Vector3 safe)
        {
            return HousingNpcLanding.Nearest(origin,homes.Where(h=>h).Select(HousingPrivacyVolume.Of).ToList(),null,null,out safe);
        }
        public bool RelocateForPlacement(HousingPlot plot,HousingType type,float yaw)
        {
            var proposed=new HousingPrivacyVolume{center=plot.Center,rotation=Quaternion.Euler(0,yaw,0),size=type.footprint};
            var volumes=homes.Where(h=>h).Select(HousingPrivacyVolume.Of).ToList();volumes.Add(proposed);
            var moves=new List<(ResidentController actor,Vector3 point)>();var reserved=new List<Vector3>();
            foreach(var resident in FindObjectsByType<ResidentController>(FindObjectsSortMode.None).OrderBy(r=>r.ResidentId))
                if(proposed.Contains(resident.transform.position,.6f))
                {
                    if(!HousingNpcLanding.Nearest(resident.transform.position,volumes,resident,reserved,out var point))return false;
                    moves.Add((resident,point));reserved.Add(point);
                }
            foreach(var move in moves)
            {
                var brain=move.actor.Brain;
                if(brain!=null){brain.Movement.Stop();brain.Movement.Warp(move.point,move.actor.transform.rotation);if(move.actor.Runtime!=null){move.actor.Runtime.TargetDestination=move.point;move.actor.Runtime.ClearRestaurantAssignment();}}
                else move.actor.transform.position=move.point;
                Relocations++;
            }
            return true;
        }
        void Registered(ResidentController resident){Protect(resident);}
        void CorrectHome(ResidentController resident)
        {
            var field=typeof(ResidentController).GetField("_homePosition",Fields);
            var position=(Vector3)field.GetValue(resident);
            if(!Excludes(position)||!TrySafePosition(position,out var safe))return;
            field.SetValue(resident,safe);HousingIsland.SetField(resident,"_hasConfiguredHome",true);
            if(resident.Brain!=null)typeof(ResidentBrain).GetField("_waypoints",Fields).SetValue(resident.Brain,new ResidentWaypointResolver(safe,resident.transform.rotation));
            RedirectedTargets++;
        }
        void Protect(ResidentController resident)
        {
            if(!resident)return;
            CorrectHome(resident);
            bool inside=Excludes(resident.transform.position);
            var brain=resident.Brain;var runtime=resident.Runtime;
            bool target=runtime!=null&&runtime.HasValidTarget&&Excludes(runtime.TargetDestination);
            bool destination=brain!=null&&Excludes(brain.Movement.Destination);
            if(!inside&&!target&&!destination)return;
            if(!TrySafePosition(resident.transform.position,out var safe))return;
            if(brain!=null)
            {
                // Give the active state a safe target as well as clearing its path.
                if(runtime!=null){runtime.TargetDestination=safe;runtime.HasValidTarget=true;runtime.ClearRestaurantAssignment();runtime.HasArrived=false;}
                brain.Movement.Stop();
                if(inside){brain.Movement.Warp(safe,resident.transform.rotation);Relocations++;}
                brain.Movement.MoveTo(safe,brain.EffectiveMoveSpeed,.2f,true);
            }
            else if(inside){resident.transform.position=safe;Relocations++;}
            if(target||destination)RedirectedTargets++;
        }
        public void ProtectNow()
        {
            BindManager();homes.RemoveAll(h=>!h);
            foreach(var resident in FindObjectsByType<ResidentController>(FindObjectsSortMode.None))Protect(resident);
        }
        public void SanitizeTargets()
        {
            foreach(var anchor in FindObjectsByType<WaypointAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(Excludes(anchor.transform.position)&&TrySafePosition(anchor.transform.position,out var safe))
                {anchor.transform.position=safe;anchor.RefreshNavMeshPosition();RedirectedTargets++;}
            if(manager)foreach(string name in new[]{"_parkWalkPoints","_cityPatrolNearPoints","_restaurantCallEntryPoints"})
            {
                var points=typeof(ResidentManager).GetField(name,Fields)?.GetValue(manager) as List<Transform>;
                points?.RemoveAll(t=>!t||Excludes(t.position));
            }
            foreach(var scheduler in FindObjectsByType<TownMealScheduler>(FindObjectsSortMode.None))
                if(scheduler.seats!=null)scheduler.seats=scheduler.seats.Where(s=>s&&!Excludes(s.transform.position)).ToArray();
        }
        void LateUpdate()
        {
            if(homes.Count==0)return;
            if(Time.unscaledTime>=nextTargets){BindManager();SanitizeTargets();nextTargets=Time.unscaledTime+.5f;}
            ProtectNow();
        }
        void OnDestroy(){if(manager)manager.RuntimeResidentRegistered-=Registered;}
    }
}
