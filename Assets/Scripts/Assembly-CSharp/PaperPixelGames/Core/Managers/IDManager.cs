using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	public class IDManager : Singleton<IDManager>
	{
		[SerializeField]
		private List<AudioClipSO> _sounds;

		[SerializeField]
		private GameDefaultSO _gameDefault;

		[SerializeField]
		private List<WholesaleSO> _wholesaleTest;

		public GameDefaultSO GameDefaultData => _gameDefault;

		public AudioClipSO GetSoundByType(SoundType type)
		{
			return _sounds.FirstOrDefault((AudioClipSO x) => x.Type == type);
		}
	}
}
