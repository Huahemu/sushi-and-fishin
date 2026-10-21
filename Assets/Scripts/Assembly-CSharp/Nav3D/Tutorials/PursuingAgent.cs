using Nav3D.API;
using UnityEngine;

namespace Nav3D.Tutorials
{
	public class PursuingAgent : MonoBehaviour
	{
		[SerializeField]
		private Transform m_Target;

		private Nav3DAgent m_Agent;

		private void Awake()
		{
			Nav3DManager.OnNav3DInit += Init;
		}

		private void Init()
		{
			m_Agent = GetComponent<Nav3DAgent>();
			m_Agent.FollowTarget(m_Target, _FollowContinuously: true, 0.1f, 0.5f, () =>
			{
				Object.Destroy(m_Target.gameObject);
				Object.Destroy(base.gameObject);
			});
		}
	}
}
