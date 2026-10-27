using System;
using System.Collections.Generic;
using ECM2;
using UnityEngine;

namespace SushiAndFish.FishingIntegration {
    // Keep ECM2's capsule sweeps, blocking and moving-platform queries. Only
    // remove the two ways walking can push a protected dynamic body: the motor's
    // explicit impulse and the PhysX kinematic-capsule contact response.
    public sealed class FishingWalkCollisionGuard:IDisposable {
        readonly CharacterMovement movement;
        readonly Collider capsule;
        readonly HashSet<Collider> targets=new HashSet<Collider>();
        readonly HashSet<Collider> ownedPairs=new HashSet<Collider>();
        bool disposed;
        public FishingWalkCollisionGuard(CharacterMovement value){movement=value;capsule=value.collider;movement.collisionResponseCallback+=OnResponse;}
        public void Protect(IEnumerable<Collider> colliders){
            if(disposed||!movement||!capsule)return;
            foreach(var collider in colliders){
                if(!collider||collider==capsule||collider.isTrigger)continue;
                targets.Add(collider);
                if(!capsule.enabled||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                if(!Physics.GetIgnoreCollision(capsule,collider)){
                    // CapsuleIgnoreCollision changes only the PhysX pair. The
                    // similarly named IgnoreCollision would remove query hits
                    // as well and make the player fall through the boat.
                    movement.CapsuleIgnoreCollision(collider);ownedPairs.Add(collider);
                }
            }
        }
        void OnResponse(ref CollisionResult hit,ref Vector3 characterImpulse,ref Vector3 otherImpulse)=>FilterResponse(targets,ref hit,ref characterImpulse,ref otherImpulse);
        public static void FilterResponse(ISet<Collider> protectedTargets,ref CollisionResult hit,ref Vector3 characterImpulse,ref Vector3 otherImpulse){
            if(hit.collider&&protectedTargets.Contains(hit.collider))otherImpulse=Vector3.zero;
        }
        public void Prune(){targets.RemoveWhere(c=>!c);ownedPairs.RemoveWhere(c=>!c);}
        public void Dispose(){if(disposed)return;disposed=true;if(movement)movement.collisionResponseCallback-=OnResponse;
            if(capsule)foreach(var collider in ownedPairs)if(collider)Physics.IgnoreCollision(capsule,collider,false);
            targets.Clear();ownedPairs.Clear();
        }
    }
}
