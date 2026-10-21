using System;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	[Serializable]
	public struct MaterialSoundEntry
	{
		public SoundType SoundType;

		public HoldableMaterialType MaterialType;

		public AudioClip[] Clips;
	}
}
