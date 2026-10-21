using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public class PlacementSurface : MonoBehaviour
	{
		public BaseInteractable DynamicInteractable;

		[Header("Tags")]
		public SurfaceTags Tags;

		[Header("Placement Mode")]
		public PlacementMode Mode;

		[Header("Free Placement Settings")]
		public bool RequireSupport = true;

		public LayerMask SupportMask;

		[Range(0.5f, 1f)]
		public float SupportRatio = 0.8f;

		[Header("Height Clamp (Free Mode)")]
		public bool ClampY;

		public float ClampLocalY;

		[Header("Snap Points Settings")]
		public SnapPoint[] SnapPoints;

		public float SnapRadius = 0.4f;

		public bool HasTag(SurfaceTags tags)
		{
			return (Tags & tags) != 0;
		}

		public BaseInteractable TryGetDynamicInteractable()
		{
			return DynamicInteractable;
		}
	}
}
