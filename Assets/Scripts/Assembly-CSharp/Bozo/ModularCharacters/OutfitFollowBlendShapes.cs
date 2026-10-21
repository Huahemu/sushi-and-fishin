using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class OutfitFollowBlendShapes : MonoBehaviour, IOutfitExtension
	{
		private OutfitSystem system;

		private SkinnedMeshRenderer[] mesh;

		private SkinnedMeshRenderer followTarget;

		[SerializeField]
		private OutfitType follow;

		[SerializeField]
		private List<Vector2> shapes = new List<Vector2>();

		private bool initalized;

		private void OnDisable()
		{
			OutfitSystem outfitSystem = system;
			outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnNewSetUpHead));
		}

		private void Init()
		{
		}

		private void OnNewSetUpHead(Outfit outfit)
		{
			if (!(outfit == null) && !(outfit.Type != follow) && (bool)outfit.skinnedRenderer)
			{
				followTarget = outfit.skinnedRenderer;
				SetUp();
			}
		}

		private void SetUp()
		{
			mesh = GetComponentsInChildren<SkinnedMeshRenderer>();
			if (mesh == null)
			{
				return;
			}
			string blendShapeName = followTarget.sharedMesh.GetBlendShapeName(0);
			string[] array = blendShapeName.Split(".");
			blendShapeName = ((array.Length <= 1) ? "" : (array[0] + "."));
			shapes.Clear();
			SkinnedMeshRenderer[] array2 = mesh;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array2)
			{
				for (int j = 0; j < skinnedMeshRenderer.sharedMesh.blendShapeCount; j++)
				{
					string text = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(j);
					array = text.Split(".");
					if (array.Length > 1)
					{
						text = array[1];
					}
					int blendShapeIndex = followTarget.sharedMesh.GetBlendShapeIndex(blendShapeName + text);
					if (blendShapeIndex != -1)
					{
						shapes.Add(new Vector2(j, blendShapeIndex));
					}
				}
			}
		}

		private void Update()
		{
			if (followTarget == null)
			{
				return;
			}
			for (int i = 0; i < shapes.Count; i++)
			{
				SkinnedMeshRenderer[] array = mesh;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
				{
					if (skinnedMeshRenderer.GetBlendShapeWeight((int)shapes[i].x) != followTarget.GetBlendShapeWeight((int)shapes[i].y))
					{
						skinnedMeshRenderer.SetBlendShapeWeight((int)shapes[i].x, followTarget.GetBlendShapeWeight((int)shapes[i].y));
					}
				}
			}
		}

		public string GetID()
		{
			return "BlendShapeFollow";
		}

		public string GetUniqueID()
		{
			return base.name + "BlendShapeFollow";
		}

		public void Initalize(OutfitSystem outfitSystem, Outfit outfit)
		{
			if (initalized)
			{
				return;
			}
			system = outfitSystem;
			if (!(system == null))
			{
				initalized = true;
				OutfitSystem outfitSystem2 = system;
				outfitSystem2.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfitSystem2.OnOutfitChanged, new UnityAction<Outfit>(OnNewSetUpHead));
				mesh = outfit.skinnedRenderers;
				Outfit outfit2 = system.GetOutfit(follow);
				if (!(outfit2 == null) && !(outfit2.skinnedRenderer == null))
				{
					followTarget = outfit2.skinnedRenderer;
					SetUp();
				}
			}
		}

		public void Execute(OutfitSystem outfitSystem, Outfit outfit)
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
