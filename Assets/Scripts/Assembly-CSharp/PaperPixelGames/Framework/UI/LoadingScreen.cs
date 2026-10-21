using UnityEngine;

namespace PaperPixelGames.Framework.UI
{
	public static class LoadingScreen
	{
		private static LoadingScreenBehaviour _instance;

		public static bool HasInstance => _instance != null;

		public static long BeginReturnCover()
		{
			if (!(_instance != null))
			{
				return 0L;
			}
			return _instance.BeginReturnCover();
		}

		public static void EndReturnCover(long version)
		{
			if (_instance != null)
			{
				_instance.EndReturnCover(version);
			}
		}

		public static void Register(LoadingScreenBehaviour behaviour)
		{
			if (!(behaviour == null))
			{
				if (_instance != null && _instance != behaviour)
				{
					Debug.LogError("[LoadingScreen] Duplicate LoadingScreenBehaviour detected. Existing: " + _instance.name + ", New: " + behaviour.name + ". The new instance will be ignored.", behaviour);
				}
				else
				{
					_instance = behaviour;
				}
			}
		}

		public static void Unregister(LoadingScreenBehaviour behaviour)
		{
			if (_instance == behaviour)
			{
				_instance = null;
			}
		}

		public static void Show(string title, string desc)
		{
			if (!(_instance == null))
			{
				_instance.Push(title, desc);
			}
		}

		public static void ShowLocalized(string titleKey, string titleFallback, string descriptionKey, string descriptionFallback)
		{
			if (!(_instance == null))
			{
				_instance.PushLocalized(titleKey, titleFallback, descriptionKey, descriptionFallback);
			}
		}

		public static void UpdateText(string title, string desc)
		{
			if (!(_instance == null))
			{
				_instance.UpdateText(title, desc);
			}
		}

		public static void UpdateLocalizedText(string titleKey, string titleFallback, string descriptionKey, string descriptionFallback)
		{
			if (!(_instance == null))
			{
				_instance.UpdateLocalizedText(titleKey, titleFallback, descriptionKey, descriptionFallback);
			}
		}

		public static void Hide()
		{
			if (!(_instance == null))
			{
				_instance.Pop();
			}
		}

		public static void ForceHide()
		{
			if (!(_instance == null))
			{
				_instance.ForceHide();
			}
		}
	}
}
