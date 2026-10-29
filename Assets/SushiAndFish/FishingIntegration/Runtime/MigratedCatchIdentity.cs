using System;
using System.Linq;
using HowToFish;
using PaperPixelGames.Core.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SushiAndFish.FishingIntegration {
    public sealed class MigratedCatchIdentity:MonoBehaviour {
        public string token;public ulong fisher,steam;
        BoxCollider pickup;float nextBounds;
        void LateUpdate(){
            var world=FishingMigration.Instance;if(!world)return;
            var catchBody=GetComponent<FishingCatch>();bool dead=world.Server?catchBody&&catchBody.CanGrab:world.FishSnapshots.TryGetValue(token,out var state)&&state.health<=0;
            if(!dead){if(pickup)pickup.enabled=false;return;}
            if(!pickup){var go=new GameObject("Dead fish pickup volume");go.transform.SetParent(transform,false);pickup=go.AddComponent<BoxCollider>();pickup.isTrigger=true;}
            pickup.enabled=true;if(Time.unscaledTime<nextBounds)return;nextBounds=Time.unscaledTime+.25f;
            bool found=false;Bounds bounds=default;
            foreach(var r in GetComponentsInChildren<Renderer>()){
                if(!r.enabled||r is ParticleSystemRenderer||r is LineRenderer||r is TrailRenderer)continue;
                var b=r.bounds;for(int i=0;i<8;i++){var p=transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}
            }
            if(found){pickup.center=bounds.center;pickup.size=Vector3.Max(bounds.size,Vector3.one*.12f);}
        }
    }
}
