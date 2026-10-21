using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.LSS
{
	[ExecuteInEditMode]
	[AddComponentMenu("Loading Screen Studio/LSS Spinner")]
	public class LSS_Spinner : MonoBehaviour
	{
		[Header("Settings")]
		public LSS_LoadingScreen loadingScreen;

		[Header("Resources")]
		public List<Image> foreground = new List<Image>();

		public List<Image> background = new List<Image>();

		private void OnEnable()
		{
			if (loadingScreen == null)
			{
				try
				{
					loadingScreen = base.gameObject.GetComponentInParent<LSS_LoadingScreen>();
				}
				catch
				{
					Debug.Log("<b>[LSS]</b> No Loading Screen found.", this);
				}
			}
		}

		public void UpdateValues()
		{
			for (int i = 0; i < foreground.Count; i++)
			{
				foreground[i].color = loadingScreen.spinnerColor;
			}
			for (int j = 0; j < background.Count; j++)
			{
				background[j].color = new Color(loadingScreen.spinnerColor.r, loadingScreen.spinnerColor.g, loadingScreen.spinnerColor.b, 0.08f);
			}
		}
	}
}
