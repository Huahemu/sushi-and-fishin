using PaperPixelGames.Core.Kitchen.ServedDishes;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public class ConveyorBeltTableTrigger : MonoBehaviour
	{
		[SerializeField]
		private ConveyorBeltTable _table;

		private Collider _probeCollider;

		private void Awake()
		{
			ResolveReferences();
		}

		private void OnEnable()
		{
			ResolveReferences();
			_table?.RegisterDishClaimProbe(this);
		}

		private void OnDisable()
		{
			_table?.UnregisterDishClaimProbe(this);
		}

		private void Reset()
		{
			ResolveReferences();
		}

		private void ResolveReferences()
		{
			if (_table == null)
			{
				_table = GetComponentInParent<ConveyorBeltTable>();
			}
			if (_probeCollider == null)
			{
				_probeCollider = GetComponent<Collider>();
			}
		}

		public bool TryGetProbeBounds(out Bounds bounds)
		{
			if (_probeCollider == null)
			{
				_probeCollider = GetComponent<Collider>();
			}
			if (_probeCollider == null || !_probeCollider.enabled)
			{
				bounds = default;
				return false;
			}
			bounds = _probeCollider.bounds;
			return true;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsServer && !(_table == null))
			{
				ServedDishSushi componentInParent = other.GetComponentInParent<ServedDishSushi>();
				if (!(componentInParent == null))
				{
					_table.ServerTryClaimDishFromTrigger(componentInParent);
				}
			}
		}
	}
}
