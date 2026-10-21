namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Gamble With Your Friends/Card Data", order = 0)]
public class CardDataSO : ScriptableObject
{
	[Header("Card Visual")]
	[SerializeField]
	public Sprite cardSprite;
}

}
