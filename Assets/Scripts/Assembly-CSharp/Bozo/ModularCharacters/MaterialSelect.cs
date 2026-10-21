using UnityEngine;

namespace Bozo.ModularCharacters
{
	public class MaterialSelect : MonoBehaviour
	{
		public CharacterCreator characterCreator;

		public Transform container;

		public MaterialSelector Selector;

		private void Start()
		{
			MaterialPackage[] array = Resources.LoadAll<MaterialPackage>("");
			foreach (MaterialPackage mat in array)
			{
				Object.Instantiate(Selector, container).Init(mat, characterCreator);
			}
		}
	}
}
