using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public class SnapPoint : MonoBehaviour
	{
		[SerializeField]
		private SurfaceTags _allowedTags;

		public SurfaceTags AllowedTags => _allowedTags;

		public bool CanSnap(BaseHoldable holdable)
		{
			if (holdable == null)
			{
				return false;
			}
			if (_allowedTags == SurfaceTags.None)
			{
				return true;
			}
			return (_allowedTags & holdable.ItemSO.PlaceableTags) != 0;
		}
	}
}
