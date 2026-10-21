using System.Collections;
using UnityEngine;

namespace HoaxGames
{
	public class DestroyAfterElapsedTime : MonoBehaviour
	{
		[SerializeField]
		private float m_timeToElapseForDestruction = 6f;

		[SerializeField]
		private bool m_destroyParent;

		private IEnumerator Start()
		{
			yield return new WaitForSeconds(m_timeToElapseForDestruction);
			if (!m_destroyParent)
			{
				Object.Destroy(base.gameObject);
			}
			else
			{
				Object.Destroy(base.transform.parent.gameObject);
			}
		}
	}
}
