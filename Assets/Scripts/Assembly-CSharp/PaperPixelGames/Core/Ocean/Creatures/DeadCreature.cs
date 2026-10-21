using System;
using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(NetworkObject))]
	public class DeadCreature : NetworkBehaviour
	{
		[Header("Creature")]
		[SerializeField]
		private CreatureSO _creatureSO;

		[Header("Graphic")]
		[SerializeField]
		private Transform _graphicRoot;

		[SerializeField]
		private GameObject _spawnedGraphic;

		[Header("Lazy Hold")]
		[SerializeField]
		[Min(0f)]
		private float _lazyHoldDelay = 0.05f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly NetworkVariable<FixedString64Bytes> _creatureId = new NetworkVariable<FixedString64Bytes>();

		private readonly NetworkVariable<float> _weight = new NetworkVariable<float>(0f);

		private Coroutine _clientInitRoutine;

		private bool _clientVisualInitDone;

		private bool _ownsSpawnedGraphic;

		public CreatureSO CreatureSO => _creatureSO;

		public string CreatureId => _creatureId.Value.ToString();

		public NetworkVariable<float> Weight => _weight;

		public GameObject SpawnedGraphic => _spawnedGraphic;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<FixedString64Bytes> creatureId = _creatureId;
			creatureId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(creatureId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnCreatureIdChanged));
			if (base.IsClient)
			{
				StartClientVisualInit();
			}
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<FixedString64Bytes> creatureId = _creatureId;
			creatureId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(creatureId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnCreatureIdChanged));
			CleanupClientVisualInit();
			CleanupOwnedGraphic();
			base.OnNetworkDespawn();
		}

		public virtual void ServerInit(CreatureSO creatureSO, float weight = 0f)
		{
			if (!base.IsServer)
			{
				Log("ServerInit ignored. Server only.", LogType.Warning);
				return;
			}
			if (creatureSO == null)
			{
				Log("ServerInit failed. CreatureSO is null.", LogType.Warning);
				return;
			}
			_creatureSO = creatureSO;
			_creatureId.Value = new FixedString64Bytes(creatureSO.Id ?? string.Empty);
			_weight.Value = Mathf.Max(0f, weight);
			if (!base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Spawn(destroyWithScene: true);
			}
			Log($"ServerInit completed. CreatureId={CreatureId} Weight={_weight.Value}");
		}

		public virtual void ServerSetWeight(float weight)
		{
			if (base.IsServer)
			{
				_weight.Value = Mathf.Max(0f, weight);
			}
		}

		public virtual void ServerLazyHold(string creatureId, ulong clientId, NetworkObjectReference interactorReference, bool select, bool canHold)
		{
			if (!base.IsServer)
			{
				Log("ServerLazyHold ignored. Server only.", LogType.Warning);
				return;
			}
			if (!string.IsNullOrWhiteSpace(creatureId))
			{
				_creatureId.Value = new FixedString64Bytes(creatureId);
			}
			StartCoroutine(ServerLazyHoldCoroutine(clientId, interactorReference, select, canHold));
		}

		private IEnumerator ServerLazyHoldCoroutine(ulong clientId, NetworkObjectReference interactorReference, bool select, bool canHold)
		{
			if (_lazyHoldDelay > 0f)
			{
				yield return new WaitForSeconds(_lazyHoldDelay);
			}
			else
			{
				yield return null;
			}
			if (base.IsServer)
			{
				OnServerLazyHoldRequested(clientId, interactorReference, select, canHold);
				ClientLazyHoldRequestedRpc(interactorReference, select, canHold, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientLazyHoldRequestedRpc(NetworkObjectReference interactorReference, bool select, bool canHold, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2904449502u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in select, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in canHold, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2904449502u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				OnClientLazyHoldRequested(interactorReference, select, canHold);
			}
		}

		protected virtual void OnServerLazyHoldRequested(ulong clientId, NetworkObjectReference interactorReference, bool select, bool canHold)
		{
		}

		protected virtual void OnClientLazyHoldRequested(NetworkObjectReference interactorReference, bool select, bool canHold)
		{
		}

		private void StartClientVisualInit()
		{
			if (!_clientVisualInitDone)
			{
				CleanupClientVisualInit();
				_clientInitRoutine = StartCoroutine(ClientVisualInitCoroutine());
			}
		}

		private void CleanupClientVisualInit()
		{
			if (_clientInitRoutine != null)
			{
				StopCoroutine(_clientInitRoutine);
				_clientInitRoutine = null;
			}
		}

		private IEnumerator ClientVisualInitCoroutine()
		{
			float elapsed = 0f;
			while (!_clientVisualInitDone && elapsed < 5f)
			{
				if (TryResolveCreatureSO())
				{
					CompleteClientVisualInit();
					yield break;
				}
				elapsed += 0.1f;
				yield return new WaitForSeconds(0.1f);
			}
			Log("Client visual init timeout. CreatureId=" + CreatureId, LogType.Warning);
		}

		private void CompleteClientVisualInit()
		{
			if (!_clientVisualInitDone)
			{
				_clientVisualInitDone = true;
				CleanupClientVisualInit();
				SpawnGraphic();
				OnClientVisualInitialized();
			}
		}

		protected virtual bool TryResolveCreatureSO()
		{
			if (_creatureSO != null)
			{
				return true;
			}
			string creatureId = CreatureId;
			if (string.IsNullOrWhiteSpace(creatureId))
			{
				return false;
			}
			if (Singleton<SeaCreatureManager>.Instance == null)
			{
				return false;
			}
			return Singleton<SeaCreatureManager>.Instance.TryGetCreature(creatureId, out _creatureSO);
		}

		protected virtual void SpawnGraphic()
		{
			if (!(_spawnedGraphic != null) && !(_creatureSO == null) && !(_creatureSO.DeadGraphic == null))
			{
				Transform parent = ((_graphicRoot != null) ? _graphicRoot : base.transform);
				_spawnedGraphic = UnityEngine.Object.Instantiate(_creatureSO.DeadGraphic, parent);
				_spawnedGraphic.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				_ownsSpawnedGraphic = true;
				OnGraphicSpawned(_spawnedGraphic);
			}
		}

		protected virtual void CleanupOwnedGraphic()
		{
			if (_ownsSpawnedGraphic && !(_spawnedGraphic == null))
			{
				UnityEngine.Object.Destroy(_spawnedGraphic);
				_spawnedGraphic = null;
				_ownsSpawnedGraphic = false;
			}
		}

		private void OnCreatureIdChanged(FixedString64Bytes previous, FixedString64Bytes current)
		{
			if (!previous.Equals(current))
			{
				if (_creatureSO != null && _creatureSO.Id != current.ToString())
				{
					_creatureSO = null;
				}
				if (base.IsClient && !_clientVisualInitDone)
				{
					StartClientVisualInit();
				}
			}
		}

		protected virtual void OnClientVisualInitialized()
		{
		}

		protected virtual void OnGraphicSpawned(GameObject graphic)
		{
		}

		protected void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[DeadCreature] " + message, type);
			}
		}

		protected override void __initializeVariables()
		{
			if (_creatureId == null)
			{
				throw new Exception("DeadCreature._creatureId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_creatureId.Initialize(this);
			__nameNetworkVariable(_creatureId, "_creatureId");
			NetworkVariableFields.Add(_creatureId);
			if (_weight == null)
			{
				throw new Exception("DeadCreature._weight cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_weight.Initialize(this);
			__nameNetworkVariable(_weight, "_weight");
			NetworkVariableFields.Add(_weight);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2904449502u, __rpc_handler_2904449502, "ClientLazyHoldRequestedRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2904449502(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((DeadCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DeadCreature)target).ClientLazyHoldRequestedRpc(value, value2, value3, ext);
				((DeadCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DeadCreature";
		}
	}
}
