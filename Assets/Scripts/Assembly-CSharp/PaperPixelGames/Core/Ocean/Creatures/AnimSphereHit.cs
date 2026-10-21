using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public class AnimSphereHit : MonoBehaviour
	{
		[SerializeField]
		private MonoBehaviour _receiverBehaviour;

		[Header("Hit Settings")]
		[SerializeField]
		private Transform _origin;

		[SerializeField]
		private Vector3 _originOffset = Vector3.zero;

		[SerializeField]
		private float _radius = 0.5f;

		[SerializeField]
		private float _distance = 1.5f;

		[SerializeField]
		private LayerMask _hitMask;

		[Header("Debug")]
		[SerializeField]
		private bool _debugLogs = true;

		private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

		private IAnimSphereHitReceiver _cachedReceiver;

		public void Anim_DoSphereHit()
		{
			IAnimSphereHitReceiver animSphereHitReceiver = ResolveReceiver();
			if (animSphereHitReceiver == null)
			{
				UtilityMethods.Log("[AnimSphereHit] Receiver is null or does not implement IAnimSphereHitReceiver.", LogType.Warning);
				return;
			}
			if (!animSphereHitReceiver.CanReceiveAnimSphereHit)
			{
				Log("Blocked. Receiver cannot process hit.");
				return;
			}
			if (_origin == null)
			{
				_origin = base.transform;
			}
			int num = Physics.SphereCastNonAlloc(_origin.TransformPoint(_originOffset), direction: _origin.forward, radius: _radius, results: _hitBuffer, maxDistance: _distance, layerMask: _hitMask, queryTriggerInteraction: QueryTriggerInteraction.Collide);
			if (num <= 0)
			{
				Log("Miss. No collider hit.");
				animSphereHitReceiver.ReceiveAnimSphereHit(null);
				return;
			}
			Collider collider = null;
			for (int i = 0; i < num; i++)
			{
				Collider collider2 = _hitBuffer[i].collider;
				if (!(collider2 == null) && collider2.CompareTag("Player"))
				{
					collider = collider2;
					break;
				}
			}
			if (collider == null)
			{
				Log("Miss. Hit colliders found, but no Player collider resolved.");
				animSphereHitReceiver.ReceiveAnimSphereHit(null);
			}
			else
			{
				animSphereHitReceiver.ReceiveAnimSphereHit(collider);
				Log("Hit collider=" + collider.name);
			}
		}

		private IAnimSphereHitReceiver ResolveReceiver()
		{
			if (_cachedReceiver != null)
			{
				return _cachedReceiver;
			}
			if (_receiverBehaviour is IAnimSphereHitReceiver cachedReceiver)
			{
				_cachedReceiver = cachedReceiver;
				return _cachedReceiver;
			}
			MonoBehaviour[] componentsInParent = GetComponentsInParent<MonoBehaviour>(includeInactive: true);
			for (int i = 0; i < componentsInParent.Length; i++)
			{
				if (componentsInParent[i] is IAnimSphereHitReceiver cachedReceiver2)
				{
					_cachedReceiver = cachedReceiver2;
					return _cachedReceiver;
				}
			}
			return null;
		}

		private void Log(string message)
		{
			if (_debugLogs)
			{
				UtilityMethods.Log("[AnimSphereHit] " + message);
			}
		}
	}
}
