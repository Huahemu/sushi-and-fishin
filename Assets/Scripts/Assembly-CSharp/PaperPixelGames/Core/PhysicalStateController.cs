using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public class PhysicalStateController : NetworkBehaviour
	{
		private bool _startPhysicallyUsable;

		[Header("Visuals")]
		[SerializeField]
		private List<GameObject> _usableStateObjects = new List<GameObject>();

		[SerializeField]
		private GameObject _unusableStateObject;

		[SerializeField]
		private ParticleSystem _smokeParticle;

		private readonly NetworkVariable<bool> _isPhysicallyUsable = new NetworkVariable<bool>(value: false);

		private bool _serverHasExplicitState;

		public bool IsPhysicallyUsable => _isPhysicallyUsable.Value;

		public bool IsBroken => !_isPhysicallyUsable.Value;

		public event Action<bool> OnPhysicallyUsableChanged;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isPhysicallyUsable = _isPhysicallyUsable;
			isPhysicallyUsable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isPhysicallyUsable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUsableStateChanged));
			if (base.IsServer && !_serverHasExplicitState)
			{
				_isPhysicallyUsable.Value = _startPhysicallyUsable;
			}
			UpdateUsableVisual(_isPhysicallyUsable.Value);
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<bool> isPhysicallyUsable = _isPhysicallyUsable;
			isPhysicallyUsable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isPhysicallyUsable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUsableStateChanged));
		}

		public void Repair()
		{
			if (base.IsServer)
			{
				_serverHasExplicitState = true;
				ServerSetUsableInternal(value: true, playRepairAudio: true);
			}
			else
			{
				ServerRepairRpc();
			}
		}

		public void Break()
		{
			if (base.IsServer)
			{
				_serverHasExplicitState = true;
				ServerSetUsableInternal(value: false, playRepairAudio: false);
			}
			else
			{
				ServerBreakRpc();
			}
		}

		public void BreakFromHarpoon()
		{
			Break();
		}

		public void ServerSetUsable(bool value)
		{
			if (base.IsServer)
			{
				_serverHasExplicitState = true;
				ServerSetUsableInternal(value, playRepairAudio: false);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerRepairRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1265230849u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1265230849u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_serverHasExplicitState = true;
				ServerSetUsableInternal(value: true, playRepairAudio: true);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerBreakRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(525160918u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 525160918u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_serverHasExplicitState = true;
				ServerSetUsableInternal(value: false, playRepairAudio: false);
			}
		}

		private void ServerSetUsableInternal(bool value, bool playRepairAudio)
		{
			if (base.IsServer && _isPhysicallyUsable.Value != value)
			{
				_isPhysicallyUsable.Value = value;
				if (playRepairAudio & value)
				{
					ClientPlayRepairAudioRpc();
				}
			}
		}

		private void OnUsableStateChanged(bool previous, bool current)
		{
			UpdateUsableVisual(current);
			if (previous != current && _smokeParticle != null)
			{
				_smokeParticle.Play();
			}
			OnPhysicallyUsableChanged?.Invoke(current);
			RefreshLocalInteractorGazes();
		}

		private void RefreshLocalInteractorGazes()
		{
			((Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null) ? Singleton<PlayersManager>.Instance.LocalPlayer.Interactor : null)?.RefreshCurrentGazeFor(base.transform);
		}

		public void UpdateUsableVisual(bool usable)
		{
			if (_usableStateObjects != null)
			{
				foreach (GameObject usableStateObject in _usableStateObjects)
				{
					if (!(usableStateObject == null) && !(usableStateObject.GetComponentInChildren<NetworkObject>(includeInactive: true) != null))
					{
						usableStateObject.SetActive(usable);
					}
				}
			}
			if (_unusableStateObject != null)
			{
				_unusableStateObject.SetActive(!usable);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayRepairAudioRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4118130662u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4118130662u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<SoundManager>.Instance == null))
				{
					Singleton<SoundManager>.Instance.PlaySFX(SoundType.Repaired, base.transform.position);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_isPhysicallyUsable == null)
			{
				throw new Exception("PhysicalStateController._isPhysicallyUsable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isPhysicallyUsable.Initialize(this);
			__nameNetworkVariable(_isPhysicallyUsable, "_isPhysicallyUsable");
			NetworkVariableFields.Add(_isPhysicallyUsable);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1265230849u, __rpc_handler_1265230849, "ServerRepairRpc", RpcInvokePermission.Everyone);
			__registerRpc(525160918u, __rpc_handler_525160918, "ServerBreakRpc", RpcInvokePermission.Everyone);
			__registerRpc(4118130662u, __rpc_handler_4118130662, "ClientPlayRepairAudioRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1265230849(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PhysicalStateController)target).ServerRepairRpc();
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_525160918(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PhysicalStateController)target).ServerBreakRpc();
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4118130662(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PhysicalStateController)target).ClientPlayRepairAudioRpc();
				((PhysicalStateController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PhysicalStateController";
		}
	}
}
