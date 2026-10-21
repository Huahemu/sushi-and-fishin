using System;
using UnityEngine.Events;

namespace PaperPixelGames.MyhreFramework.Animation
{
	[Serializable]
	public class AnimationEvent
	{
		public string eventName;

		public UnityEvent OnAnimationEvent;
	}
}
