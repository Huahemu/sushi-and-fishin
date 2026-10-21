namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class DisableOnStart : MonoBehaviour
{
	private void Awake()
	{
		base.gameObject.SetActive(value: false);
	}
}

}
