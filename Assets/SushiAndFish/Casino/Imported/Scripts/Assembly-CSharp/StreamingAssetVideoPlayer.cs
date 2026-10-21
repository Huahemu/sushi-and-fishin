namespace SushiAndFish.Casino.Original
{
using global::System.IO;
using global::UnityEngine;
using global::UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class StreamingAssetVideoPlayer : MonoBehaviour
{
	[SerializeField]
	private string fileNameWithoutExtension;

	[SerializeField]
	private string relativeFolder = "ScreensVideos";

	private void Awake()
	{
		VideoPlayer component = GetComponent<VideoPlayer>();
		string text = Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath, relativeFolder, fileNameWithoutExtension + ".webm");
		string text2 = Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath, relativeFolder, fileNameWithoutExtension + ".mp4");
		component.source = VideoSource.Url;
		component.clip = null;
		if (Application.platform == RuntimePlatform.LinuxPlayer && File.Exists(text))
		{
			component.url = text;
		}
		else if (File.Exists(text))
		{
			component.url = text;
		}
		else if (File.Exists(text2))
		{
			component.url = text2;
		}
	}
}

}
