using UnityEngine;
namespace HowToFish {
    public sealed class FishingSeagull:MonoBehaviour {
        public Transform leftWing,rightWing;
        public float phase;
        Quaternion leftRest,rightRest;
        void Awake(){if(leftWing)leftRest=leftWing.localRotation;if(rightWing)rightRest=rightWing.localRotation;}
        void LateUpdate(){
            float flap=Mathf.Sin(Time.time*6.8f+phase)*36;
            if(leftWing)leftWing.localRotation=Quaternion.AngleAxis(-flap,leftWing.parent.InverseTransformDirection(transform.forward))*leftRest;
            if(rightWing)rightWing.localRotation=Quaternion.AngleAxis(flap,rightWing.parent.InverseTransformDirection(transform.forward))*rightRest;
        }
    }
}
