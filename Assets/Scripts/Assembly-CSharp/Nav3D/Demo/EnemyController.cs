using Nav3D.API;
using UnityEngine;

namespace Nav3D.Demo
{
	[RequireComponent(typeof(Nav3DAgent))]
	public class EnemyController : MonoBehaviour
	{
		private Nav3DAgent m_Agent;

		private Vector3 m_AttackPoint;

		public bool IsVictim { get; private set; }

		public void Init(Vector3 _AttackPoint)
		{
			m_AttackPoint = _AttackPoint;
			MoveToTarget();
		}

		public void MarkAsVictim()
		{
			IsVictim = true;
		}

		public void Destroy()
		{
			Object.Destroy(base.gameObject);
		}

		private void MoveToTarget()
		{
			m_Agent.MoveTo(m_AttackPoint, Destroy);
		}

		private void Awake()
		{
			m_Agent = GetComponent<Nav3DAgent>();
		}
	}
}
