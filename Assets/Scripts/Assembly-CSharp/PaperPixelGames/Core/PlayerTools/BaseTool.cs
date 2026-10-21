using System;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.PlayerTools
{
	public abstract class BaseTool : NetworkBehaviour
	{
		protected const string LAYER_OWNER_TPS = "Player";

		protected const string LAYER_REMOTE_TPS = "ClientPlayer";

		protected const string LAYER_FPS = "ViewModel";

		private readonly NetworkVariable<bool> _nvToolStatus = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		[SerializeField]
		protected PPG_Player _player;

		[SerializeField]
		protected bool _debugActive;

		[SerializeField]
		protected Transform _fpsGraphicParent;

		[SerializeField]
		protected Transform _tpsGraphicParent;

		protected GameObject _fpsGraphic;

		protected GameObject _tpsGraphic;

		protected bool _hasAppliedLevel;

		protected byte _appliedLevel;

		public virtual string Name => "";

		public string LocalizedName => LocalizationDatabase.GetString("UiTable", "wheel.tool." + LocalizationDatabase.ToSnakeCase(Name), Name);

		public virtual Sprite Icon => null;

		public virtual bool CapturesInteractInput => false;

		public abstract byte Level { get; }

		public abstract bool IsUnlocked { get; }

		public bool ToolStatus => _nvToolStatus.Value;

		protected virtual bool ShouldShowCloseToolHint => true;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> nvToolStatus = _nvToolStatus;
			nvToolStatus.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(nvToolStatus.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnToolStatusChanged));
			NetworkSpawn();
			ApplyToolStatus(_nvToolStatus.Value);
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<bool> nvToolStatus = _nvToolStatus;
			nvToolStatus.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(nvToolStatus.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnToolStatusChanged));
			DestroyGraphics();
			_hasAppliedLevel = false;
			_appliedLevel = 0;
			NetworkDespawn();
		}

		private void OnToolStatusChanged(bool oldValue, bool newValue)
		{
			UtilityMethods.Log($"[BaseTool - {base.OwnerClientId} - {base.name}] OnToolStatusChanged - Status:{newValue}");
			ApplyToolStatus(newValue);
		}

		protected abstract void SetUnlock(bool newValue);

		public abstract void LoadLevel(ToolDataNet toolData);

		protected virtual void NetworkSpawn()
		{
		}

		protected virtual void NetworkDespawn()
		{
		}

		public virtual void Interact(InputActionPhase phase)
		{
		}

		public virtual void Mouse1(InputActionPhase phase)
		{
		}

		public virtual void RapidPress(InputActionPhase phase)
		{
		}

		public virtual void Mouse2(InputActionPhase phase)
		{
		}

		protected virtual void EnableHints()
		{
		}

		protected virtual void DisableHints()
		{
		}

		public abstract bool DisableIf();

		public void ToggleTool(bool forceActive = false)
		{
			if (base.IsOwner)
			{
				bool value = _nvToolStatus.Value;
				if (!value)
				{
					_player.TabletController.CloseTablet();
					Singleton<QuickSlotHandler>.Instance.DeselectSlot();
				}
				_nvToolStatus.Value = forceActive || !value;
			}
		}

		public void ForceDisableTool()
		{
			if (base.IsOwner)
			{
				if (_nvToolStatus.Value)
				{
					_nvToolStatus.Value = false;
				}
				_player?.ClearToolIfActive(this);
			}
		}

		protected virtual void ApplyToolStatus(bool status)
		{
			UtilityMethods.Log($"[BaseTool - {base.OwnerClientId} - {base.name}] ApplyToolStatus Status {status}");
			if (_tpsGraphicParent != null && _tpsGraphicParent.gameObject.activeSelf != status)
			{
				_tpsGraphicParent.gameObject.SetActive(status);
			}
			if (base.IsOwner && _fpsGraphicParent != null && _fpsGraphicParent.gameObject.activeSelf != status)
			{
				_fpsGraphicParent.gameObject.SetActive(status);
			}
			if (base.IsOwner)
			{
				if (status)
				{
					EnableHints();
					RefreshCloseToolHint();
				}
				else
				{
					DisableHints();
					DisableCloseToolHint();
				}
			}
		}

		public void RefreshCloseToolHint()
		{
			if (base.IsOwner && !(Singleton<HintsManager>.Instance == null) && ToolStatus)
			{
				if (!ShouldShowCloseToolHint)
				{
					DisableCloseToolHint();
				}
				else
				{
					AddCloseToolHint();
				}
			}
		}

		protected void DisableCloseToolHint()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("OpenTools");
				BaseTool baseTool = FindCloseToolHintOwner();
				if (baseTool != null)
				{
					baseTool.AddCloseToolHint();
				}
			}
		}

		private void AddCloseToolHint()
		{
			Singleton<HintsManager>.Instance.AddHint("OpenTools", "key_hold_unequip");
		}

		private BaseTool FindCloseToolHintOwner()
		{
			if (_player == null)
			{
				return null;
			}
			BaseTool[] componentsInChildren = _player.GetComponentsInChildren<BaseTool>(includeInactive: true);
			foreach (BaseTool baseTool in componentsInChildren)
			{
				if (!(baseTool == null) && !(baseTool == this) && baseTool.IsOwner && baseTool.ToolStatus && baseTool.ShouldShowCloseToolHint)
				{
					return baseTool;
				}
			}
			return null;
		}

		public virtual void ApplyLevel(byte newLevel)
		{
			if (_hasAppliedLevel && _appliedLevel == newLevel)
			{
				UtilityMethods.Log($"[BaseTool - {base.OwnerClientId} - {base.name}] ApplyLevel skipped. Same level: {newLevel}");
				return;
			}
			_hasAppliedLevel = true;
			_appliedLevel = newLevel;
			UtilityMethods.Log($"[BaseTool - {base.OwnerClientId} - {base.name}] ApplyLevel Level {newLevel}");
			DestroyGraphics();
			SpawnGraphics();
			SetEquipmentLayer();
			ApplyToolStatus(ToolStatus);
		}

		protected virtual void SpawnGraphics()
		{
			UtilityMethods.Log($"[BaseTool - {base.OwnerClientId} - {base.name}] SpawnGraphics");
			GameObject graphicByLevel = GetGraphicByLevel();
			if (graphicByLevel != null && _tpsGraphicParent != null)
			{
				_tpsGraphic = UnityEngine.Object.Instantiate(graphicByLevel, _tpsGraphicParent);
			}
			if (base.IsOwner)
			{
				GameObject graphicByLevel2 = GetGraphicByLevel();
				if (graphicByLevel2 != null && _fpsGraphicParent != null)
				{
					_fpsGraphic = UnityEngine.Object.Instantiate(graphicByLevel2, _fpsGraphicParent);
				}
			}
		}

		protected virtual void DestroyGraphics()
		{
			if (_tpsGraphic != null)
			{
				UnityEngine.Object.Destroy(_tpsGraphic);
				_tpsGraphic = null;
			}
			if (_fpsGraphic != null)
			{
				UnityEngine.Object.Destroy(_fpsGraphic);
				_fpsGraphic = null;
			}
		}

		protected virtual void SetEquipmentLayer()
		{
			if (_tpsGraphic != null)
			{
				UtilityMethods.SetLayerRecursive(_tpsGraphic, base.IsOwner ? "Player" : "ClientPlayer", includeChildren: true);
			}
			if (base.IsOwner && _fpsGraphic != null)
			{
				UtilityMethods.SetLayerRecursive(_fpsGraphic, "ViewModel", includeChildren: true);
			}
		}

		protected abstract GameObject GetGraphicByLevel();

		protected abstract int GetLevelCount();

		private void OnValidate()
		{
			if (_tpsGraphicParent == null)
			{
				Debug.LogWarning(base.name + ": _tpsGraphicParent is null", this);
			}
			if (_fpsGraphicParent == null)
			{
				Debug.LogWarning(base.name + ": _fpsGraphicParent is null", this);
			}
		}

		protected override void __initializeVariables()
		{
			if (_nvToolStatus == null)
			{
				throw new Exception("BaseTool._nvToolStatus cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_nvToolStatus.Initialize(this);
			__nameNetworkVariable(_nvToolStatus, "_nvToolStatus");
			NetworkVariableFields.Add(_nvToolStatus);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "BaseTool";
		}
	}
}
