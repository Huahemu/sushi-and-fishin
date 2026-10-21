using DG.Tweening;
using UnityEngine;

namespace UnityUtils
{
	public static class DotweenExtensions
	{
		public static Tweener DOSizeY(this BoxCollider collider, float targetHeight, float duration)
		{
			float startSize = collider.size.y;
			float startCenterY = collider.center.y;
			return DOTween.To(() => collider.size.y, (float y) =>
			{
				float num = (y - startSize) / 2f;
				collider.size = new Vector3(collider.size.x, y, collider.size.z);
				collider.center = new Vector3(collider.center.x, startCenterY + num, collider.center.z);
			}, targetHeight, duration);
		}
	}
}
