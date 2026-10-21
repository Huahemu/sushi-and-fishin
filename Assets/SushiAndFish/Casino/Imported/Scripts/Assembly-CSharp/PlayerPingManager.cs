namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerPingManager : NetworkBehaviour
{
	[CompilerGenerated]
	private sealed class _003CDestroyPingAfterDelay_003Ed__12 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public float delay;

		public GameObject pingObject;

		public PlayerPingManager _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDestroyPingAfterDelay_003Ed__12(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			PlayerPingManager playerPingManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(delay);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				if (pingObject != null)
				{
					NetworkServer.Destroy(pingObject);
					if (playerPingManager._lastPingNetId == pingObject.GetComponent<NetworkIdentity>().netId)
					{
						playerPingManager._lastPingNetId = 0u;
					}
				}
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[Header("Ping Settings")]
	[SerializeField]
	private GameObject pingPrefab;

	[SerializeField]
	private LayerMask rayMask;

	[SerializeField]
	private float pingHeightOffset = 0.1f;

	private readonly RaycastHit[] _raycastHits = new RaycastHit[8];

	private Camera _cam;

	private uint _lastPingNetId;

	private void Awake()
	{
		_cam = MonoSingleton<LocalManager>.Instance.mainCamera;
	}

	private void OnEnable()
	{
		InputEvents.OnPingEvent = (Action)Delegate.Combine(InputEvents.OnPingEvent, new Action(OnPing));
	}

	private void OnDisable()
	{
		InputEvents.OnPingEvent = (Action)Delegate.Remove(InputEvents.OnPingEvent, new Action(OnPing));
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
	}

	private void OnPing()
	{
		int num = Physics.RaycastNonAlloc(_cam.transform.position, _cam.transform.forward, _raycastHits, 100f, rayMask, QueryTriggerInteraction.Ignore);
		if (num == 0)
		{
			return;
		}
		RaycastHit raycastHit = _raycastHits[0];
		for (int i = 1; i < num; i++)
		{
			if (_raycastHits[i].distance < raycastHit.distance)
			{
				raycastHit = _raycastHits[i];
			}
		}
		Vector3 position = raycastHit.point + raycastHit.normal * pingHeightOffset;
		Quaternion rotation = Quaternion.FromToRotation(Vector3.up, raycastHit.normal);
		ulong steamId = GetComponent<PlayerProfile>().steamId;
		CmdSpawnPing(position, rotation, steamId);
	}

	[Command]
	private void CmdSpawnPing(Vector3 position, Quaternion rotation, ulong steamId)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(position);
		writer.WriteQuaternion(rotation);
		writer.WriteVarULong(steamId);
		SendCommandInternal("System.Void PlayerPingManager::CmdSpawnPing(UnityEngine.Vector3,UnityEngine.Quaternion,System.UInt64)", 1491304033, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[IteratorStateMachine(typeof(_003CDestroyPingAfterDelay_003Ed__12))]
	[Server]
	private IEnumerator DestroyPingAfterDelay(GameObject pingObject, float delay)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator PlayerPingManager::DestroyPingAfterDelay(UnityEngine.GameObject,System.Single)' called when server was not active");
			return null;
		}
		return new _003CDestroyPingAfterDelay_003Ed__12(0)
		{
			_003C_003E4__this = this,
			pingObject = pingObject,
			delay = delay
		};
	}

	[ClientRpc]
	private void RpcSetPingSteamId(uint pingNetId, ulong steamId)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarUInt(pingNetId);
		writer.WriteVarULong(steamId);
		SendRPCInternal("System.Void PlayerPingManager::RpcSetPingSteamId(System.UInt32,System.UInt64)", -1572483224, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdSpawnPing__Vector3__Quaternion__UInt64(Vector3 position, Quaternion rotation, ulong steamId)
	{
		if (_lastPingNetId != 0 && NetworkServer.spawned.TryGetValue(_lastPingNetId, out var value))
		{
			NetworkServer.Destroy(value.gameObject);
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(pingPrefab, position, rotation);
		NetworkServer.Spawn(gameObject);
		_lastPingNetId = gameObject.GetComponent<NetworkIdentity>().netId;
		RpcSetPingSteamId(_lastPingNetId, steamId);
		StartCoroutine(DestroyPingAfterDelay(gameObject, 3f));
	}

	protected static void InvokeUserCode_CmdSpawnPing__Vector3__Quaternion__UInt64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogError("Command CmdSpawnPing called on client.");
		}
		else
		{
			((PlayerPingManager)obj).UserCode_CmdSpawnPing__Vector3__Quaternion__UInt64(reader.ReadVector3(), reader.ReadQuaternion(), reader.ReadVarULong());
		}
	}

	protected void UserCode_RpcSetPingSteamId__UInt32__UInt64(uint pingNetId, ulong steamId)
	{
		if (NetworkClient.spawned.TryGetValue(pingNetId, out var value))
		{
			SteamIdComponent component = value.GetComponent<SteamIdComponent>();
			if (component != null)
			{
				component.SetSteamID(steamId);
			}
		}
	}

	protected static void InvokeUserCode_RpcSetPingSteamId__UInt32__UInt64(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetPingSteamId called on server.");
		}
		else
		{
			((PlayerPingManager)obj).UserCode_RpcSetPingSteamId__UInt32__UInt64(reader.ReadVarUInt(), reader.ReadVarULong());
		}
	}

	static PlayerPingManager()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerPingManager), "System.Void PlayerPingManager::CmdSpawnPing(UnityEngine.Vector3,UnityEngine.Quaternion,System.UInt64)", InvokeUserCode_CmdSpawnPing__Vector3__Quaternion__UInt64, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerPingManager), "System.Void PlayerPingManager::RpcSetPingSteamId(System.UInt32,System.UInt64)", InvokeUserCode_RpcSetPingSteamId__UInt32__UInt64);
	}
}

}
