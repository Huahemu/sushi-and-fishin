using UnityEngine;

namespace Bozo.ModularCharacters
{
	[CreateAssetMenu(fileName = "MaterialPackage", menuName = "BoZo/MaterialPackage")]
	public class MaterialPackage : ScriptableObject
	{
		public Material material;

		public Sprite icon;

		public MergedMaterialData[] materialData;
	}
}
