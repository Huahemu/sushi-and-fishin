using UnityEngine;

namespace Threepeat
{
	public class AnimatorStateSetter : MonoBehaviour
	{
		public AnimationClip stateToPlay;

		public bool resetOnComplete;

		public bool returnToInitialPositionOnReset = true;

		public bool sprintFirst;

		public float animationTimeOverride = -1f;

		private Vector3 initialPosition;

		private Quaternion initialRotation;

		private float currentAnimStartTime;

		private Animator animator;

		private float TIME_TO_PLAY = 10f;

		private void Start()
		{
			initialPosition = base.transform.position;
			initialRotation = base.transform.rotation;
			animator = GetComponent<Animator>();
			animator.Play(stateToPlay.name, -1, 0f);
			currentAnimStartTime = Time.time;
			TIME_TO_PLAY = stateToPlay.length;
			if (animationTimeOverride > 0f)
			{
				TIME_TO_PLAY = animationTimeOverride;
			}
		}

		private void Update()
		{
			if (resetOnComplete && Time.time - currentAnimStartTime > TIME_TO_PLAY)
			{
				if (returnToInitialPositionOnReset)
				{
					base.transform.position = initialPosition;
					base.transform.rotation = initialRotation;
				}
				animator.Play(stateToPlay.name, -1, 0f);
				currentAnimStartTime = Time.time;
			}
		}
	}
}
