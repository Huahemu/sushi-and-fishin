using System;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public static class SurfPosePlacement
	{
		public const float PaddlePeriod = 1.4f;

		public static Quaternion BodyRotation(float standing)
		{
			return Quaternion.Euler(90f * (1f - standing), 90f * standing, 0f);
		}

		public static float PaddlePhase(double elapsed)
		{
			return (float)(elapsed / 1.399999976158142 - Math.Floor(elapsed / 1.399999976158142));
		}

		public static long StrokeIndex(double elapsed)
		{
			return (long)Math.Floor((elapsed - 0.27999999523162844) / 0.699999988079071);
		}

		public static Vector3 PaddleHand(float phase, float side, float halfWidth)
		{
			phase = Mathf.Repeat(phase, 1f);
			float num = 0f;
			float y;
			float z;
			if (phase < 0.2f)
			{
				float t = Mathf.SmoothStep(0f, 1f, phase / 0.2f);
				y = Mathf.Lerp(0.1f, -0.2f, t);
				z = Mathf.Lerp(0.56f, 0.48f, t);
			}
			else if (phase < 0.6f)
			{
				float t2 = Mathf.SmoothStep(0f, 1f, (phase - 0.2f) / 0.4f);
				y = -0.2f;
				z = Mathf.Lerp(0.48f, -0.08f, t2);
			}
			else
			{
				float num2 = (phase - 0.6f) / 0.4f;
				y = Mathf.Lerp(-0.2f, 0.1f, Mathf.SmoothStep(0f, 1f, num2)) + Mathf.Sin(num2 * MathF.PI) * 0.13f;
				z = Mathf.Lerp(-0.08f, 0.56f, Mathf.SmoothStep(0f, 1f, num2));
				num = Mathf.Sin(num2 * MathF.PI) * 0.1f;
			}
			return new Vector3(side * (halfWidth + 0.16f + num), y, z);
		}

		public static void Align(Animator animator, Vector3 anchor, Vector3 up, float standingBlend)
		{
			if (!(animator == null) && animator.isHuman)
			{
				Transform boneTransform = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
				Transform boneTransform2 = animator.GetBoneTransform(HumanBodyBones.RightFoot);
				Transform boneTransform3 = animator.GetBoneTransform(HumanBodyBones.Hips);
				Transform boneTransform4 = animator.GetBoneTransform(HumanBodyBones.Chest);
				if (!(boneTransform == null) && !(boneTransform2 == null) && !(boneTransform3 == null) && !(boneTransform4 == null))
				{
					Vector3 vector = Vector3.Lerp((boneTransform3.position + boneTransform4.position) * 0.5f, (boneTransform.position + boneTransform2.position) * 0.5f, standingBlend);
					Vector3 vector2 = anchor + up * Mathf.Lerp(0.12f, 0.06f, standingBlend);
					animator.transform.position += vector2 - vector;
				}
			}
		}
	}
}
