using UnityEngine;

namespace Bozo.ModularCharacters
{
	[CreateAssetMenu(fileName = "BSMC_CharacterObject", menuName = "BoZo/BSMC_CharacterObject")]
	public class CharacterObject : DataObject
	{
		public Texture2D icon;

		public CharacterData data;

		[Tooltip("Show this preset on the character creator Data page.")]
		public bool ShowInCharacterList = true;

		public override CharacterData GetCharacterData()
		{
			return data;
		}

		public override Texture2D GetCharacterIcon()
		{
			return icon;
		}
	}
}
