using UnityEngine;
namespace HowToFish {
    public sealed class FishCollisionRelay:MonoBehaviour {
        public FishingCatch fish;Rigidbody body;Vector3 previous;
        void Awake(){body=GetComponent<Rigidbody>();}
        void FixedUpdate(){if(body)previous=body.linearVelocity;}
        void OnCollisionEnter(Collision collision){fish.Contact(collision,previous,body);}
        void OnCollisionStay(Collision collision){fish.Contact(collision,Vector3.zero,body);}
    }
}
