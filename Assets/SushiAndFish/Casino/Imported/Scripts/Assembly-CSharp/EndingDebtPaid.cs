namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

public class EndingDebtPaid : MonoBehaviour
{
	[SerializeField]
	private GameObject livingRoomStuff;

	private void Start()
	{
		if (livingRoomStuff == null)
		{
			Debug.LogError("Living Room Stuff reference is missing!");
			return;
		}
		List<GameObject> list = new List<GameObject>();
		foreach (Transform item in livingRoomStuff.transform)
		{
			list.Add(item.gameObject);
		}
		foreach (GameObject item2 in list)
		{
			item2.SetActive(value: false);
		}
	}

	public void EnableLivingRoomStuff()
	{
		List<GameObject> list = new List<GameObject>();
		foreach (Transform item in livingRoomStuff.transform)
		{
			list.Add(item.gameObject);
		}
		foreach (GameObject item2 in list)
		{
			item2.SetActive(value: true);
		}
	}
}

}
