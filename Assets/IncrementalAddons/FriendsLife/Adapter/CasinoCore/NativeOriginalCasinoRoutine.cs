using System;
using System.Collections;
using SushiAndFish.Casino.Original;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    // Re-enter an immutable native wallet scope for each original MoveNext.
    // No scope survives a Unity yield. The iterator and its RNG remain original.
    public sealed class NativeOriginalCasinoRoutine:IEnumerator,IDisposable
    {
        readonly GameBase machine;readonly IEnumerator source;readonly string actor,owner,round,operation;
        bool finished;object current;
        public NativeOriginalCasinoRoutine(GameBase machine,IEnumerator source,string actor,string owner,string round,string operation)
        {this.machine=machine;this.source=source;this.actor=actor;this.owner=owner;this.round=round;this.operation=operation;}
        public object Current=>current;
        public bool MoveNext()
        {
            if(finished)return false;
            if(!machine||!machine.NativeLeaseMatches(actor,owner,round))
            {if(machine)machine.NativeRoutineFailed("Original coroutine immutable ownership changed");Finish();return false;}
            var runtime=FriendsLifeRuntime.Instance;
            if(!runtime||!runtime.IsWorldRunning){current=null;return true;}
            try
            {
                using(CasinoInvocation.EnterNative(actor,round,operation,()=>machine&&machine.NativeLeaseMatches(actor,owner,round)))
                {
                    bool next=source.MoveNext();
                    if(!next){Finish();current=null;return false;}
                    current=source.Current;
                    if(current is IEnumerator nested)current=machine.NativeScopeRoutine(nested,operation+"/nested");
                    return true;
                }
            }
            catch(Exception e){machine.NativeRoutineFailed(e.GetBaseException().Message);Finish();throw;}
        }
        void Finish(){if(finished)return;finished=true;if(machine)machine.NativeRoutineCompleted(actor,owner,round);}
        public void Dispose(){try{(source as IDisposable)?.Dispose();}finally{Finish();}}
        public void Reset()=>throw new NotSupportedException();
    }
}
