namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[ExecuteInEditMode]
public class TransformLocker : MonoBehaviour
{
	public bool LockPosition = true;

	public bool LockPositionX;

	public float LockedPositionX;

	public bool LockPositionY;

	public float LockedPositionY;

	public bool LockPositionZ;

	public float LockedPositionZ;

	private void OnDrawGizmos()
	{
		if (LockPosition)
		{
			Vector3 localPosition = base.transform.localPosition;
			if (LockPositionX)
			{
				localPosition.x = LockedPositionX;
			}
			if (LockPositionY)
			{
				localPosition.y = LockedPositionY;
			}
			if (LockPositionZ)
			{
				localPosition.z = LockedPositionZ;
			}
			base.transform.localPosition = localPosition;
		}
	}
}

}
