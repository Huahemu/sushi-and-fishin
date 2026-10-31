using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace SushiAndFish.FriendsLife
{
    public sealed class NativeCasinoTrackedRoutine:IEnumerator,IDisposable
    {
        internal readonly FriendsCasinoCloneDriver owner;internal readonly MonoBehaviour component;internal readonly IEnumerator raw;internal readonly int id;
        internal bool active=true;object current;float yieldedAt,duration;bool restoreYield;float remaining;
        internal NativeCasinoTrackedRoutine(FriendsCasinoCloneDriver owner,MonoBehaviour component,IEnumerator raw,int id){this.owner=owner;this.component=component;this.raw=raw;this.id=id;}
        public object Current=>current;
        public bool MoveNext()
        {
            if(!owner||owner.Suspended||!active)return false;
            if(restoreYield){restoreYield=false;duration=remaining;yieldedAt=Time.time;current=remaining>0?(object)new WaitForSeconds(remaining):null;return true;}
            owner.NativeDepth++;bool released=false;
            try
            {
                using(ReferenceEquals(component,owner.Game)?FriendsEconomyService.Instance.DeferExternalCommit():null)using(CasinoInvocation.EnterOwned(owner,"native/"+(++owner.NativeStep)))
                {
                    active=raw.MoveNext();current=active?raw.Current:null;duration=current is WaitForSeconds?(float)(NativeCasinoCloneCodec.Get(current,"m_Seconds")??0f):0;yieldedAt=Time.time;
                    owner.NativeDepth--;released=true;owner.NativeAfterMove(ReferenceEquals(component,owner.Game));return active;
                }
            }
            catch(Exception e){owner.NativeFailure(e);active=false;return false;}
            finally{if(!released)owner.NativeDepth--;if(!released&&owner.NativeDepth==0)owner.NativeAfterMove();}
        }
        public void Reset()=>throw new NotSupportedException();
        public void Dispose(){active=false;if(raw is IDisposable d)d.Dispose();}
        internal JObject Capture()=>new JObject{["id"]=id,["component"]=NativeCasinoCloneCodec.Encode(component,owner),["raw"]=NativeCasinoCloneCodec.Encode(raw,owner),["remaining"]=Mathf.Max(0,duration-(Time.time-yieldedAt))};
        internal static NativeCasinoTrackedRoutine Restore(JObject o,FriendsCasinoCloneDriver owner)
        {
            var component=(MonoBehaviour)NativeCasinoCloneCodec.Decode(o["component"],typeof(MonoBehaviour),owner);var raw=(IEnumerator)NativeCasinoCloneCodec.Decode(o["raw"],typeof(object),owner);
            return new NativeCasinoTrackedRoutine(owner,component,raw,(int)o["id"]){restoreYield=true,remaining=(float)o["remaining"]};
        }
    }
}
