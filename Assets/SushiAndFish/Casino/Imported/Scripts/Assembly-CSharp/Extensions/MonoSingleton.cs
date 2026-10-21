namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace Extensions
{
	public class MonoSingleton<T> : MonoBehaviour where T : Component
	{
		private static T _instance;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Object.FindAnyObjectByType<T>();
				}
				return _instance;
			}
			protected set
			{
				_instance = value;
			}
		}

		protected void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Object.Destroy(base.gameObject);
			}
			else
			{
				_instance = this as T;
			}
			OnAwake();
		}

		protected virtual void OnAwake()
		{
		}
	}
}

}
