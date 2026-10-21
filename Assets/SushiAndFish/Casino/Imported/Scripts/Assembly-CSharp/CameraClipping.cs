namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class CameraClipping : MonoBehaviour
{
	[SerializeField]
	private Transform clippingPlane;

	[SerializeField]
	private Camera cam;

	private Plane _plane;

	private void ClipCameraMatrix()
	{
		_plane.normal = ConvertEulerAnglesToVector3(clippingPlane.rotation.eulerAngles, Vector3.down);
		Plane plane = new Plane(_plane.normal, cam.transform.position);
		_plane.distance = 0f - plane.GetDistanceToPoint(clippingPlane.position + cam.transform.position);
		Vector4 vector = new Vector4(_plane.normal.x, _plane.normal.y, _plane.normal.z, _plane.distance);
		Vector4 clipPlane = Matrix4x4.Transpose(Matrix4x4.Inverse(cam.worldToCameraMatrix)) * vector;
		cam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
	}

	private Vector3 ConvertEulerAnglesToVector3(Vector3 euler, Vector3 upVector)
	{
		return Quaternion.Euler(euler) * upVector;
	}

	public void LateUpdate()
	{
		ClipCameraMatrix();
	}
}

}
