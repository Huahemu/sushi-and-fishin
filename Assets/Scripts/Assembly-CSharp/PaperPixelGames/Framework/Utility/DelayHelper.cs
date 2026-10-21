using System;
using System.Collections;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public class DelayHelper : Singleton<DelayHelper>
	{
		public Coroutine DelayAction(float seconds, Action action)
		{
			return StartCoroutine(DelayCoroutine(seconds, action));
		}

		public Coroutine DelayActionLoop(float seconds, Action action, bool immidiateCall = false)
		{
			return StartCoroutine(DelayCoroutineLoop(seconds, action, immidiateCall));
		}

		public Coroutine DelayActionUntilEndOfFrame(Action action)
		{
			return StartCoroutine(DelayEndOfFrameCoroutine(action));
		}

		public Coroutine DelayActionUntil(Func<bool> condition, Action action)
		{
			return StartCoroutine(DelayUntilCoroutine(condition, action));
		}

		public void StopCoroutineInstance(Coroutine coroutine)
		{
			if (coroutine != null)
			{
				StopCoroutine(coroutine);
			}
		}

		private IEnumerator DelayCoroutine(float seconds, Action action)
		{
			yield return new WaitForSeconds(seconds);
			action?.Invoke();
		}

		private IEnumerator DelayCoroutineLoop(float seconds, Action action, bool immidiateCall)
		{
			if (immidiateCall)
			{
				action?.Invoke();
			}
			while (true)
			{
				yield return new WaitForSeconds(seconds);
				action?.Invoke();
			}
		}

		private IEnumerator DelayEndOfFrameCoroutine(Action action)
		{
            object boundary = new WaitForEndOfFrame();
#if UNITY_EDITOR
            // The Editor batch player loop has no WaitForEndOfFrame pump.
            // Only this explicitly enabled verification session substitutes a
            // next-frame boundary; normal Editor and player behavior is exact.
            if (Application.isBatchMode && (UnityEditor.SessionState.GetBool("SushiAndFish.ParkCasinoAudit.active", false) || UnityEditor.SessionState.GetBool("CasinoUrgent.active", false) || UnityEditor.SessionState.GetBool("TownCoastRevision.active", false))) boundary = null;
#endif
			yield return boundary;
			action?.Invoke();
		}

		private IEnumerator DelayUntilCoroutine(Func<bool> condition, Action action)
		{
			yield return new WaitUntil(condition);
			action?.Invoke();
		}
	}
}
