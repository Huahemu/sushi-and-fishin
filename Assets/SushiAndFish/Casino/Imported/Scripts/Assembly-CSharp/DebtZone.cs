namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;

public class DebtZone : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float checkInterval = 0.1f;

	[Header("References")]
	[SerializeField]
	private Collider checkCollider;

	[SerializeField]
	private TextMeshPro moneyText;

	[SerializeField]
	private InteractableEventTrigger button;

	[SerializeField]
	private MeshRenderer buttonRenderer;

	[SerializeField]
	private Material enabledMaterial;

	[SerializeField]
	private Material disabledMaterial;

	[SerializeField]
	private UnityEvent onZoneInitialized;

	private long _totalDebtMoney;

	private long _moneyInZone;

	private int _bagAmountInZone;

	private float _lastCheckTime;

	private bool _hasInitialized;

	public override void OnStartServer()
	{
		base.OnStartServer();
		_totalDebtMoney = NetworkSingleton<MoneyManager>.Instance.balance;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isServer)
		{
			base.enabled = false;
		}
	}

	private void Start()
	{
		moneyText.text = "$0 / " + MoneyFormatter.FormatWithDollar(NetworkSingleton<MoneyManager>.Instance.balance);
	}

	private void Update()
	{
		CheckDebtBags();
	}

	private void CheckDebtBags()
	{
		if (_hasInitialized || Time.time - _lastCheckTime < checkInterval || !NetworkSingleton<WinSceneManager>.Instance)
		{
			return;
		}
		_lastCheckTime = Time.time;
		Bounds bounds = checkCollider.bounds;
		int num = 0;
		long num2 = 0L;
		foreach (DebtBag debtBag in NetworkSingleton<WinSceneManager>.Instance.debtBags)
		{
			if (bounds.Contains(debtBag.transform.position))
			{
				num++;
				num2 += debtBag.moneyInBag;
			}
		}
		_moneyInZone = num2;
		if (_bagAmountInZone != num)
		{
			bool hasIncreased = num > _bagAmountInZone;
			_bagAmountInZone = num;
			OnBagAmountChanged(hasIncreased);
		}
		else
		{
			_bagAmountInZone = num;
		}
	}

	private void OnBagAmountChanged(bool hasIncreased)
	{
		string text = MoneyFormatter.FormatWithDollar(_moneyInZone);
		string text2 = MoneyFormatter.FormatWithDollar(_totalDebtMoney);
		RpcSetMoneyText(text + " / " + text2);
		bool isEnabled = _bagAmountInZone >= NetworkSingleton<WinSceneManager>.Instance.debtBags.Count && _moneyInZone >= _totalDebtMoney;
		RpcSetButtonEnabled(isEnabled);
	}

	[ClientRpc]
	private void RpcSetMoneyText(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void DebtZone::RpcSetMoneyText(System.String)", -854644100, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetButtonEnabled(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void DebtZone::RpcSetButtonEnabled(System.Boolean)", -2117910075, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerTryInitializeZone()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DebtZone::ServerTryInitializeZone()' called when server was not active");
		}
		else if (!_hasInitialized && _bagAmountInZone >= NetworkSingleton<WinSceneManager>.Instance.debtBags.Count && _moneyInZone >= _totalDebtMoney)
		{
			_hasInitialized = true;
			onZoneInitialized?.Invoke();
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetMoneyText__String(string text)
	{
		moneyText.text = text;
	}

	protected static void InvokeUserCode_RpcSetMoneyText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMoneyText called on server.");
		}
		else
		{
			((DebtZone)obj).UserCode_RpcSetMoneyText__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcSetButtonEnabled__Boolean(bool isEnabled)
	{
		button.IsInteractable = isEnabled;
		buttonRenderer.material = (isEnabled ? enabledMaterial : disabledMaterial);
	}

	protected static void InvokeUserCode_RpcSetButtonEnabled__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetButtonEnabled called on server.");
		}
		else
		{
			((DebtZone)obj).UserCode_RpcSetButtonEnabled__Boolean(reader.ReadBool());
		}
	}

	static DebtZone()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DebtZone), "System.Void DebtZone::RpcSetMoneyText(System.String)", InvokeUserCode_RpcSetMoneyText__String);
		RemoteProcedureCalls.RegisterRpc(typeof(DebtZone), "System.Void DebtZone::RpcSetButtonEnabled(System.Boolean)", InvokeUserCode_RpcSetButtonEnabled__Boolean);
	}
}

}
