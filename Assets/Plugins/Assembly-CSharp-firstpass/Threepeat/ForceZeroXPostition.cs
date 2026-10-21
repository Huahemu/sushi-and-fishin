using UnityEngine;

namespace Threepeat
{
	public class ForceZeroXPostition : MonoBehaviour
	{
		private void LateUpdate()
		{
			if (base.transform.position.x != 0f)
			{
				base.transform.position = new Vector3(0f, base.transform.position.y, base.transform.position.z);
			}
		}
	}
}
