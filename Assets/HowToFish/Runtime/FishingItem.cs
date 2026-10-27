using UnityEngine;
namespace HowToFish {
    [DefaultExecutionOrder(100)]
    public sealed class FishingItem:MonoBehaviour {
        public FishingItemKind kind;
        public FishingCatch fish;
        [System.NonSerialized] public bool ExternalGripPose;
        [HideInInspector]public FishingRig rig;
        [HideInInspector]public FishingHand owner;
        [HideInInspector]public Vector3 home;
        [HideInInspector]public Quaternion homeRotation;
        public bool Retired{get;private set;}
        Rigidbody[] bodies;Collider[] colliders;
        Transform homeParent;
        Vector3 homeLocalPosition,homeLocalScale;
        Quaternion homeLocalRotation;
        Transform[] modelParts;
        Vector3[] modelPositions,modelScales;
        Quaternion[] modelRotations;
        void Awake(){
            bodies=GetComponentsInChildren<Rigidbody>(true);colliders=GetComponentsInChildren<Collider>(true);
            SetHome(transform.parent,transform.position,transform.rotation);
            if(kind==FishingItemKind.Catch)return;
            modelParts=GetComponentsInChildren<Transform>(true);modelPositions=new Vector3[modelParts.Length];modelRotations=new Quaternion[modelParts.Length];modelScales=new Vector3[modelParts.Length];
            for(int i=0;i<modelParts.Length;i++){modelPositions[i]=modelParts[i].localPosition;modelRotations[i]=modelParts[i].localRotation;modelScales[i]=modelParts[i].localScale;}
        }
        public void SetHome(Transform parent,Vector3 position,Quaternion rotation){
            homeParent=parent;home=position;homeRotation=rotation;
            homeLocalPosition=parent?parent.InverseTransformPoint(position):position;
            homeLocalRotation=parent?Quaternion.Inverse(parent.rotation)*rotation:rotation;
            homeLocalScale=transform.localScale;
        }
        public void SetHeld(bool held){
            bool dynamic=kind==FishingItemKind.Catch&&!held;
            foreach(var part in bodies){if(!part.isKinematic){part.linearVelocity=Vector3.zero;part.angularVelocity=Vector3.zero;}part.isKinematic=!dynamic;part.useGravity=dynamic;part.interpolation=dynamic?RigidbodyInterpolation.Interpolate:RigidbodyInterpolation.None;}
            foreach(var c in colliders)c.enabled=!held;
            if(fish)fish.SetHeld(held);
        }
        public void RestoreToolPose(){
            if(modelParts==null)return;
            for(int i=0;i<modelParts.Length;i++)if(modelParts[i]&&modelParts[i]!=transform){modelParts[i].localPosition=modelPositions[i];modelParts[i].localRotation=modelRotations[i];modelParts[i].localScale=modelScales[i];}
            var rod=GetComponent<FishingRod>();if(rod)rod.ResetSteering();
        }
        public void SyncBodies(){foreach(var part in bodies){part.position=part.transform.position;part.rotation=part.transform.rotation;}}
        void LateUpdate(){if(owner)SyncBodies();}
        public void Recall(){
            if(owner||Retired)return;if(fish){fish.Recover();return;}
            var weapon=GetComponent<FishWeapon>();if(weapon)weapon.ResetForStorage();
            SetHeld(false);RestoreToolPose();
            // A returned tool must leave its old grip, even after owner was cleared.
            transform.SetParent(homeParent,false);transform.localPosition=homeLocalPosition;transform.localRotation=homeLocalRotation;transform.localScale=homeLocalScale;
            SyncBodies();
        }
        public void Retire(){if(Retired)return;Retired=true;if(fish&&fish.fishing)fish.fishing.ForgetCatch(fish);if(owner)owner.Forget(this);if(rig)rig.Unregister(this);gameObject.SetActive(false);Destroy(gameObject);}
    }
}
