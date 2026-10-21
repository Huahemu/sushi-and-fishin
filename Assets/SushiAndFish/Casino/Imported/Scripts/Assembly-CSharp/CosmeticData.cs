namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(fileName = "New Cosmetic", menuName = "Gacha/Cosmetic Data")]
public class CosmeticData : ScriptableObject
{
	[Header("Cosmetic Info")]
	[UniqueID("cosmetics")]
	public int cosmeticId;

	public string cosmeticName;

	public string description;

	public GameObject cosmeticModel;

	public Material cosmeticMaterial;

	public CosmeticType cosmeticType;

	[Header("Rarity")]
	public CosmeticRarity rarity;
}

}
