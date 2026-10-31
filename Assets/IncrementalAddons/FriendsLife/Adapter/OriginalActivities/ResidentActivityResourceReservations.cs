using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
    // Resource ownership only. This is not a body scheduler or a social rule.
    public static class ResidentActivityResourceReservations
    {
        public sealed class Claim
        {
            public string resourceId, ownerKey;
            public long epoch;
            public ResidentController actor;
            public UnityEngine.Object resource;
            public Vector3 pose;
        }
        static readonly Dictionary<string, Claim> claims=new Dictionary<string, Claim>(StringComparer.Ordinal);
        static long sequence;
        static readonly object sync=new object();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){lock(sync){claims.Clear();sequence=0;}}
        static void RemoveDestroyed()
        {
            var dead=new List<string>();foreach(var pair in claims)if(!pair.Value.actor||!pair.Value.resource)dead.Add(pair.Key);
            foreach(string key in dead)claims.Remove(key);
        }
        public static bool TryAcquire(string resourceId,string ownerKey,ResidentController actor,UnityEngine.Object resource,Vector3 pose,out Claim claim)
        {
            claim=null;if(string.IsNullOrEmpty(resourceId)||string.IsNullOrEmpty(ownerKey)||!actor||!resource)return false;
            var lifetime=actor.GetComponent<ResidentActivityResourceLifetime>();if(!lifetime)lifetime=actor.gameObject.AddComponent<ResidentActivityResourceLifetime>();lifetime.actorInstance=actor.GetInstanceID();
            lock(sync)
            {
                RemoveDestroyed();
                if(claims.TryGetValue(resourceId,out var existing))
                {if(existing.actor!=actor||existing.ownerKey!=ownerKey)return false;claim=existing;return true;}
                // Duplicate scene anchor keys must not seat two bodies at the
                // same authored pose merely because their object IDs differ.
                foreach(var other in claims.Values)
                    if(other.actor!=actor&&Vector3.Distance(other.pose,pose)<.1f)return false;
                claim=new Claim{resourceId=resourceId,ownerKey=ownerKey,actor=actor,resource=resource,pose=pose,epoch=++sequence};claims.Add(resourceId,claim);return true;
            }
        }
        internal static void ReleaseActor(int actorInstance)
        {
            lock(sync)
            {
                var keys=new List<string>();
                foreach(var pair in claims)
                {
                    var claim=pair.Value;if(!claim.actor||claim.actor.GetInstanceID()==actorInstance)
                    {if(claim.resource is EmoteArea area)area.ReleaseResidentReservation(claim.ownerKey+"/seat/"+claim.epoch);keys.Add(pair.Key);}
                }
                foreach(string key in keys)claims.Remove(key);
            }
        }
        public static bool IsOwned(Claim claim)
        {lock(sync){return claim!=null&&claim.actor&&claim.resource&&claims.TryGetValue(claim.resourceId,out var current)&&ReferenceEquals(current,claim)&&current.epoch==claim.epoch;}}
        public static bool Release(Claim claim)
        {lock(sync){return claim!=null&&claims.TryGetValue(claim.resourceId,out var current)&&ReferenceEquals(current,claim)&&current.epoch==claim.epoch&&claims.Remove(claim.resourceId);}}
        // Cheap pre-filter for choosing among same-type seats: true when another
        // actor already holds a claim on (practically) this authored pose.
        public static bool IsPoseReservedByOther(Vector3 pose,ResidentController actor,float radius=.35f)
        {
            lock(sync)
            {
                foreach(var other in claims.Values)
                    if(other.actor&&other.resource&&other.actor!=actor&&Vector3.Distance(other.pose,pose)<radius)return true;
                return false;
            }
        }
        public static bool IsReserved(string resourceId,out string ownerKey)
        {lock(sync){RemoveDestroyed();bool found=claims.TryGetValue(resourceId,out var claim);ownerKey=found?claim.ownerKey:null;return found;}}
    }
}
