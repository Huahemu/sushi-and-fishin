using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PaperPixelGames.MyhreFramework.Animation
{
	public class AnimationEventReceiver : MonoBehaviour
	{
		[SerializeField]
		private List<AnimationEvent> animationEvents = new List<AnimationEvent>();

		public void OnAnimationEventTriggered(string eventName)
		{
			animationEvents.Find((AnimationEvent se) => se.eventName == eventName)?.OnAnimationEvent?.Invoke();
		}

		public void AddListener(string eventName, UnityAction action)
		{
			if (!string.IsNullOrWhiteSpace(eventName) && action != null)
			{
				AnimationEvent animationEvent = animationEvents.Find((AnimationEvent se) => se.eventName == eventName);
				if (animationEvent == null)
				{
					animationEvent = new AnimationEvent
					{
						eventName = eventName,
						OnAnimationEvent = new UnityEvent()
					};
					animationEvents.Add(animationEvent);
				}
				AnimationEvent animationEvent2 = animationEvent;
				if (animationEvent2.OnAnimationEvent == null)
				{
					animationEvent2.OnAnimationEvent = new UnityEvent();
				}
				animationEvent.OnAnimationEvent.AddListener(action);
			}
		}

		public void RemoveListener(string eventName, UnityAction action)
		{
			if (!string.IsNullOrWhiteSpace(eventName) && action != null)
			{
				animationEvents.Find((AnimationEvent se) => se.eventName == eventName)?.OnAnimationEvent?.RemoveListener(action);
			}
		}
	}
}
