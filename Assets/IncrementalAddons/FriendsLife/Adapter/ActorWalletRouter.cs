using System;
using System.Collections;
using System.Collections.Generic;
using SushiAndFish.Casino;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
namespace SushiAndFish.FriendsLife
{
    public sealed class ActorCasinoIdentity:MonoBehaviour{public string personId;}
    public sealed class CasinoInvocation:IDisposable
    {
        [ThreadStatic]static Stack<CasinoInvocation> stack;
        public readonly string actorId,roundId,operationId;
        internal readonly FriendsCasinoCloneDriver owner;
        internal readonly ICasinoGoldWallet prior;
        readonly Func<bool> nativeValidity;
        readonly CasinoInvocation parent;
        readonly bool installed;
        int payment;
        public static CasinoInvocation Current=>stack!=null&&stack.Count>0?stack.Peek():null;
        public bool IsNpc=>owner!=null||nativeValidity!=null;
        public bool IsValid=>owner!=null?owner&&owner.Owned:nativeValidity?.Invoke()==true;
        CasinoInvocation(string actor,string round,string operation,FriendsCasinoCloneDriver owned=null,Func<bool> valid=null)
        {
            actorId=actor;roundId=round;operationId=operation;owner=owned;nativeValidity=valid;
            parent=Current;prior=CasinoRuntimeContext.Wallet;installed=IsNpc;
            if(installed&&(string.IsNullOrEmpty(actor)||string.IsNullOrEmpty(round)||string.IsNullOrEmpty(operation)||!IsValid))
                throw new InvalidOperationException("An active native machine, actor and round claim is required");
            if(stack==null)stack=new Stack<CasinoInvocation>();stack.Push(this);
            if(installed)CasinoRuntimeContext.Wallet=ActorWalletRouter.Instance;
        }
        public static CasinoInvocation Enter(string actor,string round,string operation)=>new CasinoInvocation(actor,round,operation);
        public static CasinoInvocation EnterNative(string actor,string round,string operation,Func<bool> valid)
        {
            if(valid==null)throw new ArgumentNullException(nameof(valid));
            return new CasinoInvocation(actor,round,operation,null,valid);
        }
        internal static CasinoInvocation EnterOwned(FriendsCasinoCloneDriver owner,string operation)
        {if(!owner||!owner.Owned)throw new InvalidOperationException("An existing owned NPC machine claim is required");return new CasinoInvocation(owner.PersonId,owner.RoundId,operation,owner);}
        internal string PaymentIdentity()=>"casino/"+roundId+"/"+operationId+"/"+(payment++);
        internal ICasinoGoldWallet HumanPort=>prior==ActorWalletRouter.Instance?parent?.HumanPort:prior;
        public static T Local<T>(Func<T> command){using(Enter("player","local-command",Guid.NewGuid().ToString("N")))return command();}
        public static void Local(Action command){using(Enter("player","local-command",Guid.NewGuid().ToString("N")))command();}
        public void Dispose()
        {
            if(stack==null||stack.Count==0||stack.Peek()!=this)throw new InvalidOperationException("Casino actor scopes must unwind in order");
            stack.Pop();if(installed)CasinoRuntimeContext.Wallet=prior;
        }
    }
    public sealed class CasinoScopedEnumerator:IEnumerator,IDisposable
    {
        readonly IEnumerator source;readonly string actor,round;readonly Func<bool> valid;object current;
        public CasinoScopedEnumerator(IEnumerator source,string actor,string round,Func<bool> valid){this.source=source;this.actor=actor;this.round=round;this.valid=valid;}
        public object Current=>current;
        public bool MoveNext(){if(!(valid?.Invoke()??true))return false;using(CasinoInvocation.Enter(actor,round,"enumerator")){bool next=source.MoveNext();current=next?source.Current:null;return next;}}
        public void Reset()=>throw new NotSupportedException();public void Dispose()=>(source as IDisposable)?.Dispose();
    }
    public sealed class ActorWalletRouter:ICasinoGoldWallet
    {
        public static readonly ActorWalletRouter Instance=new ActorWalletRouter();
        public static bool IsNpcInvocation=>CasinoInvocation.Current?.IsNpc==true;
        public static bool TryChangeNativeTickets(long delta,out long result)
        {
            result=0;var call=CasinoInvocation.Current;var economy=FriendsEconomyService.Instance;
            return call?.IsNpc==true&&call.IsValid&&economy&&
                economy.CommitNativeCasinoTickets(call.actorId,delta,call.PaymentIdentity(),out result);
        }
        public long WholeCoins
        {
            get
            {
                var call=CasinoInvocation.Current;
                if(call?.IsNpc==true)return call.IsValid?(FriendsEconomyService.Instance?.AvailableCents(call.actorId)??0)/100:0;
                return call?.HumanPort?.WholeCoins??ParkCasinoIntegration.Instance?.WholeCoins??0;
            }
        }
        public bool TryChangeCoins(long delta,out long result)
        {
            var call=CasinoInvocation.Current;result=WholeCoins;
            if(call?.IsNpc!=true){var port=call?.HumanPort;return port!=null&&port!=this&&port.TryChangeCoins(delta,out result);}
            if(!call.IsValid)return false;long cents;try{cents=checked(delta*100);}catch(OverflowException){return false;}
            bool accepted=FriendsEconomyService.Instance&&FriendsEconomyService.Instance.CommitDelta(call.actorId,cents,call.PaymentIdentity(),"casino-round");
            result=WholeCoins;return accepted;
        }
    }
}
