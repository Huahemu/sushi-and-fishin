using System;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Framework.Patterns
{
	public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T _instance;

		public virtual SingletonScope Scope => SingletonScope.Application;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = UnityEngine.Object.FindFirstObjectByType<T>();
					if (_instance == null)
					{
						UtilityMethods.Log("[Singleton] Instance is null!", LogType.Warning);
					}
				}
				return _instance;
			}
		}

		public static bool TryGetInstance(out T instance)
		{
			instance = _instance;
			return instance != null;
		}

		protected virtual void Awake()
		{
			if (_instance == null)
			{
				_instance = this as T;
				if (Scope == SingletonScope.Application)
				{
					UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
				}
			}
			else if (_instance != this)
			{
				throw new Exception("[" + typeof(T).Name + "]: Bootstrap order?");
			}
		}
	}
}
