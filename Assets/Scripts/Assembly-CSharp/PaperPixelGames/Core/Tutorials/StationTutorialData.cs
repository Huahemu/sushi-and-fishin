using UnityEngine;
using UnityEngine.Video;

namespace PaperPixelGames.Core.Tutorials
{
	[CreateAssetMenu(fileName = "StationTutorialData", menuName = "Data/Tutorial/Station Tutorial Data")]
	public sealed class StationTutorialData : ScriptableObject
	{
		[field: SerializeField]
		public StationTutorialId TutorialId { get; private set; }

		[field: SerializeField]
		public string TitleLocalizationKey { get; private set; }

		[field: SerializeField]
		public string BodyLocalizationKey { get; private set; }

		[field: SerializeField]
		public string Title { get; private set; }

		[field: TextArea(4, 10)]
		[field: SerializeField]
		public string Body { get; private set; }

		[field: SerializeField]
		public VideoClip VideoClip { get; private set; }

		public string SeenKey => $"Tutorial.Station.{TutorialId}.AutoRepairShown";
	}
}
