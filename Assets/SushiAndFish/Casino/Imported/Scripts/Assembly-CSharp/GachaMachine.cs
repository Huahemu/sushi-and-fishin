namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class GachaMachine : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform spawnPoint;

	[SerializeField]
	private GachaSphere spherePrefab;

	[SerializeField]
	private MMLootTableScriptableObjectSO lootTable;

	[Header("Settings")]
	[SerializeField]
	private int cost = 1;

	[Server]
	public void ServerBuyGacha(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GachaMachine::ServerBuyGacha(PlayerInteract)' called when server was not active");
		}
		else if (NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-cost))
		{
			ProcessGacha();
		}
	}

	[Server]
	private void ProcessGacha()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GachaMachine::ProcessGacha()' called when server was not active");
			return;
		}
		ScriptableObject loot = lootTable.GetLoot();
		Vector3 position = ((spawnPoint != null) ? spawnPoint.position : (base.transform.position + Vector3.up));
		GameObject obj = Object.Instantiate(spherePrefab.gameObject, position, Quaternion.identity);
		NetworkServer.Spawn(obj);
		GachaSphere component = obj.GetComponent<GachaSphere>();
		if (component != null && loot != null)
		{
			CosmeticData cosmeticData = loot as CosmeticData;
			if (cosmeticData != null)
			{
				component.SetCosmeticId(cosmeticData.cosmeticId);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
