using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class Character : MonoBehaviour
	{
		[SerializeField]
		private Animator _Anim;

		[SerializeField]
		private BindingPoints _BindingPoints;

		[SerializeField]
		private Transform _Target;

		private AnimatorOverrideController _overrideController;

		public BindingPoints BindingPoints => _BindingPoints;

		private void Start()
		{
			if (_Anim.runtimeAnimatorController != null)
			{
				_overrideController = new AnimatorOverrideController(_Anim.runtimeAnimatorController);
				_Anim.runtimeAnimatorController = _overrideController;
			}
		}

		public void PlayAnimation(string clipId, AnimationClip clip)
		{
			if (_overrideController != null)
			{
				_overrideController[clipId] = clip;
				_Anim.SetTrigger("Play");
			}
		}

		public Vector3 GetTarget()
		{
			Vector3 normalized = (_Target.position - base.transform.position).normalized;
			if (Physics.Raycast(new Ray(base.transform.position, normalized), out var hitInfo, 100f))
			{
				return hitInfo.point;
			}
			return _Target.position;
		}
	}
}
