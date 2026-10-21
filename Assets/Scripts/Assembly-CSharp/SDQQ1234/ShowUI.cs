using UnityEngine;
using UnityEngine.UI;

namespace SDQQ1234
{
	public class ShowUI : MonoBehaviour
	{
		public Text fpsUI;

		private float deltaTime;

		private void Update()
		{
			deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
			float num = deltaTime * 1000f;
			float num2 = 1f / deltaTime;
			fpsUI.text = $"{num:0.0} ms ({num2:0.} fps)";
		}

		private void Start()
		{
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = 240;
		}
	}
}
