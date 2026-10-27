using System;
using System.Collections.Generic;
using UnityEngine;
namespace HowToFish {
    public enum FishingItemKind {Rod,Weapon,Catch}
    public sealed class FishingRig:MonoBehaviour {
        public Camera view;
        public FishingHand[] hands;
        public Font interfaceFont;
        public Transform locomotionRoot;
        public bool Tracked {get;set;}
        public int ActiveIndex{get;private set;}=1;
        public FishingHand ActiveHand=>hands[ActiveIndex];
        public string Message{get;private set;}="按 1 取竿 · 右键拉竿，松开抛出 · 左键收线";
        public float MessageUntil{get;private set;}
        public event Action<string> Notified;
        readonly List<FishingItem> items=new List<FishingItem>();
        public IReadOnlyList<FishingItem> Items=>items;
        void Awake(){foreach(var hand in hands)if(hand)hand.rig=this;}
        public void Bind(Camera camera,Transform origin,Transform leftGrip,Transform rightGrip){view=camera;locomotionRoot=origin;hands[0].transform.SetParent(leftGrip,false);hands[1].transform.SetParent(rightGrip,false);}
        public void SelectHand(FishingHand hand){for(int i=0;i<hands.Length;i++)if(hands[i]==hand)ActiveIndex=i;}
        public void SwitchHand(){ActiveIndex=1-ActiveIndex;}
        public bool Take(FishingItem item){
            if(!item||item.Retired||item.fish&&!item.fish.CanGrab)return false;
            if(!Tracked&&item.kind!=FishingItemKind.Catch){
                // Desktop grips share the camera: keep one visible tool. Fish can
                // still occupy the other hand; tracked VR grips remain independent.
                foreach(var grip in hands)if(grip&&grip.Held&&grip.Held!=item&&grip.Held.kind!=FishingItemKind.Catch)grip.Release();
            }
            if(item.owner){SelectHand(item.owner);return true;}
            var hand=ActiveHand;
            if(item.kind==FishingItemKind.Catch&&hand.Held)hand=hands[1-ActiveIndex];
            if(hand.Held){if(item.kind==FishingItemKind.Catch)return false;hand.Release();}
            if(!hand.Grab(item))return false;SelectHand(hand);return true;
        }
        public bool Register(FishingItem item){if(!item)return false;if(!items.Contains(item))items.Add(item);item.rig=this;return true;}
        public void Unregister(FishingItem item){items.Remove(item);}
        public void Notify(string message){Message=message;MessageUntil=Time.unscaledTime+5;Notified?.Invoke(message);}
        public void Feedback(FishingHand hand){if(hand)hand.Pulse(.35f,.055f);}
    }
}
