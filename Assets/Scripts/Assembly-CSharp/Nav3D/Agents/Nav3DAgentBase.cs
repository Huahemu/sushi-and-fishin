using UnityEngine;

namespace Nav3D.Agents
{
	public abstract class Nav3DAgentBase : MonoBehaviour
	{
		protected Transform m_CachedTransform;

		public abstract void DoFixedUpdate();
	}
}
