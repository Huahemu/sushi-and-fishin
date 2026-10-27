using System;
using System.Linq;
using HowToFish;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    public sealed partial class FishingMigration {
        FishingWorldEvents eventDirector,replicaEvents;
        FishingModule replicaContext;
        FishingEnvironmentOverlay environment;
        void TickEvents(){
            var caster=actors.Values.FirstOrDefault(a=>a.module&&a.equipped&&a.inputActive&&a.equipment<0&&a.module.State!=FishingState.Ready&&a.module.State!=FishingState.Charging);
            if(!eventDirector){
                var actor=actors.Values.FirstOrDefault(a=>a.module);if(actor==null)return;
                eventDirector=CreateDirector(actor.module,false);
                eventDirector.Eligibility=()=>actors.Values.Any(a=>a.module&&a.equipped&&a.inputActive&&a.equipment<0&&(a.module.State==FishingState.Casting||a.module.State==FishingState.Floating||a.module.State==FishingState.Hooked));
                foreach(var other in actors.Values)if(other.module)other.module.worldEvents=eventDirector;
            }
            if(caster!=null)eventDirector.module=caster.module;
            else if(eventDirector.Current!=FishingEventKind.None)eventDirector.SuspendEvents();
            if(eventDirector.weather)eventDirector.weather.module=Local!=null&&Local.module?Local.module:eventDirector.module;
            foreach(var actor in actors.Values)if(actor.module)actor.module.worldEvents=eventDirector;
        }
        bool IsCovered(Vector3 point){foreach(var hit in Physics.RaycastAll(point,Vector3.up,40,~0,QueryTriggerInteraction.Ignore))if(!hit.collider.GetComponentInParent<FishingItem>()&&!hit.collider.GetComponentInParent<PaperPixelGames.Core.Player.PPG_Player>())return true;return false;}
        FishingWorldEvents CreateDirector(FishingModule context,bool replica){
            var template=Config.systemPrefab.GetComponent<FishingWorldEvents>();
            var root=new GameObject(replica?"Fishing event presentation":"Fishing-only world event director");root.transform.SetParent(transform);
            var director=root.AddComponent<FishingWorldEvents>();director.module=context;director.automatic=!replica;
            director.seagullPrefab=template.seagullPrefab;director.seaHorse=template.seaHorse;director.waterSignalMaterial=template.waterSignalMaterial;director.seagullCalls=template.seagullCalls;
            director.firstEventDelayMin=template.firstEventDelayMin;director.firstEventDelayMax=template.firstEventDelayMax;director.eventDuration=template.eventDuration;director.intervalMin=template.intervalMin;director.intervalMax=template.intervalMax;
            director.seagullWeight=template.seagullWeight;director.seahorseWeight=template.seahorseWeight;director.rainWeight=template.rainWeight;director.mistWeight=template.mistWeight;director.repeatWeight=template.repeatWeight;director.flockSize=template.flockSize;director.flockHeight=template.flockHeight;
            director.weatherDurationMin=template.weatherDurationMin;director.weatherDurationMax=template.weatherDurationMax;director.weatherTransition=template.weatherTransition;director.rareWeightMultiplier=template.rareWeightMultiplier;director.baseShinyChance=template.baseShinyChance;director.seagullShinyChance=template.seagullShinyChance;director.seaHorseChance=template.seaHorseChance;
            var weatherTemplate=Config.systemPrefab.GetComponent<FishingWeather>();var weather=root.AddComponent<FishingWeather>();weather.module=context;weather.rainMaterial=weatherTemplate.rainMaterial;weather.rainLoop=weatherTemplate.rainLoop;weather.mistLoop=weatherTemplate.mistLoop;
            weather.rainFishSigns=weatherTemplate.rainFishSigns?.Select(t=>{if(!t)return null;var sign=Instantiate(t,root.transform);sign.gameObject.SetActive(false);return sign;}).ToArray();
            environment=FindFirstObjectByType<FishingEnvironmentOverlay>();if(!environment)environment=gameObject.AddComponent<FishingEnvironmentOverlay>();weather.EnvironmentBlend=environment.Apply;
            weather.Covered=IsCovered;director.weather=weather;return director;
        }
        internal void ApplyReplicaEvent(int kind,float remaining,float duration){
            if(Server||Local==null||!Local.Camera)return;
            if(!replicaContext){var root=new GameObject("Client fishing weather context");root.SetActive(false);root.transform.SetParent(transform);replicaContext=root.AddComponent<FishingModule>();replicaContext.enabled=false;replicaContext.zones=Array.Empty<FishingWaterZone>();replicaContext.waterSurface=new GameObject("Sea height").transform;replicaContext.waterSurface.SetParent(root.transform);var rig=root.AddComponent<FishingRig>();rig.hands=Array.Empty<FishingHand>();rig.view=Local.Camera;rig.locomotionRoot=Local.player.transform;replicaContext.rig=rig;replicaContext.WaterQuery=IsWater;root.SetActive(true);}
            replicaContext.waterSurface.position=Vector3.up*SeaHeight;
            if(!replicaEvents)replicaEvents=CreateDirector(replicaContext,true);
            replicaEvents.ApplyReplicaState((FishingEventKind)kind,remaining,duration);
        }
    }
}
