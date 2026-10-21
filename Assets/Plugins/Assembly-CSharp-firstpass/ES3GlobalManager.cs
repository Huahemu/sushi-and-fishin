using System.Collections;
using UnityEngine;

public class ES3GlobalManager : MonoBehaviour
{
	private bool storeCache;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
	private static void Run()
	{
		GameObject obj = new GameObject("Easy Save 3 Global Manager");
		obj.AddComponent<ES3GlobalManager>();
		Object.DontDestroyOnLoad(obj);
	}

	public IEnumerator Start()
	{
		while (true)
		{
			yield return new WaitForEndOfFrame();
			if ((ES3Settings.defaultSettings.location == ES3.Location.Cache && ES3Settings.defaultSettings.storeCacheAtEndOfEveryFrame) || storeCache)
			{
				ES3File.StoreAll();
				storeCache = false;
			}
		}
	}

	private void OnApplicationQuit()
	{
		if (ES3Settings.defaultSettings.storeCacheOnApplicationQuit)
		{
			storeCache = true;
		}
	}

	private void OnApplicationPause(bool paused)
	{
		if ((ES3Settings.defaultSettings.storeCacheOnApplicationPause || (Application.isMobilePlatform && ES3Settings.defaultSettings.storeCacheOnApplicationQuit)) & paused)
		{
			storeCache = true;
		}
	}
}
