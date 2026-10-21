using UnityEngine;

namespace SushiAndFish.FrontEnd
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IslandOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 islandCentre;
        [SerializeField] private float heightAboveCentre = 650f;
        [SerializeField] private float orthographicHalfHeight = 430f;
        [SerializeField] private float degreesPerSecond = 2f;
        [SerializeField] private float initialHeading;
        [SerializeField] private bool orthographicProjection;
        private float heading;
        public Vector3 IslandCentre => islandCentre;
        public float Heading => heading;
        public float DegreesPerSecond => degreesPerSecond;
        private void OnEnable(){heading=initialHeading;ApplyPose();}
        private void LateUpdate(){heading=Mathf.Repeat(heading+degreesPerSecond*Time.unscaledDeltaTime,360f);ApplyPose();}
        private void OnValidate(){ApplyPose();}
        public void Configure(Vector3 centre,float altitude,float halfHeight)
        {islandCentre=centre;heightAboveCentre=altitude;orthographicHalfHeight=halfHeight;ApplyPose();}
        public void ApplyPose()
        {
            transform.position=islandCentre+Vector3.up*heightAboveCentre;
            // Unity's world Y is vertical. Keep the camera straight down,
            // with a fixed centre/altitude and zero authored Z-axis tilt.
            transform.rotation=Quaternion.Euler(90f,heading,0f);
            var camera=GetComponent<Camera>();
            if(camera)
            {
                camera.orthographic=orthographicProjection;
                camera.orthographicSize=orthographicHalfHeight;
                camera.fieldOfView=Mathf.Clamp(2f*Mathf.Atan(orthographicHalfHeight/heightAboveCentre)*Mathf.Rad2Deg,30f,100f);
                camera.nearClipPlane=0.3f;camera.farClipPlane=2500f;
            }
        }
    }
}
