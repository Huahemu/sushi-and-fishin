using MagicaCloth2;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	public class BoZo_MagicaClothCollider : MonoBehaviour
	{
		private enum Direction
		{
			X = 0,
			Y = 1,
			Z = 2
		}

		[SerializeField]
		private Direction direction;

		[SerializeField]
		private bool reverseDirection;

		[SerializeField]
		private float length;

		[SerializeField]
		private float startRadius;

		[SerializeField]
		private float endRadius;

		[SerializeField]
		private Vector3 center;

		private void Awake()
		{
			MagicaCapsuleCollider magicaCapsuleCollider = base.gameObject.AddComponent<MagicaCapsuleCollider>();
			switch (direction)
			{
			case Direction.X:
				magicaCapsuleCollider.direction = MagicaCapsuleCollider.Direction.X;
				break;
			case Direction.Y:
				magicaCapsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
				break;
			case Direction.Z:
				magicaCapsuleCollider.direction = MagicaCapsuleCollider.Direction.Z;
				break;
			}
			magicaCapsuleCollider.reverseDirection = reverseDirection;
			magicaCapsuleCollider.SetSize(startRadius, endRadius, length);
			magicaCapsuleCollider.center = center;
			magicaCapsuleCollider.UpdateParameters();
		}

		private void Start()
		{
			Object.Destroy(this);
		}
	}
}
