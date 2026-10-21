using UnityEngine;

namespace PaperPixelGames
{
	public class BillboardBehaviour : MonoBehaviour
	{
		private Transform m_Camera;

		private void Awake()
		{
			if (m_Camera == null && Camera.main != null)
			{
				m_Camera = Camera.main.transform;
			}
		}

		private void LateUpdate()
		{
			if (m_Camera == null)
			{
				if (Camera.main != null)
				{
					m_Camera = Camera.main.transform;
				}
			}
			else
			{
				base.transform.rotation = Quaternion.LookRotation(m_Camera.forward, m_Camera.up);
				Vector3 eulerAngles = base.transform.rotation.eulerAngles;
				base.transform.rotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, 0f);
			}
		}
	}
}
