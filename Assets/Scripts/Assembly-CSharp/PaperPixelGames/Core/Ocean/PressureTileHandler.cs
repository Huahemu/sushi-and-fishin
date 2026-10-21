using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace PaperPixelGames.Core.Ocean
{
	public class PressureTileHandler : NetworkBehaviour
	{
		[Header("Tiles")]
		[SerializeField]
		private List<PressureTile> _tiles = new List<PressureTile>();

		[Header("Events")]
		[SerializeField]
		private UnityEvent _onResolved;

		private readonly NetworkVariable<bool> _isResolved = new NetworkVariable<bool>(value: false);

		public bool IsResolved => _isResolved.Value;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isResolved = _isResolved;
			isResolved.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isResolved.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnResolved));
			if (base.IsServer)
			{
				RegisterAllTiles();
				EvaluateResolved();
			}
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<bool> isResolved = _isResolved;
			isResolved.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isResolved.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnResolved));
		}

		public void RegisterTile(PressureTile tile)
		{
			if (base.IsServer && !(tile == null) && !_tiles.Contains(tile))
			{
				_tiles.Add(tile);
			}
		}

		public void UnregisterTile(PressureTile tile)
		{
			if (base.IsServer && !(tile == null))
			{
				_tiles.Remove(tile);
			}
		}

		public void HandleTileStateChanged(PressureTile changedTile)
		{
			if (base.IsServer && !_isResolved.Value)
			{
				EvaluateResolved();
			}
		}

		private void RegisterAllTiles()
		{
			for (int i = 0; i < _tiles.Count; i++)
			{
				if (!(_tiles[i] == null))
				{
					_tiles[i].SetHandler(this);
				}
			}
		}

		private void EvaluateResolved()
		{
			if (_isResolved.Value || _tiles.Count == 0)
			{
				return;
			}
			for (int i = 0; i < _tiles.Count; i++)
			{
				PressureTile pressureTile = _tiles[i];
				if (pressureTile == null || !pressureTile.IsActive)
				{
					return;
				}
			}
			Resolve();
		}

		private void Resolve()
		{
			if (!base.IsServer || _isResolved.Value)
			{
				return;
			}
			_isResolved.Value = true;
			for (int i = 0; i < _tiles.Count; i++)
			{
				if (!(_tiles[i] == null))
				{
					_tiles[i].ForceLockPressedResolvedState();
				}
			}
		}

		private void OnResolved(bool old, bool @new)
		{
			_onResolved?.Invoke();
		}

		protected override void __initializeVariables()
		{
			if (_isResolved == null)
			{
				throw new Exception("PressureTileHandler._isResolved cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isResolved.Initialize(this);
			__nameNetworkVariable(_isResolved, "_isResolved");
			NetworkVariableFields.Add(_isResolved);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "PressureTileHandler";
		}
	}
}
