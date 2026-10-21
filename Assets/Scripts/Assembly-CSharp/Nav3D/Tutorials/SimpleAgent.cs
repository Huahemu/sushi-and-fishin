using Nav3D.API;
using UnityEngine;

namespace Nav3D.Tutorials
{
	public class SimpleAgent : MonoBehaviour
	{
		[SerializeField]
		private Transform m_TargetPoint;

		private Nav3DAgent m_Agent;

		private void Awake()
		{
			Nav3DManager.OnNav3DInit += Init;
		}

		private void Init()
		{
			m_Agent = GetComponent<Nav3DAgent>();
			m_Agent.MoveTo(m_TargetPoint.position);
		}
	}
}
