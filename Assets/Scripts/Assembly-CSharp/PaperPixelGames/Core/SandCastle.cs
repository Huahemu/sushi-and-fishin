using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public class SandCastle : NetworkBehaviour
	{
		[SerializeField]
		private GameObject _graph;

		[SerializeField]
		private GameObject _crashedgraph;

		[SerializeField]
		private ParticleSystem _sandVfx;

		[SerializeField]
		private AudioClipSO _sandSfx;

		private readonly NetworkVariable<bool> _isCrashedNetwork = new NetworkVariable<bool>(value: false);

		private bool _isCrashed;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isCrashedNetwork = _isCrashedNetwork;
			isCrashedNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCrashedNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCrashedStateChanged));
			ApplyCrashStatus(_isCrashedNetwork.Value, playImpactEffects: false);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isCrashedNetwork = _isCrashedNetwork;
			isCrashedNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCrashedNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCrashedStateChanged));
			base.OnNetworkDespawn();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!(other == null) && other.CompareTag("Player") && !_isCrashed)
			{
				if (base.IsServer)
				{
					ServerCrash(ResolvePlayerClientId(other));
				}
				else
				{
					ServerCrashRpc();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCrashRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4185899369u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4185899369u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerCrash(rpcParams.Receive.SenderClientId);
			}
		}

		private void ServerCrash(ulong clientId)
		{
			if (!_isCrashed)
			{
				SetCrashedState(status: true);
				Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.SandCastleCrashed, 1, "sand_castle_crashed", clientId);
				Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_CHILD_MAKER_CRY", clientId);
			}
		}

		public bool ServerCrashFromBeachBall()
		{
			if (!base.IsServer || _isCrashed)
			{
				return false;
			}
			SetCrashedState(status: true);
			return true;
		}

		private static ulong ResolvePlayerClientId(Collider other)
		{
			PPG_Player pPG_Player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
			if (pPG_Player != null)
			{
				return pPG_Player.OwnerClientId;
			}
			return ulong.MaxValue;
		}

		public void ServerSetCrashedState(bool status)
		{
			if (base.IsServer)
			{
				SetCrashedState(status);
			}
		}

		private void SetCrashedState(bool status)
		{
			if (base.IsServer)
			{
				_isCrashedNetwork.Value = status;
			}
		}

		private void OnCrashedStateChanged(bool previousStatus, bool currentStatus)
		{
			ApplyCrashStatus(currentStatus, currentStatus && !previousStatus);
		}

		private void ApplyCrashStatus(bool status, bool playImpactEffects)
		{
			if (_graph != null)
			{
				_graph.SetActive(!status);
			}
			if (_crashedgraph != null)
			{
				_crashedgraph.SetActive(status);
			}
			if (_sandVfx != null && (status & playImpactEffects))
			{
				_sandVfx.Play();
			}
			if (((_sandSfx != null) & status & playImpactEffects) && Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(_sandSfx, base.transform.position);
			}
			_isCrashed = status;
		}

		protected override void __initializeVariables()
		{
			if (_isCrashedNetwork == null)
			{
				throw new Exception("SandCastle._isCrashedNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCrashedNetwork.Initialize(this);
			__nameNetworkVariable(_isCrashedNetwork, "_isCrashedNetwork");
			NetworkVariableFields.Add(_isCrashedNetwork);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4185899369u, __rpc_handler_4185899369, "ServerCrashRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4185899369(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((SandCastle)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SandCastle)target).ServerCrashRpc(ext);
				((SandCastle)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SandCastle";
		}
	}
}
