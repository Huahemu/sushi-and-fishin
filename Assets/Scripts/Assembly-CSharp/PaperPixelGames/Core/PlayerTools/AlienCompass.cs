using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.PlayerTools
{
	public class AlienCompass : BaseTool
	{
		[SerializeField]
		private CompassData _data;

		private CompassNeedle3D _fpsCompassNeedle;

		private CompassNeedle3D _tpsCompassNeedle;

		private int _currentTargetIndex = -1;

		private Transform _routeTarget;

		private byte _compassLevel;

		private bool _compassUnlocked;

		private RpcRelay _relay;

		public override byte Level => _player.ToolsData.CompassLevel;

		public override bool IsUnlocked => _player.ToolsData.CompassUnlocked;

		public override string Name => _data.Name;

		public override Sprite Icon => _data.Sprite;

		private void Start()
		{
			_tpsGraphicParent.gameObject.SetActive(value: false);
			_fpsGraphicParent.gameObject.SetActive(value: false);
			_tpsGraphicParent.SetParent(_player.RigBones.TpsRig.RightHand);
			_tpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			if (base.IsOwner)
			{
				_fpsGraphicParent.SetParent(_player.RigBones.FpsRig.RightHand);
				_fpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
		}

		public bool UpdateNextLevel()
		{
			int num = GetLevelCount() - 1;
			if (num < 0 || _compassLevel >= num)
			{
				UtilityMethods.Log("[Compass] UpdateNextLevel failed: compass is max level!");
				return false;
			}
			if (!TryResolveRelay())
			{
				UtilityMethods.Log("[Compass] UpdateNextLevel failed!");
				return false;
			}
			_relay.ServerUpgradeCompassToNextLevelRpc();
			return true;
		}

		public override bool DisableIf()
		{
			return true;
		}

		public override void LoadLevel(ToolDataNet data)
		{
			_compassUnlocked = data.CompassUnlocked;
			_compassLevel = data.CompassLevel;
			ApplyLevel(_compassLevel);
			if (base.IsOwner)
			{
				SetUnlock(_compassUnlocked);
			}
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			Singleton<PPG_WheelManager>.Instance.UnlockCompass(isUnlocked);
		}

		protected override GameObject GetGraphicByLevel()
		{
			if (_data == null || _data.Levels == null || _data.Levels.Count == 0)
			{
				return null;
			}
			int index = Mathf.Clamp(Level, 0, _data.Levels.Count - 1);
			if (!(_data.Levels[index] != null))
			{
				return null;
			}
			return _data.Levels[index].GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			if (!(_data != null) || _data.Levels == null)
			{
				return 0;
			}
			return _data.Levels.Count;
		}

		protected override void SpawnGraphics()
		{
			base.SpawnGraphics();
			_tpsCompassNeedle = ((_tpsGraphic != null) ? _tpsGraphic.GetComponent<CompassNeedle3D>() : null);
			RefreshNeedleTargets();
			if (_data == null)
			{
				return;
			}
			ItemOffset itemOffset = ((Level == 0) ? _data.Level0_OffsetTps : _data.OffsetTps);
			ItemOffset itemOffset2 = ((Level == 0) ? _data.Level0_OffsetFps : _data.OffsetFps);
			if (_tpsGraphic != null)
			{
				_tpsGraphic.transform.SetLocalPositionAndRotation(itemOffset.PositionOffset, Quaternion.Euler(itemOffset.RotationOffset));
			}
			if (_tpsGraphic != null && itemOffset.ScaleOffset != Vector3.zero)
			{
				_tpsGraphic.transform.localScale = itemOffset.ScaleOffset;
			}
			if (base.IsOwner)
			{
				_fpsCompassNeedle = ((_fpsGraphic != null) ? _fpsGraphic.GetComponent<CompassNeedle3D>() : null);
				RefreshNeedleTargets();
				if (_fpsGraphic != null)
				{
					_fpsGraphic.transform.SetLocalPositionAndRotation(itemOffset2.PositionOffset, Quaternion.Euler(itemOffset2.RotationOffset));
				}
				if (_fpsGraphic != null && itemOffset2.ScaleOffset != Vector3.zero)
				{
					_fpsGraphic.transform.localScale = itemOffset2.ScaleOffset;
				}
			}
		}

		public void SetTarget(Transform target, byte currentTargetIndex)
		{
			_currentTargetIndex = currentTargetIndex;
			_routeTarget = target;
			RefreshNeedleTargets();
		}

		private void RefreshNeedleTargets()
		{
			if (_tpsCompassNeedle != null)
			{
				_tpsCompassNeedle.SetTarget(_routeTarget);
			}
			if (base.IsOwner && _fpsCompassNeedle != null)
			{
				_fpsCompassNeedle.SetTarget(_routeTarget);
			}
		}

		protected override void NetworkDespawn()
		{
			_routeTarget = null;
			base.NetworkDespawn();
		}

		protected override void ApplyToolStatus(bool status)
		{
			base.ApplyToolStatus(status);
			CompassStatus(status);
			if (status)
			{
				_player.EquipHoldProfile(1);
				if (_compassUnlocked)
				{
					Singleton<GameProgressManager>.Instance?.RestaurantManager?.RefreshCompassTargetForLocalPlayer();
					RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance?.RestaurantManager;
					if (base.IsOwner && restaurantManager != null && restaurantManager.TryGetCompassTargetIndexForLevel(_compassLevel, out var targetIndex) && (restaurantManager.CurrentCompassTargetLevel != _compassLevel || _currentTargetIndex != targetIndex))
					{
						restaurantManager.ServerSetCompassTargetRpc(_compassLevel);
					}
				}
			}
			_player.SetHolding(status, startHold: true);
		}

		private void CompassStatus(bool status)
		{
			if (status)
			{
				if (_tpsCompassNeedle != null)
				{
					_tpsCompassNeedle.Activate();
				}
				if (base.IsOwner && _fpsCompassNeedle != null)
				{
					_fpsCompassNeedle.Activate();
				}
			}
			else
			{
				if (_tpsCompassNeedle != null)
				{
					_tpsCompassNeedle.Deactivate();
				}
				if (base.IsOwner && _fpsCompassNeedle != null)
				{
					_fpsCompassNeedle.Deactivate();
				}
			}
		}

		private bool TryResolveRelay()
		{
			if (_relay != null)
			{
				return true;
			}
			ServiceLocator.ForSceneOf(this).Get<RpcRelay>(out _relay);
			return _relay != null;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "AlienCompass";
		}
	}
}
