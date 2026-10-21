using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Michsky.LSS
{
	[AddComponentMenu("Loading Screen Studio/LSS Manager")]
	public class LSS_Manager : MonoBehaviour
	{
		public enum LoadingMode
		{
			Single = 0,
			Additive = 1
		}

		public LSS_LoadingScreen LoadingScreen;

		public LoadingMode loadingMode;

		public string presetName = "Default";

		public bool enableTrigger;

		public bool onTriggerExit;

		public bool loadWithTag;

		public bool startLoadingAtStart;

		public string objectTag;

		public string sceneName;

		[Range(0f, 10f)]
		public float audioFadeDuration = 2f;

		public List<AudioSource> audioSources = new List<AudioSource>();

		public Object[] loadingScreens;

		public int selectedLoadingIndex;

		public int selectedTagIndex;

		public UnityEvent onLoadingStart;

		public List<GameObject> dontDestroyOnLoad = new List<GameObject>();
	}
}
