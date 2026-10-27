using System;
using UnityEngine;
namespace HowToFish {
    public sealed class FishingVitals:MonoBehaviour {
        public FishingModule fishing;
        public int Health{get;private set;}=100;
        public float HitFlash{get;private set;}
        public bool Down=>Health==0;
        public event Action Died;
        public event Action<int> HealthChanged;
        void Update(){HitFlash=Mathf.MoveTowards(HitFlash,0,Time.deltaTime*1.8f);}
        public void Hit(int amount,string fish){if(Down)return;Health=Mathf.Max(0,Health-amount);HitFlash=1;foreach(var hand in fishing.rig.hands)hand.Pulse(.45f,.12f);fishing.rig.Notify(fish+"撞到了你 · 生命 "+Health+" / 100");HealthChanged?.Invoke(Health);if(Down){fishing.Abort();Died?.Invoke();}}
        public void Restore(int health=100){Health=Mathf.Clamp(health,1,100);HealthChanged?.Invoke(Health);}
    }
}
