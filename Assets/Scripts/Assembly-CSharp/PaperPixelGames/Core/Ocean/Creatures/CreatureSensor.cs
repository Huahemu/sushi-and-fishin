using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	[RequireComponent(typeof(SphereCollider))]
	public sealed class CreatureSensor : MonoBehaviour
	{
		[Header("Sensor")]
		[SerializeField]
		private SphereCollider _collider;

		[SerializeField]
		private bool _log;

		public SphereCollider Collider => _collider;

		public bool IsActive
		{
			get
			{
				if (_collider != null)
				{
					return _collider.enabled;
				}
				return false;
			}
		}

		public event Action<Collider> DetectedEnter;

		public event Action<Collider> DetectedExit;

		private void Awake()
		{
			CacheCollider();
			EnsureTrigger();
		}

		private void Reset()
		{
			CacheCollider();
			EnsureTrigger();
		}

		public void SetSensor(CreatureSO creatureSO)
		{
			if (creatureSO == null)
			{
				Log("SetSensor failed. CreatureSO is null.", LogType.Warning);
			}
			else
			{
				SetSensor(creatureSO.SensorRadius, creatureSO.SensorCenter);
			}
		}

		public void SetSensor(float radius, Vector3 center)
		{
			CacheCollider();
			if (_collider == null)
			{
				Log("SetSensor failed. SphereCollider is null.", LogType.Warning);
				return;
			}
			_collider.radius = Mathf.Max(0.01f, radius);
			_collider.center = center;
			EnsureTrigger();
		}

		public void SetActive(bool active)
		{
			CacheCollider();
			if (!(_collider == null))
			{
				_collider.enabled = active;
			}
		}

		public void ClearListeners()
		{
			DetectedEnter = null;
			DetectedExit = null;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (CanEmit(other))
			{
				DetectedEnter?.Invoke(other);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (CanEmit(other))
			{
				DetectedExit?.Invoke(other);
			}
		}

		private bool CanEmit(Collider other)
		{
			if (other == null)
			{
				return false;
			}
			if (other.transform == base.transform)
			{
				return false;
			}
			if (other.transform.IsChildOf(base.transform))
			{
				return false;
			}
			return true;
		}

		private void CacheCollider()
		{
			if (!(_collider != null))
			{
				TryGetComponent<SphereCollider>(out _collider);
			}
		}

		private void EnsureTrigger()
		{
			if (!(_collider == null))
			{
				_collider.isTrigger = true;
			}
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[CreatureSensor] " + message, type);
			}
		}
	}
}
