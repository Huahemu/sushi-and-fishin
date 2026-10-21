using UnityEngine;

namespace Nav3D.Demo
{
	public class CameraCircleMover : MonoBehaviour
	{
		[SerializeField]
		private Transform m_LookPoint;

		[SerializeField]
		private Transform m_RotationPivot;

		[SerializeField]
		private float m_DegreesSpeed;

		private void FixedUpdate()
		{
			base.transform.RotateAround(m_RotationPivot.position, m_RotationPivot.up, m_DegreesSpeed);
			base.transform.LookAt(m_LookPoint);
		}
	}
}
