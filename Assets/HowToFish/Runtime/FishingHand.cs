using UnityEngine;
using UnityEngine.Events;
namespace HowToFish {
    public sealed class FishingHand:MonoBehaviour {
        public FishingRig rig;
        public bool tracked;
        public FishingItem Held{get;private set;}
        public UnityEvent<float,float> haptic=new UnityEvent<float,float>();
        public bool Grab(FishingItem item){
            if(!item||Held||item.owner||item.Retired)return false;
            if(item.fish&&!item.fish.CanGrab){rig.Notify("活鱼不能拾取，先用枪击败它");return false;}
            if(item.fish&&item.fish.Attached)item.fish.fishing.ReleaseCatch(item.fish);
            Held=item;item.owner=this;item.SetHeld(true);item.RestoreToolPose();item.transform.SetParent(transform,false);item.transform.localPosition=Vector3.zero;item.transform.localRotation=Quaternion.identity;item.SyncBodies();if(item.fish)item.fish.MarkArchiveAcquired();Pulse(.2f,.04f);return true;
        }
        public void Forget(FishingItem item){if(!item)return;if(Held==item)Held=null;if(item.owner==this)item.owner=null;}
        public void Release(){
            if(!Held)return;
            var item=Held;
            if(item.kind==FishingItemKind.Rod){item.GetComponent<FishingRod>().module.ReturnRod();return;}
            if(item.kind==FishingItemKind.Weapon){item.GetComponent<FishWeapon>().combat.ReturnGun(item.GetComponent<FishWeapon>());return;}
            Forget(item);item.transform.SetParent(item.fish.fishing.transform,true);item.SetHeld(false);
        }
        public void Pulse(float amount,float seconds){haptic.Invoke(amount,seconds);}
    }
}
