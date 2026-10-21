using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.GastronormPanIngredients
{
	public class PanIngredientGraphics : MonoBehaviour
	{
		[SerializeField]
		private Transform _ingredientsParent;

		private Tween _staggeredSyncTween;

		private static int _lastDeferredBuildFrame = -1;

		public List<GameObject> IngredientObjects { get; private set; } = new List<GameObject>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetBuildBudget()
		{
			_lastDeferredBuildFrame = -1;
		}

		public static bool TryBeginDeferredBuild()
		{
			if (_lastDeferredBuildFrame == Time.frameCount)
			{
				return false;
			}
			_lastDeferredBuildFrame = Time.frameCount;
			return true;
		}

		private void Awake()
		{
			Initialize();
		}

		public virtual void Initialize()
		{
			IngredientObjects.Clear();
			if (_ingredientsParent == null)
			{
				Debug.LogWarning("[PanIngredientGraphics] _ingredientsParent atanmamış!");
				return;
			}
			for (int i = 0; i < _ingredientsParent.childCount; i++)
			{
				Transform child = _ingredientsParent.GetChild(i);
				child.gameObject.SetActive(value: false);
				IngredientObjects.Add(child.gameObject);
			}
		}

		public virtual void SyncToCount(int target)
		{
			if (IngredientObjects.Count == 0)
			{
				return;
			}
			target = Mathf.Clamp(target, 0, IngredientObjects.Count);
			for (int i = 0; i < IngredientObjects.Count; i++)
			{
				bool flag = i < target;
				if (IngredientObjects[i].activeSelf != flag)
				{
					IngredientObjects[i].SetActive(flag);
				}
			}
		}

		public virtual void SyncToCountStaggered(int previous, int target, float interval)
		{
			CancelStaggeredSync();
			if (target <= previous || interval <= 0f)
			{
				SyncToCount(target);
				return;
			}
			target = Mathf.Clamp(target, 0, IngredientObjects.Count);
			int visibleCount = Mathf.Min(previous + 1, target);
			SyncToCount(visibleCount);
			if (visibleCount >= target)
			{
				return;
			}
			_staggeredSyncTween = DOTween.To(() => visibleCount, (int value) =>
			{
				if (value != visibleCount)
				{
					visibleCount = value;
					SyncToCount(value);
				}
			}, target, (float)(target - visibleCount) * interval).SetEase(Ease.Linear).SetLink(base.gameObject, LinkBehaviour.KillOnDestroy)
				.OnKill(() =>
				{
					_staggeredSyncTween = null;
				})
				.OnComplete(() =>
				{
					_staggeredSyncTween = null;
				});
		}

		public void CancelStaggeredSync()
		{
			_staggeredSyncTween?.Kill();
			_staggeredSyncTween = null;
		}

		public virtual void ResetAll()
		{
			CancelStaggeredSync();
			foreach (GameObject ingredientObject in IngredientObjects)
			{
				ingredientObject.SetActive(value: false);
			}
		}
	}
}
