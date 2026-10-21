using System.Collections.Generic;
using IndieKit;
using PaperPixelGames.Core.Animals;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.ScrapSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityServiceLocator;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public class HarpoonTipSensor : NetworkBehaviour
	{
		private readonly struct PendingCreatureSens
		{
			public readonly NetworkObjectReference CreatureReference;

			public readonly NetworkObjectReference PlayerReference;

			public readonly byte SensType;

			public readonly float TriggerTime;

			public PendingCreatureSens(NetworkObjectReference creatureReference, NetworkObjectReference playerReference, byte sensType, float triggerTime)
			{
				CreatureReference = creatureReference;
				PlayerReference = playerReference;
				SensType = sensType;
				TriggerTime = triggerTime;
			}
		}

		[Header("References")]
		public PPG_Player Player;

		[Tooltip("Logical tip used for ray / sphere cast (should move in a straight line).")]
		public Transform LogicTip;

		[Tooltip("Where the logical tip is reset when sensing ends.")]
		public Transform Reset;

		[Header("Raycast")]
		[Tooltip("Physical radius of the travelling harpoon tip used against all colliders.")]
		[Min(0f)]
		public float sphereRadius = 0.055f;

		[Tooltip("Larger aim-assist radius used only for supported harpoon targets.")]
		[Min(0f)]
		public float targetAssistRadius = 0.25f;

		[Min(0f)]
		public float sphereSensRadius = 0.1f;

		public LayerMask hitMask = -1;

		[Min(0.01f)]
		public float maxStepDistance = 1.5f;

		[Header("Effects")]
		public ParticleSystem _tilePS;

		public ParticleSystem _waterPS;

		public ParticleSystem _sandPS;

		public ParticleSystem _grassPS;

		public ParticleSystem _fishPS;

		public ParticleSystem _playerPS;

		public ParticleSystem _ripplePS;

		[Header("Events")]
		public UnityEvent<RaycastHit> OnTipHit;

		[Header("Debug")]
		public bool debugDraw;

		public Color debugColor = new Color(0f, 1f, 1f, 0.8f);

		[Header("Creature Sens")]
		[SerializeField]
		[Min(0f)]
		private float _creatureSensDelay = 0.12f;

		private readonly HashSet<Collider> _sensColliders = new HashSet<Collider>();

		private readonly HashSet<NetworkObject> _sensedCreatures = new HashSet<NetworkObject>();

		private readonly List<PendingCreatureSens> _pendingCreatureSensList = new List<PendingCreatureSens>();

		private readonly RaycastHit[] _targetAssistHits = new RaycastHit[16];

		private bool _hasTipHit;

		private Vector3 _lastPos;

		private bool _active;

		private const float kEpsilon = 0.0001f;

		[HideInInspector]
		private GameObject _tipGraphic;

		[Header("Tip Graphic Simple Follow")]
		[SerializeField]
		private Vector3 _tipLocalPosOffset = Vector3.zero;

		[SerializeField]
		private Vector3 _tipLocalRotOffsetEuler = Vector3.zero;

		[SerializeField]
		private bool _useWorldUp = true;

		private Quaternion _tipLocalRotOffset;

		private bool _firstTimeScrap;

		private WorldPickupHandler _pickupHandler;

		public void BeginSense()
		{
			_active = true;
			_lastPos = LogicTip.position;
			_sensColliders.Clear();
			_sensedCreatures.Clear();
			_pendingCreatureSensList.Clear();
			_hasTipHit = false;
			if (Player != null && Player.CameraTransform != null)
			{
				Vector3 forward = Player.CameraTransform.forward;
				if (forward.sqrMagnitude > 1E-06f)
				{
					LogicTip.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
				}
			}
			if (_tipGraphic != null)
			{
				_tipGraphic.SetActive(value: true);
			}
			if (Player.IsUnderWater)
			{
				_ripplePS.Play();
			}
		}

		public void EndSense()
		{
			_active = false;
			_pendingCreatureSensList.Clear();
			_sensedCreatures.Clear();
			if (LogicTip != null && Reset != null)
			{
				LogicTip.SetPositionAndRotation(Reset.position, Reset.rotation);
			}
			if (_tipGraphic != null)
			{
				_tipGraphic.transform.localPosition = _tipLocalPosOffset;
				_tipGraphic.transform.localRotation = _tipLocalRotOffset;
				_tipGraphic.SetActive(value: false);
			}
			_lastPos = ((LogicTip != null) ? LogicTip.position : Vector3.zero);
			if (_ripplePS != null)
			{
				_ripplePS.Stop();
			}
		}

		public void SetupTipGraphic(GameObject refTip)
		{
			if (_tipGraphic != null)
			{
				Object.Destroy(_tipGraphic);
			}
			_tipLocalRotOffset = Quaternion.Euler(_tipLocalRotOffsetEuler);
			_tipGraphic = Object.Instantiate(refTip, LogicTip);
			_tipGraphic.transform.localPosition = _tipLocalPosOffset;
			_tipGraphic.transform.localRotation = _tipLocalRotOffset;
			_tipGraphic.SetActive(value: false);
			UtilityMethods.SetLayerRecursive(_tipGraphic, "ViewModel", includeChildren: true);
		}

		private void LateUpdate()
		{
			if (!_active || LogicTip == null)
			{
				return;
			}
			if (_tipGraphic != null && _tipGraphic.activeSelf)
			{
				_tipGraphic.transform.localPosition = _tipLocalPosOffset;
			}
			Vector3 position = LogicTip.position;
			Vector3 vector = position - _lastPos;
			float magnitude = vector.magnitude;
			if (magnitude <= 0.0001f)
			{
				_lastPos = position;
				return;
			}
			Vector3 vector2 = vector / magnitude;
			float num = magnitude;
			Vector3 lastPos = _lastPos;
			while (num > 0.0001f)
			{
				float num2 = Mathf.Min(num, maxStepDistance);
				if (Physics.SphereCast(lastPos, sphereSensRadius, vector2, out var hitInfo, num2, hitMask, QueryTriggerInteraction.Collide))
				{
					QueueCreatureSens(hitInfo.collider, 0);
				}
				if (TryGetClosestStepHit(lastPos, vector2, num2, out var hit))
				{
					HandleLocalHit(hit);
					break;
				}
				lastPos += vector2 * num2;
				num -= num2;
			}
			if (debugDraw)
			{
				Debug.DrawLine(_lastPos, position, debugColor, 0f, depthTest: false);
			}
			ProcessPendingCreatureSens();
			_lastPos = position;
		}

		private bool TryGetClosestStepHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
		{
			bool num = Physics.SphereCast(origin, sphereRadius, direction, out var hitInfo, distance, hitMask, QueryTriggerInteraction.UseGlobal);
			bool flag = TryGetClosestAssistedTargetHit(origin, direction, distance, out var closestHit);
			if (!num)
			{
				hit = closestHit;
				return flag;
			}
			if (!flag)
			{
				hit = hitInfo;
				return true;
			}
			float num2 = Mathf.Max(closestHit.distance, Vector3.Dot(closestHit.point - origin, direction));
			hit = ((hitInfo.distance <= num2) ? hitInfo : closestHit);
			return true;
		}

		private bool TryGetClosestAssistedTargetHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
		{
			closestHit = default;
			if (targetAssistRadius <= sphereRadius)
			{
				return false;
			}
			int num = Physics.SphereCastNonAlloc(origin, targetAssistRadius, direction, _targetAssistHits, distance, hitMask, QueryTriggerInteraction.UseGlobal);
			bool result = false;
			float num2 = float.PositiveInfinity;
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _targetAssistHits[i];
				if (IsAssistedTarget(raycastHit.collider) && !(raycastHit.distance >= num2))
				{
					result = true;
					num2 = raycastHit.distance;
					closestHit = raycastHit;
				}
			}
			return result;
		}

		private static bool IsAssistedTarget(Collider collider)
		{
			if (collider == null)
			{
				return false;
			}
			PullableRock component8;
			if (!(collider.GetComponentInParent<CrabBossController>() != null) && !(collider.GetComponentInParent<CrabSmallController>() != null) && !collider.TryGetComponent<Radio>(out var _) && !collider.TryGetComponent<SeaCreature>(out var _) && !collider.TryGetComponent<JellyfishTrap>(out var _) && !collider.TryGetComponent<PlayerInteraction>(out var _) && !collider.TryGetComponent<HarpoonGrapplePoint>(out var _) && !collider.TryGetComponent<DestructibleObject>(out var _) && !collider.TryGetComponent<HarpoonPullDropTarget>(out var _))
			{
				return collider.TryGetComponent<PullableRock>(out component8);
			}
			return true;
		}

		private void QueueCreatureSens(Collider collider, byte sensType)
		{
			if (!(collider == null))
			{
				SeaCreature componentInParent = collider.GetComponentInParent<SeaCreature>();
				if (!(componentInParent == null) && !(componentInParent.NetworkObject == null) && componentInParent.NetworkObject.IsSpawned && _sensedCreatures.Add(componentInParent.NetworkObject) && !(Player == null) && !(Player.NetworkObject == null) && base.IsSpawned)
				{
					_pendingCreatureSensList.Add(new PendingCreatureSens(componentInParent.NetworkObject.ToReference(), Player.NetworkObject.ToReference(), sensType, Time.time));
				}
			}
		}

		private void ProcessPendingCreatureSens()
		{
			if (_hasTipHit || _pendingCreatureSensList.Count == 0)
			{
				return;
			}
			float time = Time.time;
			for (int num = _pendingCreatureSensList.Count - 1; num >= 0; num--)
			{
				PendingCreatureSens pendingCreatureSens = _pendingCreatureSensList[num];
				if (!(time - pendingCreatureSens.TriggerTime < _creatureSensDelay))
				{
					ServerCreatureSensRpc(pendingCreatureSens.CreatureReference, pendingCreatureSens.PlayerReference, pendingCreatureSens.SensType);
					_pendingCreatureSensList.RemoveAt(num);
				}
			}
		}

		private void HandleLocalHit(RaycastHit hit)
		{
			_hasTipHit = true;
			_pendingCreatureSensList.Clear();
			_active = false;
			if (hit.collider != null && hit.collider.name.Equals("UnderWater Volume"))
			{
				return;
			}
			OnTipHit?.Invoke(hit);
			if (hit.collider == null)
			{
				return;
			}
			hit.collider.GetComponentInParent<CrabSmallController>()?.TryDodge(hit.point);
			if (!(Player == null) && !(Player.SurfaceChecker == null))
			{
				Player.UIHitFeedback();
				string text = (hit.collider.TryGetComponent<SeaCreature>(out var _) ? "fish" : (hit.collider.TryGetComponent<PPG_Player>(out var _) ? "player" : ((!hit.collider.TryGetComponent<Terrain>(out var _)) ? "Tile" : Player.SurfaceChecker.TryGetTerrainSurface(hit.collider.transform, hit.point).ToString())));
				if (base.IsSpawned)
				{
					ServerTipHitRpc(hit.point, hit.normal, text);
				}
				SpawnImpactVfxAtPoint(hit.point, hit.normal, text);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTipHitRpc(Vector3 hitPoint, Vector3 hitNormal, FixedString64Bytes surfaceTag)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3708868204u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				bufferWriter.WriteValueSafe(in surfaceTag, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 3708868204u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ClientSpawnImpactVfxRpc(hitPoint, hitNormal, surfaceTag);
			if (!(surfaceTag.ToString() != "Sand"))
			{
				if (_pickupHandler == null)
				{
					ServiceLocator.ForSceneOf(this).Get<WorldPickupHandler>(out _pickupHandler);
				}
				if (!(_pickupHandler == null) && (!_firstTimeScrap || Random.value > 0.9f))
				{
					_firstTimeScrap = true;
					_pickupHandler.ServerSpawnOneScrapAtPosition(hitPoint);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientSpawnImpactVfxRpc(Vector3 hitPoint, Vector3 hitNormal, FixedString64Bytes surfaceTag)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3027821128u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				bufferWriter.WriteValueSafe(in surfaceTag, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 3027821128u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SpawnImpactVfxAtPoint(hitPoint, hitNormal, surfaceTag.ToString());
			}
		}

		private void SpawnImpactVfxAtPoint(Vector3 hitPoint, Vector3 hitNormal, string surfaceTag)
		{
			ParticleSystem particleSystem = surfaceTag switch
			{
				"Water" => _waterPS, 
				"Sand" => _sandPS, 
				"Grass" => _grassPS, 
				"fish" => _fishPS, 
				"player" => _playerPS, 
				_ => _tilePS, 
			};
			SoundType type = surfaceTag switch
			{
				"Water" => SoundType.Harpoon_Hit_Water, 
				"Sand" => SoundType.Harpoon_Hit_Sand, 
				"Grass" => SoundType.Harpoon_Hit_Sand, 
				"Tile" => SoundType.Harpoon_Hit_Concrete, 
				"Rock" => SoundType.Harpoon_Hit_Concrete, 
				_ => SoundType.Harpoon_Hit_Flesh, 
			};
			if (!(particleSystem == null))
			{
				Quaternion rotation = ((hitNormal.sqrMagnitude > 0.0001f) ? Quaternion.LookRotation(hitNormal) : Quaternion.identity);
				Object.Instantiate(particleSystem, hitPoint, rotation).Play();
				if (LogicTip != null)
				{
					LogicTip.position = hitPoint;
				}
				Singleton<SoundManager>.Instance.PlaySFX(type, hitPoint);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCreatureSensRpc(NetworkObjectReference creatureReference, NetworkObjectReference playerReference, byte sensType)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1118667063u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in sensType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1118667063u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<SeaCreature>(out var component) && playerReference.TryGetComponent<PPG_Player>(out var component2))
				{
					component.ServerCreatureSens(component2, sensType);
				}
			}
		}

		private void OnDrawGizmosSelected()
		{
			if ((bool)LogicTip)
			{
				Gizmos.color = debugColor;
				Gizmos.DrawWireSphere(LogicTip.position, sphereRadius);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3708868204u, __rpc_handler_3708868204, "ServerTipHitRpc", RpcInvokePermission.Everyone);
			__registerRpc(3027821128u, __rpc_handler_3027821128, "ClientSpawnImpactVfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(1118667063u, __rpc_handler_1118667063, "ServerCreatureSensRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3708868204(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out FixedString64Bytes value3, default(FastBufferWriter.ForFixedStrings));
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTipSensor)target).ServerTipHitRpc(value, value2, value3);
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3027821128(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out FixedString64Bytes value3, default(FastBufferWriter.ForFixedStrings));
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTipSensor)target).ClientSpawnImpactVfxRpc(value, value2, value3);
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1118667063(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTipSensor)target).ServerCreatureSensRpc(value, value2, value3);
				((HarpoonTipSensor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "HarpoonTipSensor";
		}
	}
}
