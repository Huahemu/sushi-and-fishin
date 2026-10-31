using System;
using SushiAndFish.FriendsLife;
using SushiAndFish.FriendsLife.CasinoCore;
using UnityEngine;

namespace SushiAndFish.Casino.Original
{
    public partial class GameBase
    {
        string nativeReservedActor,nativeReservedOwner,nativeActor,nativeOwner,nativeRound;
        PlayerProfile nativeProfile;
        bool nativeRoundStarted,nativeResultCommitted,nativeStartAttempted,nativePayoutEntered;
        int nativeOutstanding,nativePayoutOrdinal;
        public string NativeActor=>nativeActor;
        public string NativeOwner=>nativeOwner;
        public string NativeRound=>nativeRound;
        public string NativeFailure{get;private set;}
        public bool NativeHasActiveLease=>!string.IsNullOrEmpty(nativeOwner);
        public bool NativeHasUnsettledResult=>nativeRoundStarted&&(!nativeResultCommitted||nativeOutstanding>0)||isPlaying;
        public bool NativeRoundResultReady=>nativeRoundStarted&&nativeResultCommitted&&nativeOutstanding==0&&!isPlaying&&!nativePayoutEntered;
        public System.Collections.IEnumerator NativeScopeRoutine(System.Collections.IEnumerator source,string operation)
        {
            if(!NativeHasActiveLease)return source;
            nativeOutstanding++;return new NativeOriginalCasinoRoutine(this,source,nativeActor,nativeOwner,nativeRound,operation);
        }
        public void NativeRoutineCompleted(string actor,string owner,string round)
        {if(NativeLeaseMatches(actor,owner,round))nativeOutstanding=Math.Max(0,nativeOutstanding-1);}
        public void NativeRoutineFailed(string reason){NativeFailure=reason;}
        public bool NativeTryReserve(string actor,string owner)
        {
            if(!Mirror.NetworkServer.active||!gameObject.activeInHierarchy||string.IsNullOrEmpty(actor)||string.IsNullOrEmpty(owner)||isPlaying||NativeHasActiveLease)return false;
            if(!string.IsNullOrEmpty(nativeReservedOwner)&&nativeReservedOwner!=owner)return false;
            nativeReservedActor=actor;nativeReservedOwner=owner;return true;
        }
        public bool NativeReservationMatches(string actor,string owner)=>nativeReservedActor==actor&&nativeReservedOwner==owner;
        public bool NativeTryClaim(string actor,string owner,string targetId)
        {
            if(!Mirror.NetworkServer.active||!gameObject.activeInHierarchy||isPlaying||NativeHasActiveLease||!NativeReservationMatches(actor,owner))return false;
            var runtime=FriendsLifeRuntime.Instance;
            if(!FriendsBodyAuthority.IsOwned(runtime,actor,owner)||!NativeCasinoTargetDirectory.TryGet(targetId,out var row)||row.machine!=this||
                !CasinoMachineApproach.TryResolve(runtime.ActorOf(actor),this,out var goal,out _)||Vector3.Distance(runtime.ActorOf(actor).transform.position,goal)>.25f)return false;
            nativeActor=actor;nativeOwner=owner;nativeRound=null;nativeRoundStarted=false;nativeResultCommitted=false;nativeStartAttempted=false;nativePayoutEntered=false;nativeOutstanding=0;nativePayoutOrdinal=0;NativeFailure=null;return true;
        }
        public bool NativeLeaseMatches(string actor,string owner,string round=null)=>nativeActor==actor&&nativeOwner==owner&&
            (round==null||nativeRound==round)&&FriendsBodyAuthority.IsOwned(FriendsLifeRuntime.Instance,actor,owner);
        public bool NativeTryRelease(string actor,string owner)
        {
            if(nativeReservedActor==actor&&nativeReservedOwner==owner&&!NativeHasActiveLease){nativeReservedActor=nativeReservedOwner=null;return true;}
            if(nativeActor!=actor||nativeOwner!=owner||NativeHasUnsettledResult)return false;
            nativeReservedActor=nativeReservedOwner=nativeActor=nativeOwner=nativeRound=null;nativeRoundStarted=nativeResultCommitted=false;return true;
        }
        bool NativeAllowOriginalHumanStart()
        {
            var call=CasinoInvocation.Current;
            if(call?.IsNpc==true)return call.IsValid&&NativeLeaseMatches(call.actorId,nativeOwner,call.roundId)&&!nativeRoundStarted;
            // A planned reservation cannot reserve a human out for a long walk.
            // A source round that already took a bet cannot be cleared by it.
            if(NativeHasActiveLease&&nativeRoundStarted)return false;
            nativeReservedActor=nativeReservedOwner=null;
            if(NativeHasActiveLease){nativeActor=nativeOwner=nativeRound=null;nativeResultCommitted=false;}
            return true;
        }
        public bool NativeTryStartOriginal(string actor,string owner,string round,long bet,PlayerInteract input,out string reason)
        {
            reason=null;
            if(!Mirror.NetworkServer.active||!NativeLeaseMatches(actor,owner)||string.IsNullOrEmpty(round)||nativeStartAttempted||isPlaying||!input||
                input.GetComponent<ActorCasinoIdentity>()?.personId!=actor||!keypad||isGoldenChipApplied||isGoldenBet||
                bet<MinBet||bet>MaxBet||FriendsEconomyService.Instance.AvailableCents(actor)/100<bet)
            {reason="Original machine/session/input/budget guard refused start";return false;}
            // Slots is the first full input family. More original adapters must
            // explicitly supply their normal choices before using this method.
            if(!(this is Slots)){reason="Original input family adapter is not installed";return false;}
            nativeRound=round;nativeStartAttempted=true;
            using(FriendsEconomyService.Instance.DeferExternalCommit())
            using(CasinoInvocation.EnterNative(actor,round,"bet",()=>NativeLeaseMatches(actor,owner,round)))
            {
                keypad.ClearInput();foreach(char digit in bet.ToString(System.Globalization.CultureInfo.InvariantCulture))keypad.AppendDigit(digit.ToString());
                if(currentBet!=bet||keypad.GetCurrentInput()!=bet){reason="Original keypad readback differs from requested bet";return false;}
                TryStartGame(input);
            }
            if(!nativeRoundStarted){reason="Original source rejected before its accepted-bet boundary";return false;}
            return true;
        }
        void NativeOnOriginalBetAccepted()
        {
            var call=CasinoInvocation.Current;if(call?.IsNpc!=true)return;
            if(!call.IsValid||!NativeLeaseMatches(call.actorId,nativeOwner,call.roundId))throw new InvalidOperationException("Original bet lost immutable native ownership");
            nativeRoundStarted=true;
            nativeProfile=interactingPlayer;
            if(!nativeProfile||nativeProfile.GetComponent<ActorCasinoIdentity>()?.personId!=nativeActor)
                throw new InvalidOperationException("Original accepted bet has another resident profile");
        }
        IDisposable NativePayoutScope()
        {
            if(!NativeHasActiveLease)return null;
            if(!nativeRoundStarted||nativePayoutEntered||this is Slots&&nativeResultCommitted||interactingPlayer!=nativeProfile)
                throw new InvalidOperationException("Original result is duplicate or has no accepted native round");
            nativePayoutEntered=true;var actor=nativeActor;var owner=nativeOwner;var round=nativeRound;
            return CasinoInvocation.EnterNative(actor,round,"payout/"+nativePayoutOrdinal,()=>NativeLeaseMatches(actor,owner,round));
        }
        bool NativeRecordSourceResult(long bet,long payout,bool tipsy,bool melody,bool immunity,System.Collections.Generic.Dictionary<string,object> data)
        {
            if(!NativeHasActiveLease)return false;
            if(!nativePayoutEntered||!NativeCasinoResultLedger.Record(this,nativeActor,nativeRound,nativePayoutOrdinal,bet,payout,tipsy,melody,immunity,data))
                throw new InvalidOperationException("Native original result ledger did not commit");
            return true;
        }
        void NativeOnOriginalPayoutComplete()
        {if(!NativeHasActiveLease)return;nativeResultCommitted=true;nativePayoutEntered=false;nativePayoutOrdinal++;}
    }
}
