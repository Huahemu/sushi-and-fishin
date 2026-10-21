using System.Text;
using Unity.Netcode;
using UnityEngine;

public class NetworkDebugDisplay : NetworkBehaviour
{
	private StringBuilder _info = new StringBuilder();

	private float _ngoPingTimer;

	private float _ngoPingInterval = 3f;

	private float _lastPingSentTime;

	private float _ngoPingValue = -1f;

	private float _deltaTime;

	private bool _enableDebug;

	public override void OnNetworkSpawn()
	{
		if (!Debug.isDebugBuild)
		{
			base.enabled = false;
		}
		else if (base.IsOwner)
		{
			SendPingToServer();
		}
	}

	private void Update()
	{
		if (!base.IsOwner)
		{
			return;
		}
		if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F))
		{
			_enableDebug = !_enableDebug;
		}
		if (_enableDebug)
		{
			_deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
			_ngoPingTimer += Time.deltaTime;
			if (_ngoPingTimer >= _ngoPingInterval)
			{
				_ngoPingTimer = 0f;
				SendPingToServer();
			}
		}
	}

	private void SendPingToServer()
	{
		_lastPingSentTime = Time.realtimeSinceStartup;
		SendPingServerRpc(base.OwnerClientId);
	}

	[Rpc(SendTo.Server)]
	private void SendPingServerRpc(ulong sender)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1038324152u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, sender);
			__endSendRpc(ref bufferWriter, 1038324152u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			SendPingClientRpc(sender);
		}
	}

	[Rpc(SendTo.Owner)]
	private void SendPingClientRpc(ulong clientId)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1802967899u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			__endSendRpc(ref bufferWriter, 1802967899u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (clientId == base.NetworkManager.LocalClientId)
			{
				_ngoPingValue = (Time.realtimeSinceStartup - _lastPingSentTime) * 1000f;
			}
		}
	}

	private void OnGUI()
	{
		if (base.IsOwner && _enableDebug)
		{
			_info.Clear();
			float f = 1f / _deltaTime;
			float num = _deltaTime * 1000f;
			_info.AppendLine($"<b>FPS:</b> <color=lime>{Mathf.Ceil(f)}</color>  <size=18>({num:F1} ms)</size>");
			string text = ((_ngoPingValue >= 0f) ? $"<color=red>{_ngoPingValue:F1} ms</color>" : "<color=red>N/A</color>");
			_info.AppendLine("<b>Ping:</b> " + text);
			GUIStyle style = new GUIStyle
			{
				richText = true,
				alignment = TextAnchor.UpperLeft,
				fontSize = 25,
				normal = 
				{
					textColor = Color.white,
					background = null
				}
			};
			GUI.Label(new Rect(10f, 10f, 350f, 80f), _info.ToString(), style);
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(1038324152u, __rpc_handler_1038324152, "SendPingServerRpc", RpcInvokePermission.Everyone);
		__registerRpc(1802967899u, __rpc_handler_1802967899, "SendPingClientRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_1038324152(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((NetworkDebugDisplay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((NetworkDebugDisplay)target).SendPingServerRpc(value);
			((NetworkDebugDisplay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1802967899(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((NetworkDebugDisplay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((NetworkDebugDisplay)target).SendPingClientRpc(value);
			((NetworkDebugDisplay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "NetworkDebugDisplay";
	}
}
