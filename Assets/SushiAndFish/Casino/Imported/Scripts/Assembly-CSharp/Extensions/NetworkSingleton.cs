namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

namespace Extensions
{
	public class NetworkSingleton<T> : NetworkBehaviour where T : Component
	{
		private static T _instance;

		private static bool _hadInstance;

        public static bool TryGetInstance(out T instance)
        {
            instance = _instance;
            return instance != null;
        }

		public static T Instance
		{
			get
			{
				if (_instance == null && Application.isPlaying && !_hadInstance)
				{
					_instance = Object.FindAnyObjectByType<T>();
					if (_instance == null)
					{
						Debug.LogError("No instance for: " + typeof(T).Name);
					}
					else
					{
						_hadInstance = true;
					}
				}
				return _instance;
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
				_hadInstance = true;
			}
			OnAwake();
		}

		protected virtual void OnAwake()
		{
		}

		protected virtual void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		public override bool Weaved()
		{
			return true;
		}
	}
}

}
