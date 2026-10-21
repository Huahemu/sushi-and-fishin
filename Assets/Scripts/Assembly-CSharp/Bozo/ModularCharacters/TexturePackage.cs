using UnityEngine;

namespace Bozo.ModularCharacters
{
	public class TexturePackage : MonoBehaviour
	{
		public Texture texture;

		public Sprite icon;

		public Color[] colors;

		public Vector2 maxScale = new Vector2(-1f, -1f);

		public TextureType type;

		public string catagory;
	}
}
