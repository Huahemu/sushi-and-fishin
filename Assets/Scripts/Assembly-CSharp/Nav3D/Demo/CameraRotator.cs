using UnityEngine;

namespace Nav3D.Demo
{
	public class CameraRotator : MonoBehaviour
	{
		[SerializeField]
		private Transform m_Target;

		[SerializeField]
		private int m_MouseRotateKeyCode;

		[SerializeField]
		private float m_Sensitivity = 3f;

		[SerializeField]
		private float m_YLimit = 80f;

		[SerializeField]
		private float m_Zoom = 0.25f;

		[SerializeField]
		private float m_ZoomMaxDist = 20f;

		[SerializeField]
		private float m_ZoomMinDist = 3f;

		private Transform m_Transform;

		private Vector3 m_Offset;

		private float m_X;

		private float m_Y;

		private KeyCode CameraRotationMouseKey => m_MouseRotateKeyCode switch
		{
			0 => KeyCode.Mouse0, 
			1 => KeyCode.Mouse1, 
			2 => KeyCode.Mouse2, 
			_ => KeyCode.Mouse0, 
		};

		private void Start()
		{
			m_Transform = base.transform;
			m_YLimit = Mathf.Abs(m_YLimit);
			if (m_YLimit > 90f)
			{
				m_YLimit = 90f;
			}
			m_Offset = new Vector3(m_Offset.x, m_Offset.y, (0f - Mathf.Abs(m_ZoomMaxDist)) / 2f);
			m_Transform.position = ((m_Target != null) ? m_Target.position : (Vector3.zero + m_Offset));
		}

		private void Update()
		{
			if (Input.GetAxis("Mouse ScrollWheel") > 0f)
			{
				m_Offset.z += m_Zoom;
			}
			else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
			{
				m_Offset.z -= m_Zoom;
			}
			m_Offset.z = Mathf.Clamp(m_Offset.z, 0f - Mathf.Abs(m_ZoomMaxDist), 0f - Mathf.Abs(m_ZoomMinDist));
			if (Input.GetKey(CameraRotationMouseKey))
			{
				m_X = m_Transform.localEulerAngles.y + Input.GetAxis("Mouse X") * m_Sensitivity;
				m_Y += Input.GetAxis("Mouse Y") * m_Sensitivity;
				m_Y = Mathf.Clamp(m_Y, 0f - m_YLimit, m_YLimit);
			}
			m_Transform.localEulerAngles = new Vector3(0f - m_Y, m_X, 0f);
			m_Transform.position = m_Transform.localRotation * m_Offset + ((m_Target != null) ? m_Target.position : Vector3.zero);
		}
	}
}
