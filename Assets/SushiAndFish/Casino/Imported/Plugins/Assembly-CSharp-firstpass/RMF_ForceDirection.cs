namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[AddComponentMenu("Radial Menu Framework/RMF Force Direction")]
[ExecuteInEditMode]
public class RMF_ForceDirection : MonoBehaviour
{
	private RectTransform rt;

	[Tooltip("This will force this particular element to always have the specified absolute Z rotation. Use 0 for a straight upwards facing.")]
	public float forcedZRotation;

	private Vector3 rot = Vector3.zero;

	private void Awake()
	{
		rot.z = forcedZRotation;
		rt = GetComponent<RectTransform>();
	}

	private void Start()
	{
	}

	private void Update()
	{
		if (!Application.isPlaying)
		{
			rot.z = forcedZRotation;
		}
		if (rt.eulerAngles != rot)
		{
			rt.eulerAngles = rot;
		}
	}
}

}
