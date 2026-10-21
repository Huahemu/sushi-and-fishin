using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	[DefaultExecutionOrder(1000)]
	public sealed class FishingRodReelIKController : MonoBehaviour
	{
		private sealed class ViewRig
		{
			public Transform AnimatorRoot;

			public Transform UpperArm;

			public Transform LowerArm;

			public Transform Hand;

			public Transform SourceTarget;

			public Vector3 AutomaticHintLocalPosition;

			public float Weight;
		}

		private const float IK_EPSILON = 0.0001f;

		private PPG_Player _player;

		private FishingRodData _data;

		private ViewRig _fpsRig;

		private ViewRig _tpsRig;

		private bool _reeling;

		private bool _initialized;

		public void Initialize(PPG_Player player, FishingRodData data)
		{
			if (!(player == null) && !(data == null))
			{
				_player = player;
				_data = data;
				if (!_initialized)
				{
					_fpsRig = (_player.IsOwner ? CreateViewRig(useFpsAnimator: true, "FPS") : null);
					_tpsRig = CreateViewRig(useFpsAnimator: false, "TPS");
					_initialized = true;
				}
			}
		}

		public void BindTargets(Transform fpsTarget, Transform tpsTarget)
		{
			if (_fpsRig != null)
			{
				_fpsRig.SourceTarget = fpsTarget;
			}
			if (_tpsRig != null)
			{
				_tpsRig.SourceTarget = tpsTarget;
			}
		}

		public void SetReeling(bool reeling)
		{
			_reeling = reeling;
		}

		private void LateUpdate()
		{
			if (_initialized && !(_data == null))
			{
				UpdateViewRig(_fpsRig);
				UpdateViewRig(_tpsRig);
			}
		}

		private void OnDisable()
		{
			ResetViewRig(_fpsRig);
			ResetViewRig(_tpsRig);
		}

		private ViewRig CreateViewRig(bool useFpsAnimator, string viewName)
		{
			if (!_player.TryGetFishingRodReelRig(useFpsAnimator, out var animator, out var upperArm, out var lowerArm, out var hand))
			{
				UtilityMethods.Log("[FishingRodReelIK] " + viewName + " left arm rig could not be resolved. IK is disabled for this view.", LogType.Warning);
				return null;
			}
			Vector3 position = ResolveAutomaticHintPosition(animator.transform, upperArm, lowerArm, hand);
			return new ViewRig
			{
				AnimatorRoot = animator.transform,
				UpperArm = upperArm,
				LowerArm = lowerArm,
				Hand = hand,
				AutomaticHintLocalPosition = animator.transform.InverseTransformPoint(position)
			};
		}

		private void UpdateViewRig(ViewRig viewRig)
		{
			if (viewRig != null && !(viewRig.UpperArm == null) && !(viewRig.LowerArm == null) && !(viewRig.Hand == null))
			{
				bool flag = viewRig.SourceTarget != null && viewRig.SourceTarget.gameObject.activeInHierarchy;
				float target = ((_reeling & flag) ? Mathf.Clamp01(_data.ReelHandIkWeight) : 0f);
				viewRig.Weight = Mathf.MoveTowards(viewRig.Weight, target, Mathf.Max(0.01f, _data.ReelHandIkBlendSpeed) * Time.deltaTime);
				if (flag && !(viewRig.Weight <= 0.0001f))
				{
					Vector3 targetPosition = ResolveSoftReachTarget(viewRig, viewRig.SourceTarget.position);
					Vector3 hintPosition = viewRig.AnimatorRoot.TransformPoint(viewRig.AutomaticHintLocalPosition + _data.ReelHandIkHintOffset);
					SolveTwoBoneIk(viewRig, targetPosition, viewRig.SourceTarget.rotation, hintPosition, viewRig.Weight);
				}
			}
		}

		private Vector3 ResolveSoftReachTarget(ViewRig viewRig, Vector3 desiredPosition)
		{
			float num = Vector3.Distance(viewRig.UpperArm.position, viewRig.LowerArm.position);
			float num2 = Vector3.Distance(viewRig.LowerArm.position, viewRig.Hand.position);
			float num3 = num + num2;
			if (num3 <= 0.0001f)
			{
				return desiredPosition;
			}
			Vector3 vector = desiredPosition - viewRig.UpperArm.position;
			float magnitude = vector.magnitude;
			if (magnitude <= 0.0001f)
			{
				return desiredPosition;
			}
			float num4 = num3 * Mathf.Clamp(_data.ReelHandIkMaxReach, 0.5f, 0.99f);
			float num5 = num3 * Mathf.Clamp(_data.ReelHandIkSoftReach, 0f, 0.25f);
			if (num5 <= 0.0001f)
			{
				return viewRig.UpperArm.position + vector.normalized * Mathf.Min(magnitude, num4);
			}
			num5 = Mathf.Min(num5, num4 * 0.5f);
			float num6 = num4 - num5;
			if (magnitude <= num6)
			{
				return desiredPosition;
			}
			float num7 = magnitude - num6;
			float num8 = num6 + num5 * (1f - Mathf.Exp((0f - num7) / num5));
			return viewRig.UpperArm.position + vector.normalized * num8;
		}

		private void SolveTwoBoneIk(ViewRig viewRig, Vector3 targetPosition, Quaternion targetRotation, Vector3 hintPosition, float weight)
		{
			Transform upperArm = viewRig.UpperArm;
			Transform lowerArm = viewRig.LowerArm;
			Transform hand = viewRig.Hand;
			float t = Mathf.Clamp01(_data.ReelHandIkPositionWeight * weight);
			float t2 = Mathf.Clamp01(_data.ReelHandIkRotationWeight * weight);
			float hintWeight = Mathf.Clamp01(_data.ReelHandIkHintWeight * weight);
			Vector3 position = upperArm.position;
			Vector3 position2 = lowerArm.position;
			Vector3 position3 = hand.position;
			Vector3 vector = Vector3.Lerp(position3, targetPosition, t);
			Quaternion rotation = Quaternion.Lerp(hand.rotation, targetRotation, t2);
			Vector3 lhs = position2 - position;
			Vector3 rhs = position3 - position2;
			Vector3 vector2 = position3 - position;
			Vector3 vector3 = vector - position;
			float magnitude = lhs.magnitude;
			float magnitude2 = rhs.magnitude;
			float magnitude3 = vector2.magnitude;
			float magnitude4 = vector3.magnitude;
			if (!(magnitude <= 0.0001f) && !(magnitude2 <= 0.0001f) && !(magnitude4 <= 0.0001f))
			{
				Vector3 vector4 = Vector3.Cross(lhs, rhs);
				if (vector4.sqrMagnitude <= 0.0001f)
				{
					vector4 = Vector3.Cross(hintPosition - position, rhs);
				}
				if (vector4.sqrMagnitude <= 0.0001f)
				{
					vector4 = Vector3.Cross(vector3, rhs);
				}
				if (vector4.sqrMagnitude <= 0.0001f)
				{
					vector4 = viewRig.AnimatorRoot.up;
				}
				float num = TriangleAngle(magnitude3, magnitude, magnitude2);
				float num2 = TriangleAngle(magnitude4, magnitude, magnitude2);
				Quaternion quaternion = Quaternion.AngleAxis((num - num2) * 57.29578f, vector4.normalized);
				lowerArm.rotation = quaternion * lowerArm.rotation;
				Vector3 fromDirection = hand.position - position;
				if (fromDirection.sqrMagnitude > 0.0001f)
				{
					upperArm.rotation = Quaternion.FromToRotation(fromDirection, vector3) * upperArm.rotation;
				}
				ApplyHint(viewRig, hintPosition, hintWeight, magnitude + magnitude2);
				hand.rotation = rotation;
			}
		}

		private static void ApplyHint(ViewRig viewRig, Vector3 hintPosition, float hintWeight, float maximumReach)
		{
			if (hintWeight <= 0.0001f)
			{
				return;
			}
			Vector3 position = viewRig.UpperArm.position;
			Vector3 position2 = viewRig.LowerArm.position;
			Vector3 vector = viewRig.Hand.position - position;
			float sqrMagnitude = vector.sqrMagnitude;
			if (!(sqrMagnitude <= 0.0001f))
			{
				Vector3 vector2 = vector / Mathf.Sqrt(sqrMagnitude);
				Vector3 vector3 = position2 - position;
				Vector3 vector4 = hintPosition - position;
				Vector3 fromDirection = vector3 - vector2 * Vector3.Dot(vector3, vector2);
				Vector3 toDirection = vector4 - vector2 * Vector3.Dot(vector4, vector2);
				if (!(fromDirection.sqrMagnitude <= maximumReach * maximumReach * 0.001f) && !(toDirection.sqrMagnitude <= 0.0001f))
				{
					Quaternion b = Quaternion.FromToRotation(fromDirection, toDirection);
					Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, b, hintWeight);
					viewRig.UpperArm.rotation = quaternion * viewRig.UpperArm.rotation;
				}
			}
		}

		private static Vector3 ResolveAutomaticHintPosition(Transform animatorRoot, Transform upperArm, Transform lowerArm, Transform hand)
		{
			Vector3 onNormal = hand.position - upperArm.position;
			Vector3 vector = ((onNormal.sqrMagnitude > 0.0001f) ? (upperArm.position + Vector3.Project(lowerArm.position - upperArm.position, onNormal)) : lowerArm.position);
			Vector3 vector2 = lowerArm.position - vector;
			if (vector2.sqrMagnitude < 0.0001f)
			{
				vector2 = -animatorRoot.forward;
			}
			float num = Mathf.Max(0.15f, Vector3.Distance(upperArm.position, lowerArm.position));
			return lowerArm.position + vector2.normalized * num;
		}

		private static float TriangleAngle(float oppositeLength, float firstSideLength, float secondSideLength)
		{
			float num = firstSideLength * secondSideLength * 2f;
			if (num <= 0.0001f)
			{
				return 0f;
			}
			return Mathf.Acos(Mathf.Clamp((firstSideLength * firstSideLength + secondSideLength * secondSideLength - oppositeLength * oppositeLength) / num, -1f, 1f));
		}

		private static void ResetViewRig(ViewRig viewRig)
		{
			if (viewRig != null)
			{
				viewRig.Weight = 0f;
			}
		}
	}
}
