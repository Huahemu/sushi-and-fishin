using System;
using System.Collections.Generic;
using System.Linq;
using SushiAndFish.Casino.Original;
using UnityEngine;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    [Serializable] public sealed class NativeCasinoActorEnergy
    {public string personId;public float energy;}
    // Stores the ORIGINAL component's value, not another gameplay energy pool.
    // Default is read from AddComponent<PlayerEnergy> (the original zero), never
    // invented100. Original Poker still calls DecreaseEnergy(6.6f) and its clamp.
    public sealed class NativeCasinoOriginalProfileState:MonoBehaviour
    {
        FriendsLifeRuntime runtime;FriendsEconomyService economy;FriendsEconomySave state;PlayerEnergy source;
        NativeCasinoActorEnergy record;string world,person;bool binding;
        public bool Matches(FriendsLifeRuntime actualRuntime,FriendsEconomyService actualEconomy,string actor)
            =>runtime==actualRuntime&&economy==actualEconomy&&ReferenceEquals(state,actualEconomy.State)&&world==actualRuntime.State.worldId&&person==actor;
        public bool Bind(FriendsLifeRuntime runtime,FriendsEconomyService economy,string actor,out string reason)
        {
            reason=null;var actual=GetComponent<PlayerEnergy>();
            if(!runtime||!runtime.Ready||!runtime.IsAuthority||!economy||!actual||GetComponent<ActorCasinoIdentity>()?.personId!=actor)
            {reason="Actual original NPC identity/energy component required";return false;}
            this.runtime=runtime;this.economy=economy;state=economy.State;source=actual;person=actor;world=runtime.State.worldId;
            state.originalCasinoActorEnergy=state.originalCasinoActorEnergy??new List<NativeCasinoActorEnergy>();
            record=state.originalCasinoActorEnergy.FirstOrDefault(v=>v.personId==actor);
            if(record==null){record=new NativeCasinoActorEnergy{personId=actor,energy=source.Networkenergy};state.originalCasinoActorEnergy.Add(record);}
            if(float.IsNaN(record.energy)||float.IsInfinity(record.energy)||record.energy<0||record.energy>100)
            {reason="Saved original NPC energy is outside its original component range; preserve for inspection";return false;}
            binding=true;try{source.Networkenergy=record.energy;}finally{binding=false;}
            source._Mirror_SyncVarHookDelegate_energy-=Changed;source._Mirror_SyncVarHookDelegate_energy+=Changed;return true;
        }
        void Changed(float before,float after){if(!binding)Capture();}
        void LateUpdate(){Capture();}
        public void Capture()
        {
            if(!runtime||!economy||!runtime.Ready||!runtime.IsAuthority||!ReferenceEquals(state,economy.State)||runtime.State.worldId!=world||
                !source||record==null||GetComponent<ActorCasinoIdentity>()?.personId!=person)return;
            float value=source.Networkenergy;
            // Original DecreaseEnergy performs subtraction then clamp. Do not
            // persist an intermediate negative value inside that same method.
            if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>100||record.energy==value)return;
            record.energy=value;runtime.NotifyChanged();
        }
        void OnDestroy(){if(source)source._Mirror_SyncVarHookDelegate_energy-=Changed;}
    }
}
namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomySave
    {public List<CasinoCore.NativeCasinoActorEnergy> originalCasinoActorEnergy=new List<CasinoCore.NativeCasinoActorEnergy>();}
}
