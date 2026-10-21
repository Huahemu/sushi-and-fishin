using Bozo.ModularCharacters;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[DisallowMultipleComponent]
	public sealed class ResidentBakedVisualBindings : MonoBehaviour
	{
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private OutfitSystem _outfitSystem;

		[SerializeField]
		private Transform _restaurantPropAnchor;

		public Animator Animator => _animator;

		public OutfitSystem OutfitSystem => _outfitSystem;

		public Transform RestaurantPropAnchor => _restaurantPropAnchor;

		public void InitializeFromHierarchy()
		{
			Animator[] componentsInChildren = GetComponentsInChildren<Animator>(includeInactive: true);
			_animator = FindCharacterAnimator(componentsInChildren);
			if (_outfitSystem == null)
			{
				_outfitSystem = GetComponentInChildren<OutfitSystem>(includeInactive: true);
			}
			if (_animator != null && _animator.isHuman)
			{
				_restaurantPropAnchor = _animator.GetBoneTransform(HumanBodyBones.RightHand);
			}
			DisableBakedBodyShapeModifiers();
		}

		private void DisableBakedBodyShapeModifiers()
		{
			BodyShapeModifier[] componentsInChildren = GetComponentsInChildren<BodyShapeModifier>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					componentsInChildren[i].enabled = false;
				}
			}
		}

		private static Animator FindCharacterAnimator(Animator[] animators)
		{
			Animator result = null;
			int num = int.MinValue;
			foreach (Animator animator in animators)
			{
				if (!(animator == null))
				{
					int num2 = 0;
					if (animator.avatar != null)
					{
						num2 += 10;
					}
					if (animator.isHuman)
					{
						num2 += 100;
					}
					if (animator.enabled)
					{
						num2 += 5;
					}
					if (animator.gameObject.activeInHierarchy)
					{
						num2 += 2;
					}
					if (num2 > num)
					{
						result = animator;
						num = num2;
					}
				}
			}
			return result;
		}
	}
}
