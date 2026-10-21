using UnityEngine;

namespace Nav3D.Common
{
	public static class Singleton<T> where T : MonoBehaviour
	{
		private static T m_Instance;

		private static readonly object m_Lock = new object();

		public static T Instance => GetInstance();

		public static T GetInstance(string _Name = null)
		{
			if (m_Instance == null)
			{
				lock (m_Lock)
				{
					if (m_Instance == null)
					{
						m_Instance = Object.FindObjectOfType<T>();
						if (m_Instance == null && !Application.isPlaying)
						{
							GameObject gameObject = GameObject.Find(_Name ?? typeof(T).FullName);
							if (gameObject != null)
							{
								m_Instance = gameObject.GetComponent<T>();
							}
						}
						if (m_Instance == null)
						{
							GameObject gameObject2 = new GameObject(_Name ?? typeof(T).FullName);
							if (Application.isPlaying)
							{
								Object.DontDestroyOnLoad(gameObject2);
							}
							else
							{
								gameObject2.hideFlags |= HideFlags.DontSave;
							}
							m_Instance = gameObject2.AddComponent<T>();
						}
					}
					return m_Instance;
				}
			}
			return m_Instance;
		}
	}
}
