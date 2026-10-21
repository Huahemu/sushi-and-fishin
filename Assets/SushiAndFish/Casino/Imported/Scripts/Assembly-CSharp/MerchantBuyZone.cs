namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

[RequireComponent(typeof(Collider))]
public class MerchantBuyZone : NetworkBehaviour
{
	[Header("Settings")]
	[Tooltip("Should the collider be a trigger? (Automatically set on Awake)")]
	[SerializeField]
	private bool isTrigger = true;

	[Header("Total Cost Display")]
	[Tooltip("TextMeshPro 3D component to display the total cost in tickets.")]
	[SerializeField]
	private TextMeshPro totalCostText;

	[Tooltip("Item price settings. If null, will load from SushiAndFish.Casino.Original.CasinoResources.")]
	[SerializeField]
	private ItemPriceSettings priceSettings;

	[SerializeField]
	private GameSettings gameSettings;

	private const float UPDATE_INTERVAL = 0.1f;

	private float lastUpdateTime;

	private Collider zoneCollider;

	private void Awake()
	{
		zoneCollider = GetComponent<Collider>();
		if (zoneCollider != null)
		{
			zoneCollider.isTrigger = isTrigger;
		}
		if (priceSettings == null)
		{
			priceSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ItemPriceSettings>("ItemPriceSettings");
		}
		if (base.isServer)
		{
			UpdateTotalCostDisplay();
		}
		totalCostText.text = "";
		if (!gameSettings)
		{
			gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		}
	}

	private void Update()
	{
		if (base.isServer && Time.time - lastUpdateTime >= 0.1f)
		{
			UpdateTotalCostDisplay();
			lastUpdateTime = Time.time;
		}
	}

	private bool IsNormalItem(ConsumableItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (item.GetComponent<GachaSphere>() != null)
		{
			return false;
		}
		return item.spawnableSo != null;
	}

	[Server]
	public List<ConsumableItem> GetItemsInZone()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<ConsumableItem> MerchantBuyZone::GetItemsInZone()' called when server was not active");
			return null;
		}
		List<ConsumableItem> list = new List<ConsumableItem>();
		if (zoneCollider == null)
		{
			return list;
		}
		Collider[] array = Physics.OverlapBox(zoneCollider.bounds.center, zoneCollider.bounds.extents, zoneCollider.transform.rotation);
		HashSet<ConsumableItem> hashSet = new HashSet<ConsumableItem>();
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			if (!(collider == null) && !(collider == zoneCollider))
			{
				ConsumableItem component = collider.GetComponent<ConsumableItem>();
				if (component == null && collider.attachedRigidbody != null)
				{
					component = collider.attachedRigidbody.GetComponent<ConsumableItem>();
				}
				if (component != null && !hashSet.Contains(component) && component.IsInteractable)
				{
					hashSet.Add(component);
				}
			}
		}
		list.AddRange(hashSet);
		return list;
	}

	[Server]
	public void ClearItemsInZone()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MerchantBuyZone::ClearItemsInZone()' called when server was not active");
		}
		else
		{
			UpdateTotalCostDisplay();
		}
	}

	[Server]
	private void UpdateTotalCostDisplay()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MerchantBuyZone::UpdateTotalCostDisplay()' called when server was not active");
			return;
		}
		int totalCost = CalculateTotalCost();
		RpcUpdateTotalCostDisplay(totalCost);
	}

	[Server]
	private int CalculateTotalCost()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Int32 MerchantBuyZone::CalculateTotalCost()' called when server was not active");
			return default;
		}
		List<ConsumableItem> itemsInZone = GetItemsInZone();
		if (itemsInZone == null || itemsInZone.Count == 0)
		{
			return 0;
		}
		if (priceSettings == null)
		{
			return 0;
		}
		int num = 0;
		int floorIndex = 0;
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			floorIndex = gameSettings.DayToFloor(NetworkSingleton<GameManager>.Instance.daysPassed - 1);
		}
		foreach (ConsumableItem item in itemsInZone)
		{
			if (item == null)
			{
				continue;
			}
			GachaSphere component = item.GetComponent<GachaSphere>();
			if (component != null)
			{
				CosmeticRarity rarity = CosmeticRarity.Common;
				CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(component.CosmeticId);
				if (cosmeticById != null)
				{
					rarity = cosmeticById.rarity;
				}
				num += priceSettings.CalculateCosmeticPrice(rarity, floorIndex);
			}
			else if (!(item.spawnableSo == null))
			{
				int num2 = priceSettings.CalculatePrice(item.spawnableSo, floorIndex);
				num += num2;
			}
		}
		return num;
	}

	[ClientRpc]
	private void RpcUpdateTotalCostDisplay(int totalCost)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(totalCost);
		SendRPCInternal("System.Void MerchantBuyZone::RpcUpdateTotalCostDisplay(System.Int32)", 1527735829, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcUpdateTotalCostDisplay__Int32(int totalCost)
	{
		if (totalCostText != null)
		{
			if (totalCost > 0)
			{
				totalCostText.text = $"{totalCost} Tickets";
				totalCostText.gameObject.SetActive(value: true);
			}
			else
			{
				totalCostText.text = "";
				totalCostText.gameObject.SetActive(value: false);
			}
		}
	}

	protected static void InvokeUserCode_RpcUpdateTotalCostDisplay__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateTotalCostDisplay called on server.");
		}
		else
		{
			((MerchantBuyZone)obj).UserCode_RpcUpdateTotalCostDisplay__Int32(reader.ReadVarInt());
		}
	}

	static MerchantBuyZone()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MerchantBuyZone), "System.Void MerchantBuyZone::RpcUpdateTotalCostDisplay(System.Int32)", InvokeUserCode_RpcUpdateTotalCostDisplay__Int32);
	}
}

}
