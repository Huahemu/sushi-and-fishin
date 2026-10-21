namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class TransformSnapper : MonoBehaviour
{
	[Header("Snap Settings")]
	[Tooltip("World position snap step (ex: 0.05)")]
	public float positionSnap = 0.05f;

	[Tooltip("World rotation snap step (ex: 15)")]
	public float rotationSnap = 15f;

	[Tooltip("World scale snap step (ex: 0.1)")]
	public float scaleSnap = 0.1f;

	public void ForceSnap()
	{
		Vector3 position = base.transform.position;
		Vector3 eulerAngles = base.transform.eulerAngles;
		Vector3 lossyScale = base.transform.lossyScale;
		if (positionSnap > 0f)
		{
			base.transform.position = SnapVector3(position, positionSnap);
		}
		if (rotationSnap > 0f)
		{
			base.transform.eulerAngles = SnapVector3(eulerAngles, rotationSnap);
		}
		if (scaleSnap > 0f)
		{
			Vector3 localScale = SnapVector3(lossyScale, scaleSnap);
			if (base.transform.parent != null)
			{
				Vector3 lossyScale2 = base.transform.parent.lossyScale;
				Vector3 localScale2 = new Vector3((lossyScale2.x != 0f) ? (localScale.x / lossyScale2.x) : localScale.x, (lossyScale2.y != 0f) ? (localScale.y / lossyScale2.y) : localScale.y, (lossyScale2.z != 0f) ? (localScale.z / lossyScale2.z) : localScale.z);
				base.transform.localScale = localScale2;
			}
			else
			{
				base.transform.localScale = localScale;
			}
		}
	}

	private Vector3 SnapVector3(Vector3 value, float snap)
	{
		return new Vector3(Mathf.Round(value.x / snap) * snap, Mathf.Round(value.y / snap) * snap, Mathf.Round(value.z / snap) * snap);
	}
}

}
