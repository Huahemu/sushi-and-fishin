using System;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfPoseDriver
	{
		private readonly Animator _animator;

		private readonly Transform[] _upper = new Transform[4];

		private readonly Transform[] _lower = new Transform[4];

		private readonly Transform[] _end = new Transform[4];

		private readonly Transform[] _toes = new Transform[2];

		public SurfPoseDriver(Animator animator)
		{
			_animator = animator;
			if (!(animator == null) && animator.isHuman)
			{
				HumanBodyBones[] array = new HumanBodyBones[4]
				{
					HumanBodyBones.LeftUpperArm,
					HumanBodyBones.RightUpperArm,
					HumanBodyBones.LeftUpperLeg,
					HumanBodyBones.RightUpperLeg
				};
				HumanBodyBones[] array2 = new HumanBodyBones[4]
				{
					HumanBodyBones.LeftLowerArm,
					HumanBodyBones.RightLowerArm,
					HumanBodyBones.LeftLowerLeg,
					HumanBodyBones.RightLowerLeg
				};
				HumanBodyBones[] array3 = new HumanBodyBones[4]
				{
					HumanBodyBones.LeftHand,
					HumanBodyBones.RightHand,
					HumanBodyBones.LeftFoot,
					HumanBodyBones.RightFoot
				};
				for (int i = 0; i < 4; i++)
				{
					_upper[i] = animator.GetBoneTransform(array[i]);
					_lower[i] = animator.GetBoneTransform(array2[i]);
					_end[i] = animator.GetBoneTransform(array3[i]);
				}
				_toes[0] = animator.GetBoneTransform(HumanBodyBones.LeftToes);
				_toes[1] = animator.GetBoneTransform(HumanBodyBones.RightToes);
			}
		}

		public void Apply(Vector3 anchor, Quaternion boardRotation, float halfWidth, float halfLength, float standing, float paddle, float phase, float turn, double time)
		{
			if (_animator == null || !_animator.isHuman)
			{
				return;
			}
			Vector3 vector = boardRotation * Vector3.up;
			Vector3 vector2 = boardRotation * Vector3.forward;
			Vector3 vector3 = boardRotation * Vector3.right;
			float angle = standing * (turn * -7f + Mathf.Sin((float)time * 1.7f) * 1.2f);
			_animator.transform.rotation = Quaternion.AngleAxis(angle, vector2) * _animator.transform.rotation;
			SurfPosePlacement.Align(_animator, anchor, vector, standing);
			_animator.transform.position += standing * (-vector * 0.1f - vector3 * 0.055f + vector2 * 0.035f);
			Transform boneTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
			if (boneTransform != null)
			{
				boneTransform.rotation = Quaternion.AngleAxis(-14f * standing, vector2) * Quaternion.AngleAxis(Mathf.Lerp(-22f, 8f, standing), vector3) * boneTransform.rotation;
			}
			float t = Mathf.Sin(standing * MathF.PI);
			for (int i = 0; i < 2; i++)
			{
				float num = ((i != 0) ? 1 : (-1));
				Vector3 a = new Vector3(num * (halfWidth + 0.1f), -0.12f, 0.32f);
				Vector3 b = SurfPosePlacement.PaddleHand(phase + (float)i * 0.5f, num, halfWidth);
				Vector3 a2 = Vector3.Lerp(a, b, paddle * (1f - standing));
				Vector3 b2 = new Vector3(num * Mathf.Max(0.15f, halfWidth * 0.85f), 0.02f, 0.25f);
				a2 = Vector3.Lerp(a2, b2, t);
				float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, standing));
				Solve(i, anchor + boardRotation * a2, anchor + vector3 * num * 1.2f + vector * 0.3f, weight);
				float num2 = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, standing));
				if (_upper[i + 2] != null && _end[i + 2] != null && num2 > 0f)
				{
					Quaternion b3 = Quaternion.FromToRotation(_end[i + 2].position - _upper[i + 2].position, -vector2) * _upper[i + 2].rotation;
					_upper[i + 2].rotation = Quaternion.Slerp(_upper[i + 2].rotation, b3, num2);
				}
				float num3 = Mathf.Min(0.27f, halfLength * 0.32f);
				Vector3 vector4 = anchor + vector2 * (0f - num) * num3 + vector3 * num * 0.025f + vector * 0.055f;
				float num4 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, standing));
				Vector3 vector5 = ((i == 0) ? (vector3 + vector2).normalized : vector3);
				Solve(i + 2, vector4, vector4 + vector5 * 1.3f + vector * 0.4f, num4);
				if (_toes[i] != null && _end[i + 2] != null)
				{
					Vector3 fromDirection = _toes[i].position - _end[i + 2].position;
					Quaternion b4 = Quaternion.FromToRotation(fromDirection, -vector2 * fromDirection.magnitude) * _end[i + 2].rotation;
					_end[i + 2].rotation = Quaternion.Slerp(_end[i + 2].rotation, b4, num2);
					fromDirection = _toes[i].position - _end[i + 2].position;
					Vector3 toDirection = vector5 * fromDirection.magnitude - vector * 0.045f;
					Quaternion b5 = Quaternion.FromToRotation(fromDirection, toDirection) * _end[i + 2].rotation;
					_end[i + 2].rotation = Quaternion.Slerp(_end[i + 2].rotation, b5, num4);
				}
			}
		}

		private void Solve(int index, Vector3 target, Vector3 pole, float weight)
		{
			Transform transform = _upper[index];
			Transform transform2 = _lower[index];
			Transform transform3 = _end[index];
			if (transform == null || transform2 == null || transform3 == null || weight <= 0f)
			{
				return;
			}
			target = Vector3.Lerp(transform3.position, target, weight);
			Quaternion rotation = transform3.rotation;
			Vector3 position = transform.position;
			float num = Vector3.Distance(position, transform2.position);
			float num2 = Vector3.Distance(transform2.position, transform3.position);
			Vector3 vector = target - position;
			float num3 = Mathf.Clamp(vector.magnitude, Mathf.Abs(num - num2) + 0.001f, num + num2 - 0.001f);
			if (!(num < 0.001f) && !(num2 < 0.001f) && !(vector.sqrMagnitude < 1E-06f))
			{
				Vector3 normalized = vector.normalized;
				Vector3 normalized2 = Vector3.ProjectOnPlane(pole - position, normalized).normalized;
				if (normalized2.sqrMagnitude < 0.001f)
				{
					normalized2 = Vector3.Cross(normalized, Vector3.forward).normalized;
				}
				float num4 = (num * num + num3 * num3 - num2 * num2) / (2f * num3);
				Vector3 vector2 = position + normalized * num4 + normalized2 * Mathf.Sqrt(Mathf.Max(0f, num * num - num4 * num4));
				transform.rotation = Quaternion.FromToRotation(transform2.position - position, vector2 - position) * transform.rotation;
				transform2.rotation = Quaternion.FromToRotation(transform3.position - transform2.position, target - transform2.position) * transform2.rotation;
				transform3.rotation = rotation;
			}
		}
	}
}
