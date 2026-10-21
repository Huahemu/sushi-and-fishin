namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class RotateByTransformY : MonoBehaviour
{
	[SerializeField]
	private Transform target;

	[SerializeField]
	[Range(0f, 1f)]
	private float lerp = 0.1f;

	private void Update()
	{
		Quaternion b = Quaternion.Euler(base.transform.eulerAngles.x, target.eulerAngles.y, base.transform.eulerAngles.z);
		base.transform.rotation = Quaternion.Slerp(base.transform.rotation, b, lerp);
	}
}

}
