using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class OutfitHeightChange : MonoBehaviour, IOutfitExtension
	{
		private OutfitSystem outfitSystem;

		[SerializeField]
		private float HeightOffset;

		[Header("Heel Options")]
		[SerializeField]
		private bool heelEnabled;

		[SerializeField]
		private string animParameter = "HeelHeight";

		[SerializeField]
		private string blendName = "AnimShape_HeelHeight";

		[Range(0f, 1f)]
		[SerializeField]
		private float heelHeight;

		[SerializeField]
		private float heelHeightOffset;

		public void Execute(OutfitSystem outfitSystem, Outfit outfit)
		{
			this.outfitSystem = outfitSystem;
			if (outfitSystem == null)
			{
				return;
			}
			Animator animator = outfitSystem.animator;
			if (outfit == null)
			{
				return;
			}
			if ((bool)animator && heelEnabled)
			{
				if (animator.hasRootMotion)
				{
					return;
				}
				AnimatorControllerParameter[] parameters = animator.parameters;
				foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
				{
					if (animatorControllerParameter.name == animParameter)
					{
						animator.SetFloat(animatorControllerParameter.name, heelHeight);
						if (outfit.skinnedRenderer == null)
						{
							return;
						}
						int blendShapeIndex = outfit.skinnedRenderer.sharedMesh.GetBlendShapeIndex(blendName);
						if (blendShapeIndex == -1)
						{
							return;
						}
						string[] array = blendName.Split(".");
						if (array.Length > 1)
						{
							blendName = array[1];
						}
						outfit.skinnedRenderer.SetBlendShapeWeight(blendShapeIndex, heelHeight * 100f);
					}
				}
			}
			outfitSystem.AddHeightSource(base.gameObject, heelHeightOffset);
			outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Combine(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnRigChanged));
		}

		public void OnRigChanged(SkinnedMeshRenderer smr)
		{
			if (!outfitSystem)
			{
				return;
			}
			AnimatorControllerParameter[] parameters = outfitSystem.animator.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.name == animParameter)
				{
					outfitSystem.animator.SetFloat(animatorControllerParameter.name, heelHeight);
				}
			}
		}

		private void OnDestroy()
		{
			if ((bool)outfitSystem)
			{
				OutfitSystem obj = outfitSystem;
				obj.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Remove(obj.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnRigChanged));
				outfitSystem.RemoveHeightSource(base.gameObject, HeightOffset);
				outfitSystem.SetHeight();
			}
		}

		public string GetID()
		{
			return "HeightChange";
		}

		public void Initalize(OutfitSystem outfitSystem, Outfit outfit)
		{
		}

		public object GetValue()
		{
			return null;
		}

		public Type GetValueType()
		{
			return null;
		}
	}
}
