using UnityEngine;

namespace Michsky.MUIP
{
	public class MUIPInternalTools : MonoBehaviour
	{
		public static string modalWindowStateName = "Fade-out";

		public static string windowManagerStateName = "WM Window Out";

		public static float GetAnimatorClipLength(Animator _animator, string _clipName)
		{
			float result = -1f;
			RuntimeAnimatorController runtimeAnimatorController = _animator.runtimeAnimatorController;
			for (int i = 0; i < runtimeAnimatorController.animationClips.Length; i++)
			{
				if (runtimeAnimatorController.animationClips[i].name == _clipName)
				{
					result = runtimeAnimatorController.animationClips[i].length;
					break;
				}
			}
			return result;
		}
	}
}
