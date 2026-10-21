using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools.Harpoon;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Events;

namespace PaperPixelGames.Core.Ocean
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(NetworkTransform))]
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(Collider))]
	public class PullableRock : NetworkBehaviour
	{
		[Header("Pull Settings")]
		[SerializeField]
		private float _pullForce = 100f;

		[SerializeField]
		private float _upwardForce = 50f;

		[SerializeField]
		private int _minLevel = 1;

		[SerializeField]
		private float _minImpactSpeed = 3f;

		[SerializeField]
		private float _impactCooldown = 0.25f;

		[SerializeField]
		private LayerMask _impactMask;

		[Header("FX")]
		[SerializeField]
		private GameObject _impactVfxPrefab;

		[SerializeField]
		private Vector3 _impactVfxOffset = Vector3.zero;

		[SerializeField]
		private bool _alignImpactToSurfaceNormal = true;

		[SerializeField]
		private float _impactVfxLifeTime = 3f;

		[SerializeField]
		private AudioClipSO _impactVfxClip;

		[Header("Events")]
		[SerializeField]
		private UnityEvent _onPulled;

		[SerializeField]
		private UnityEvent _onImpact;

		[Header("Debug")]
		[SerializeField]
		private bool _logDebug;

		private int _currentTotalLevel;

		private Rigidbody _rigidbody;

		private Coroutine _durationCor;

		private bool _pullSuccess;

		private HashSet<HarpoonTool> _attachedHarpoons = new HashSet<HarpoonTool>();

		private float _lastImpactTime;

		private bool _wasPulled;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		private void Awake()
		{
			_rigidbody = GetComponent<Rigidbody>();
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (((1 << collision.gameObject.layer) & (int)_impactMask) != 0 && _wasPulled && !(Time.time - _lastImpactTime < _impactCooldown) && !(collision.relativeVelocity.magnitude < _minImpactSpeed))
			{
				_lastImpactTime = Time.time;
				_wasPulled = false;
				ContactPoint contact = collision.GetContact(0);
				PlayImpact(contact.point, contact.normal);
			}
		}

		private void PlayImpact(Vector3 position, Vector3 normal)
		{
			Vector3 position2 = position + _impactVfxOffset;
			if (_impactVfxPrefab != null)
			{
				Quaternion rotation = (_alignImpactToSurfaceNormal ? Quaternion.LookRotation(normal) : Quaternion.identity);
				GameObject obj = Object.Instantiate(_impactVfxPrefab, position2, rotation);
				if (_impactVfxLifeTime > 0f)
				{
					Object.Destroy(obj, _impactVfxLifeTime);
				}
			}
			Singleton<SoundManager>.Instance.PlaySFX(_impactVfxClip, base.transform.position, base.transform);
			_onImpact?.Invoke();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_rigidbody != null)
			{
				_rigidbody.isKinematic = true;
				_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			}
		}

		public void RequestPull(HarpoonTool harpoon)
		{
			ServerPullRockRpc(harpoon.NetworkObject.ToReference());
		}

		private void EndTimer()
		{
			if (!_pullSuccess)
			{
				foreach (HarpoonTool attachedHarpoon in _attachedHarpoons)
				{
					attachedHarpoon.OwnerFailPullableRpc();
				}
			}
			_currentTotalLevel = 0;
			_durationCor = null;
			_pullSuccess = false;
			_attachedHarpoons.Clear();
		}

		[Rpc(SendTo.Server)]
		private void ServerPullRockRpc(NetworkObjectReference playerRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2835594924u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2835594924u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!playerRef.TryGetComponent<PPG_Player>(out var component))
			{
				UtilityMethods.Log("[PullableRock] ServerPullRockRpc player not found!", LogType.Warning);
				return;
			}
			if (_durationCor == null)
			{
				_durationCor = Singleton<DelayHelper>.Instance.DelayAction(0.5f, EndTimer);
			}
			_currentTotalLevel += component.Harpoon.Level + 1;
			if (!CanApplyPull())
			{
				_attachedHarpoons.Add(component.Harpoon);
				return;
			}
			_pullSuccess = true;
			Vector3 position = component.RigBones.TpsRig.Spine.position;
			Vector3 vector = position - base.transform.position;
			if (!(vector.sqrMagnitude <= 0.0001f))
			{
				vector.Normalize();
				Vector3 vector2 = vector * _pullForce;
				vector2 += Vector3.up * _upwardForce;
				_rigidbody.isKinematic = false;
				_rigidbody.AddForce(vector2, ForceMode.Impulse);
				_currentTotalLevel = 0;
				if (_logDebug)
				{
					UtilityMethods.Log("<b><color=#57C7FF>[PullableRock]</color></b> Pulled: <b>" + base.name + "</b>\n" + $"    PullerPos   : {position}\n" + $"    Direction   : {vector}\n" + $"    Force       : {vector2}\n");
				}
				OnPulledClientRpc();
			}
		}

		private bool CanApplyPull()
		{
			if (_rigidbody == null)
			{
				return false;
			}
			if (!base.gameObject.activeInHierarchy)
			{
				return false;
			}
			if (_currentTotalLevel < _minLevel)
			{
				return false;
			}
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void OnPulledClientRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(704605839u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 704605839u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_wasPulled = true;
				_onPulled?.Invoke();
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2835594924u, __rpc_handler_2835594924, "ServerPullRockRpc", RpcInvokePermission.Everyone);
			__registerRpc(704605839u, __rpc_handler_704605839, "OnPulledClientRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2835594924(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((PullableRock)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PullableRock)target).ServerPullRockRpc(value);
				((PullableRock)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_704605839(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PullableRock)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PullableRock)target).OnPulledClientRpc();
				((PullableRock)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PullableRock";
		}
	}
}
