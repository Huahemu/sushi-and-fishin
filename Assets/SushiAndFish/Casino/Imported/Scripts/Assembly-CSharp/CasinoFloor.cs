namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class CasinoFloor : MonoBehaviour
{
	public int floorIndex;

	[SerializeField]
	private BoxCollider[] sfxTriggerBox;

	public void SetSfxTrigger(bool isEnabled)
	{
		BoxCollider[] array = sfxTriggerBox;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = isEnabled;
		}
	}
}

}
