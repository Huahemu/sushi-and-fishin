using UnityEngine;

namespace Threepeat
{
	public class CameraSmoothFollow : MonoBehaviour
	{
		public Vector3 offset = new Vector3(0f, 0f, -10f);

		public float smoothTime = 0.25f;

		private Vector3 velocity = Vector3.zero;

		[SerializeField]
		private Transform target;

		private void Update()
		{
			Vector3 vector = target.position + offset;
			base.transform.position = Vector3.SmoothDamp(base.transform.position, vector, ref velocity, smoothTime);
		}
	}
}
