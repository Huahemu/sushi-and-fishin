using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Wheel;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public class Mop : BaseTool
	{
		[SerializeField]
		private MopData _data;

		[SerializeField]
		private MopUI _mopUI;

		[SerializeField]
		private ItemOffset _fpsOffset;

		[SerializeField]
		private ItemOffset _tpsOffset;

		public override byte Level => _player.ToolsData.MopLevel;

		public override bool IsUnlocked => false;

		public override string Name => _data.Name;

		public override Sprite Icon => _data.Sprite;

		public override bool DisableIf()
		{
			return true;
		}

		public override void LoadLevel(ToolDataNet data)
		{
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			Singleton<PPG_WheelManager>.Instance.UnlockMop(isUnlocked);
		}

		protected override GameObject GetGraphicByLevel()
		{
			byte mopLevel = _player.ToolsData.MopLevel;
			return _data.Levels[mopLevel].GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			return _data.Levels.Count;
		}

		protected override void EnableHints()
		{
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_sweep");
		}

		protected override void DisableHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

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

		private void Update()
		{
			_ = base.IsOwner;
		}

		private void UpdateDebugUI()
		{
			if (!base.IsOwner || !_debugActive || !base.ToolStatus)
			{
				_mopUI.ClearDebug();
			}
			else
			{
				_mopUI.UpdateDebugUI(Level);
			}
		}

		protected override void SpawnGraphics()
		{
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
			return "Mop";
		}
	}
}
