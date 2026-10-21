using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace ECM2.Walkthrough.Ex31
{
	public class AnimationController : MonoBehaviour
	{
		private static readonly int Swim = Animator.StringToHash("Swim");

		private static readonly int SwimUp = Animator.StringToHash("SwimUp");

		private static readonly int SwimDown = Animator.StringToHash("SwimDown");

		private static readonly int Forward = Animator.StringToHash("Forward");

		private static readonly int Turn = Animator.StringToHash("Turn");

		private static readonly int Ground = Animator.StringToHash("OnGround");

		private static readonly int Crouch = Animator.StringToHash("Crouch");

		private static readonly int Jump = Animator.StringToHash("Jump");

		private static readonly int JumpLeg = Animator.StringToHash("JumpLeg");

		public Character Character;

		public Animator TPSAnimator;

		public Animator FPSAnimator;

		private PPG_Player _player;

		private void Awake()
		{
			_player = GetComponentInParent<PPG_Player>();
		}

		private void OnValidate()
		{
			if (Character == null)
			{
				Character = GetComponentInParent<Character>();
			}
			if (Character != null && TPSAnimator == null)
			{
				TPSAnimator = Character.GetAnimator();
			}
			if (GetComponentInParent<PPG_Player>() != null && FPSAnimator == null)
			{
				UtilityMethods.Log("[OnValidate] FPSAnimator is null!", LogType.Error);
			}
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			int num;
			int num2;
			if (_player != null)
			{
				num = (_player.IsLocomotionAnimationSuppressed ? 1 : 0);
				if (num != 0)
				{
					num2 = 0;
					goto IL_0033;
				}
			}
			else
			{
				num = 0;
			}
			num2 = (Character.IsSwimming() ? 1 : 0);
			goto IL_0033;
			IL_0033:
			bool flag = (byte)num2 != 0;
			FPSAnimator.SetBool(Swim, flag);
			TPSAnimator.SetBool(Swim, flag);
			TPSAnimator.SetBool(SwimUp, flag && Character.jumpInputPressed);
			TPSAnimator.SetBool(SwimDown, flag && Character.crouchInputPressed);
			Vector3 forwardVector = Character.GetForwardVector();
			Vector3 rightVector = Character.GetRightVector();
			Vector3 vector = ((num != 0) ? Vector3.zero : Character.GetMovementDirection());
			float num3 = Vector3.Dot(vector.normalized, forwardVector);
			float num4;
			if (flag)
			{
				bool flag2 = Character.IsSprinting && num3 > 0f;
				num4 = num3 * (flag2 ? 2f : 1f);
			}
			else
			{
				num4 = num3 * (Character.IsSprinting ? 1f : 0.5f);
			}
			float value = Vector3.Dot(vector.normalized, rightVector * (Character.IsSprinting ? 1f : 0.5f));
			TPSAnimator.SetFloat(Forward, num4, 0.1f, deltaTime);
			TPSAnimator.SetFloat(Turn, value, 0.1f, deltaTime);
			FPSAnimator.SetBool(Ground, Character.IsGrounded());
			TPSAnimator.SetBool(Ground, Character.IsGrounded());
			TPSAnimator.SetBool(Crouch, Character.IsCrouched());
			if (Character.IsFalling())
			{
				TPSAnimator.SetFloat(Jump, Character.GetVelocity().y, 0.1f, deltaTime);
			}
			float value2 = ((Mathf.Repeat(TPSAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime + 0.2f, 1f) < 0.5f) ? 1f : (-1f)) * num4;
			if (Character.IsGrounded())
			{
				TPSAnimator.SetFloat(JumpLeg, value2);
			}
		}

		private void SyncFPSAnimator(float forward, float turn, float jump, float jumpLeg)
		{
			if (!(FPSAnimator == null))
			{
				FPSAnimator.SetBool(Swim, TPSAnimator.GetBool(Swim));
				FPSAnimator.SetBool(SwimUp, TPSAnimator.GetBool(SwimUp));
				FPSAnimator.SetBool(SwimDown, TPSAnimator.GetBool(SwimDown));
				FPSAnimator.SetBool(Ground, TPSAnimator.GetBool(Ground));
				FPSAnimator.SetBool(Crouch, TPSAnimator.GetBool(Crouch));
				FPSAnimator.SetFloat(Forward, forward);
				FPSAnimator.SetFloat(Turn, turn);
				if (!Character.IsGrounded())
				{
					FPSAnimator.SetFloat(Jump, jump);
				}
				FPSAnimator.SetFloat(JumpLeg, jumpLeg);
			}
		}
	}
}
