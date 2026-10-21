using System;
using MagicaCloth2;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Player
{
	[DisallowMultipleComponent]
	public class PlayerRigBoneReferences : MonoBehaviour
	{
		[Serializable]
		public class RigBoneSet
		{
			[Header("Core Bones")]
			[SerializeField]
			private Transform _head;

			[SerializeField]
			private Transform _spine;

			[Header("Hands")]
			[SerializeField]
			private Transform _leftHand;

			[SerializeField]
			private Transform _rightHand;

			[Header("Feet")]
			[SerializeField]
			private Transform _leftFoot;

			[SerializeField]
			private Transform _rightFoot;

			public Transform Head => _head;

			public Transform Spine => _spine;

			public Transform LeftHand => _leftHand;

			public Transform RightHand => _rightHand;

			public Transform LeftFoot => _leftFoot;

			public Transform RightFoot => _rightFoot;

			public void Assign(Transform head, Transform spine, Transform leftHand, Transform rightHand, Transform leftFoot, Transform rightFoot)
			{
				_head = head;
				_spine = spine;
				_leftHand = leftHand;
				_rightHand = rightHand;
				_leftFoot = leftFoot;
				_rightFoot = rightFoot;
			}
		}

		[Header("Auto Assign Sources")]
		[SerializeField]
		private Transform _fpsCharacterRoot;

		[SerializeField]
		private Transform _tpsCharacterRoot;

		[Header("FPS Rig Bones")]
		[SerializeField]
		private RigBoneSet _fpsRigBones = new RigBoneSet();

		[Header("TPS Rig Bones")]
		[SerializeField]
		private RigBoneSet _tpsRigBones = new RigBoneSet();

		private static readonly string[] HeadNames = new string[3] { "Head", "mixamorig:Head", "Bip001 Head" };

		private static readonly string[] SpineNames = new string[7] { "Spine", "Spine1", "Spine_01", "Chest", "mixamorig:Spine", "mixamorig:Spine1", "Bip001 Spine" };

		private static readonly string[] LeftHandNames = new string[5] { "LeftHand", "Hand_L", "L_Hand", "mixamorig:LeftHand", "Bip001 L Hand" };

		private static readonly string[] RightHandNames = new string[5] { "RightHand", "Hand_R", "R_Hand", "mixamorig:RightHand", "Bip001 R Hand" };

		private static readonly string[] LeftFootNames = new string[5] { "LeftFoot", "Foot_L", "L_Foot", "mixamorig:LeftFoot", "Bip001 L Foot" };

		private static readonly string[] RightFootNames = new string[5] { "RightFoot", "Foot_R", "R_Foot", "mixamorig:RightFoot", "Bip001 R Foot" };

		public RigBoneSet FpsRig => _fpsRigBones;

		public RigBoneSet TpsRig => _tpsRigBones;

		public ColliderComponent TpsHeadMagicaCollider => ResolveMagicaCollider(_tpsRigBones?.Head);

		public ColliderComponent TpsSpineMagicaCollider => ResolveMagicaCollider(_tpsRigBones?.Spine);

		private void AutoAssignRigBones()
		{
			EnsureRigSets();
			bool flag = TryAssignRig(_fpsCharacterRoot, _fpsRigBones, "FPS");
			bool flag2 = TryAssignRig(_tpsCharacterRoot, _tpsRigBones, "TPS");
			if (!flag || !flag2)
			{
				UtilityMethods.Log($"[PlayerRigBoneReferences] Auto assign completed with missing source(s). FPS:{flag}, TPS:{flag2}", LogType.Warning);
			}
		}

		private void Reset()
		{
			EnsureRigSets();
		}

		private void OnValidate()
		{
			EnsureRigSets();
		}

		private void EnsureRigSets()
		{
			if (_fpsRigBones == null)
			{
				_fpsRigBones = new RigBoneSet();
			}
			if (_tpsRigBones == null)
			{
				_tpsRigBones = new RigBoneSet();
			}
		}

		private static bool TryAssignRig(Transform root, RigBoneSet target, string label)
		{
			if (root == null || target == null)
			{
				return false;
			}
			Animator componentInChildren = root.GetComponentInChildren<Animator>(includeInactive: true);
			Transform transform = ResolveBone(root, componentInChildren, HumanBodyBones.Head, HeadNames);
			Transform transform2 = ResolveBone(root, componentInChildren, HumanBodyBones.Spine, SpineNames);
			Transform transform3 = ResolveBone(root, componentInChildren, HumanBodyBones.LeftHand, LeftHandNames);
			Transform transform4 = ResolveBone(root, componentInChildren, HumanBodyBones.RightHand, RightHandNames);
			Transform transform5 = ResolveBone(root, componentInChildren, HumanBodyBones.LeftFoot, LeftFootNames);
			Transform transform6 = ResolveBone(root, componentInChildren, HumanBodyBones.RightFoot, RightFootNames);
			target.Assign(transform, transform2, transform3, transform4, transform5, transform6);
			bool flag = transform != null && transform2 != null && transform3 != null && transform4 != null && transform5 != null && transform6 != null;
			if (!flag)
			{
				UtilityMethods.Log("[PlayerRigBoneReferences] " + label + " rig auto assign missing bones. Root=" + root.name + ", Head=" + GetName(transform) + ", Spine=" + GetName(transform2) + ", LeftHand=" + GetName(transform3) + ", RightHand=" + GetName(transform4) + ", LeftFoot=" + GetName(transform5) + ", RightFoot=" + GetName(transform6), LogType.Warning);
			}
			return flag;
		}

		private static Transform ResolveBone(Transform root, Animator animator, HumanBodyBones humanoidBone, string[] fallbackNames)
		{
			if (animator != null && animator.isHuman)
			{
				Transform boneTransform = animator.GetBoneTransform(humanoidBone);
				if (boneTransform != null)
				{
					return boneTransform;
				}
			}
			return FindChildByNames(root, fallbackNames);
		}

		private static Transform FindChildByNames(Transform root, string[] names)
		{
			if (root == null || names == null)
			{
				return null;
			}
			for (int i = 0; i < names.Length; i++)
			{
				Transform transform = FindChildByNormalizedName(root, NormalizeBoneName(names[i]));
				if (transform != null)
				{
					return transform;
				}
			}
			return null;
		}

		private static Transform FindChildByNormalizedName(Transform root, string normalizedName)
		{
			if (root == null || string.IsNullOrEmpty(normalizedName))
			{
				return null;
			}
			if (NormalizeBoneName(root.name) == normalizedName)
			{
				return root;
			}
			for (int i = 0; i < root.childCount; i++)
			{
				Transform transform = FindChildByNormalizedName(root.GetChild(i), normalizedName);
				if (transform != null)
				{
					return transform;
				}
			}
			return null;
		}

		private static string NormalizeBoneName(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			return value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty)
				.Replace(".", string.Empty)
				.Replace(":", string.Empty)
				.ToLowerInvariant();
		}

		private static string GetName(Transform bone)
		{
			if (!(bone != null))
			{
				return "Missing";
			}
			return bone.name;
		}

		private static ColliderComponent ResolveMagicaCollider(Transform bone)
		{
			if (!(bone != null))
			{
				return null;
			}
			return bone.GetComponent<ColliderComponent>();
		}
	}
}
