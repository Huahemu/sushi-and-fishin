using UnityEngine;
namespace HowToFish {
    public sealed class FishingWaterZone:MonoBehaviour {
        public Vector2 halfSize=new Vector2(70,70);
        public float surfaceHeight;
        public bool Contains(Vector3 point){var local=transform.InverseTransformPoint(point);return Mathf.Abs(local.x)<halfSize.x&&Mathf.Abs(local.z)<halfSize.y;}
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireCube(transform.position,new Vector3(halfSize.x*2,.1f,halfSize.y*2));}
    }
}
