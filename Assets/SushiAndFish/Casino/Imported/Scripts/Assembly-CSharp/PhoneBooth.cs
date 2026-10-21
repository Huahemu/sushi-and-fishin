namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;

public class PhoneBooth : InteractableBase
{
	[Header("Settings")]
	[SerializeField]
	private float interactionCooldown;

	[SerializeField]
	private float doorOpenDelay;

	[Header("References")]
	[SerializeField]
	private VehicleDoors vehicleDoors;

	[SerializeField]
	private ChallengeBooth challengeBooth;

	[SerializeField]
	private StudioEventEmitter phoneRingEmitter;

	[SerializeField]
	private TextMeshPro screenText;

	[SerializeField]
	private MMWiggle phoneWiggle;

	private float _interactionCooldown;

	private bool _isVehicleCalled;

	private void Update()
	{
		if (base.isServer && !(_interactionCooldown <= 0f))
		{
			_interactionCooldown -= Time.deltaTime;
		}
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		if (!(_interactionCooldown > 0f))
		{
			_interactionCooldown += interactionCooldown;
			if (!_isVehicleCalled)
			{
				ServerFirstInteraction();
			}
			else
			{
				ServerRerollChallenge();
			}
		}
	}

	[Server]
	private void ServerFirstInteraction()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PhoneBooth::ServerFirstInteraction()' called when server was not active");
			return;
		}
		_isVehicleCalled = true;
		StartCoroutine(FirstInteractionRoutine());
	}

	private IEnumerator FirstInteractionRoutine()
	{
		RpcOnPhoneAnswered();
		_interactionCooldown = doorOpenDelay + 2f;
		yield return new WaitForSeconds(doorOpenDelay);
		vehicleDoors.ServerOpenDoors();
		RpcOnDoorsOpened();
		yield return new WaitForSeconds(0.5f);
		challengeBooth.TryGiveDailyChallenge();
		RpcOnCallEnded();
		yield return new WaitForSeconds(1.5f);
		RpcOnRerollActive();
	}

	[Server]
	private void ServerRerollChallenge()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PhoneBooth::ServerRerollChallenge()' called when server was not active");
			return;
		}
		challengeBooth.RerollChallenge();
		RpcOnChallengeRerolled();
	}

	[ClientRpc]
	private void RpcOnPhoneAnswered()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PhoneBooth::RpcOnPhoneAnswered()", -2075849234, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnDoorsOpened()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PhoneBooth::RpcOnDoorsOpened()", -648298079, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnCallEnded()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PhoneBooth::RpcOnCallEnded()", -1064754735, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnChallengeRerolled()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PhoneBooth::RpcOnChallengeRerolled()", 1290531531, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnRerollActive()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PhoneBooth::RpcOnRerollActive()", 721189153, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnPhoneAnswered()
	{
		IsInteractable = false;
		phoneWiggle.enabled = false;
		phoneWiggle.RestoreInitialValues();
		phoneRingEmitter.Stop();
		screenText.text = "On call...";
	}

	protected static void InvokeUserCode_RpcOnPhoneAnswered(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnPhoneAnswered called on server.");
		}
		else
		{
			((PhoneBooth)obj).UserCode_RpcOnPhoneAnswered();
		}
	}

	protected void UserCode_RpcOnDoorsOpened()
	{
	}

	protected static void InvokeUserCode_RpcOnDoorsOpened(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDoorsOpened called on server.");
		}
		else
		{
			((PhoneBooth)obj).UserCode_RpcOnDoorsOpened();
		}
	}

	protected void UserCode_RpcOnCallEnded()
	{
		screenText.text = "Doors Opened!";
	}

	protected static void InvokeUserCode_RpcOnCallEnded(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnCallEnded called on server.");
		}
		else
		{
			((PhoneBooth)obj).UserCode_RpcOnCallEnded();
		}
	}

	protected void UserCode_RpcOnChallengeRerolled()
	{
		screenText.transform.DOPunchScale(screenText.transform.localScale * 0.2f, 0.3f, 1);
	}

	protected static void InvokeUserCode_RpcOnChallengeRerolled(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnChallengeRerolled called on server.");
		}
		else
		{
			((PhoneBooth)obj).UserCode_RpcOnChallengeRerolled();
		}
	}

	protected void UserCode_RpcOnRerollActive()
	{
		screenText.text = "Reroll Challenge \n(1 Ticket)";
		screenText.color = Color.crimson;
		InteractableName = "Reroll Challenge (1 Ticket)";
		TooltipMessage = "[E] Reroll";
		IsInteractable = true;
	}

	protected static void InvokeUserCode_RpcOnRerollActive(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnRerollActive called on server.");
		}
		else
		{
			((PhoneBooth)obj).UserCode_RpcOnRerollActive();
		}
	}

	static PhoneBooth()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PhoneBooth), "System.Void PhoneBooth::RpcOnPhoneAnswered()", InvokeUserCode_RpcOnPhoneAnswered);
		RemoteProcedureCalls.RegisterRpc(typeof(PhoneBooth), "System.Void PhoneBooth::RpcOnDoorsOpened()", InvokeUserCode_RpcOnDoorsOpened);
		RemoteProcedureCalls.RegisterRpc(typeof(PhoneBooth), "System.Void PhoneBooth::RpcOnCallEnded()", InvokeUserCode_RpcOnCallEnded);
		RemoteProcedureCalls.RegisterRpc(typeof(PhoneBooth), "System.Void PhoneBooth::RpcOnChallengeRerolled()", InvokeUserCode_RpcOnChallengeRerolled);
		RemoteProcedureCalls.RegisterRpc(typeof(PhoneBooth), "System.Void PhoneBooth::RpcOnRerollActive()", InvokeUserCode_RpcOnRerollActive);
	}
}

}
