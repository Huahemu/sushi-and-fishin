using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HoaxGames
{
	public class FadeAwayDecal : MonoBehaviour
	{
		[SerializeField]
		private float m_elapsedTimeToDestroy = 5f;

		private WaitForEndOfFrame m_waitForEndOfFrame = new WaitForEndOfFrame();

		protected virtual IEnumerator Start()
		{
			if (m_elapsedTimeToDestroy > 0.01f)
			{
				DecalProjector decal = GetComponent<DecalProjector>();
				float startFadeFactor = decal.fadeFactor;
				float endTime = Time.time + m_elapsedTimeToDestroy;
				while (Time.time < endTime)
				{
					float t = (endTime - Time.time) / m_elapsedTimeToDestroy;
					decal.fadeFactor = Mathf.Lerp(0f, startFadeFactor, t);
					yield return m_waitForEndOfFrame;
				}
			}
			Object.Destroy(base.transform.parent.gameObject);
		}
	}
}
