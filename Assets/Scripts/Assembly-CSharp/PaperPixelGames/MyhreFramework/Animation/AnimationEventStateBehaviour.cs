using UnityEngine;

namespace PaperPixelGames.MyhreFramework.Animation
{
	public class AnimationEventStateBehaviour : StateMachineBehaviour
	{
		public enum TriggerPhase
		{
			NormalizedTime = 0,
			StateEnter = 1,
			StateExit = 2
		}

		public string eventName;

		public TriggerPhase triggerPhase;

		[Range(0f, 1f)]
		public float triggerTime;

		private bool _hasTriggered;

		private AnimationEventReceiver _receiver;

		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			_hasTriggered = false;
			_receiver = animator.GetComponent<AnimationEventReceiver>();
			if (triggerPhase == TriggerPhase.StateEnter)
			{
				NotifyReceiver();
				_hasTriggered = true;
			}
		}

		public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (triggerPhase == TriggerPhase.NormalizedTime)
			{
				float num = stateInfo.normalizedTime % 1f;
				if (!_hasTriggered && num >= triggerTime)
				{
					NotifyReceiver();
					_hasTriggered = true;
				}
			}
		}

		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (triggerPhase == TriggerPhase.StateExit && !_hasTriggered)
			{
				NotifyReceiver();
				_hasTriggered = true;
			}
		}

		private void NotifyReceiver()
		{
			if (_receiver != null)
			{
				_receiver.OnAnimationEventTriggered(eventName);
			}
		}
	}
}
