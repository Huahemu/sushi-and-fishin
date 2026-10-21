using System;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	[Serializable]
	public class LinkedColorSets
	{
		public OutfitType linkedType;

		[Range(1f, 9f)]
		public int linkedChannelRange;
	}
}
