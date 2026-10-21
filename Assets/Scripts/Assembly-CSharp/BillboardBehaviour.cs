using UnityEngine;

public class BillboardBehaviour : MonoBehaviour
{
	[SerializeField]
	private Transform m_lookAtTarget;

	[SerializeField]
	private bool m_onlyAroundUpAxis;

	private Transform m_transform;

	private void Awake()
	{
		m_transform = base.transform;
	}

	private void Start()
	{
	}

	private void Update()
	{
		Vector3 vector = m_lookAtTarget.position - m_transform.position;
		if (m_onlyAroundUpAxis)
		{
			vector -= Vector3.Project(vector, Vector3.up);
			m_transform.LookAt(m_transform.position - vector, Vector3.up);
		}
		else
		{
			m_transform.LookAt(m_transform.position - vector, m_lookAtTarget.up);
		}
	}
}
