using System.Collections.Generic;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.ScrapSystem
{
	public class ScrapForge : NetworkBehaviour
	{
		[Header("Visual")]
		[SerializeField]
		private Transform _toolPoint;

		[Header("User Info")]
		[SerializeField]
		private Image _avatarImage;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[Header("InfoText")]
		[SerializeField]
		private TextMeshProUGUI _goldText;

		[SerializeField]
		private TextMeshProUGUI _scrapText;

		[SerializeField]
		private ScrapForgeLever _lever;

		[Header("Data")]
		[SerializeField]
		private DivingEquipmentData _suitData;

		[SerializeField]
		private HarpoonData _harpoonData;

		[SerializeField]
		private FishingRodData _rodData;

		[SerializeField]
		private MopData _mopData;

		private ulong _prevClientId = 3000uL;

		private ToolType _selectedTool;

		private byte _currentPreviewLevelIndex;

		private int _currentUnlockCost;

		private int _currentUpgradeCost;

		private PPG_Player _currentPlayer;

		[Header("Buttons")]
		[SerializeField]
		private List<ScrapForgeButton> _toolButtons;

		[SerializeField]
		private List<ScrapForgeButton> _levelButtons;

		private GameObject _currentToolGraphic;

		private async void UpdateUserInfoUI(SteamId steamId)
		{
			if (SteamClient.IsValid)
			{
				Sprite sprite = await SteamAvatarCache.GetAvatarSpriteAsync(steamId);
				if (sprite != null)
				{
					_avatarImage.sprite = sprite;
					_avatarImage.gameObject.SetActive(value: true);
				}
				_nameText.text = SteamClient.Name;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSyncUserInfoRpc(ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3608110651u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 3608110651u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_prevClientId != clientId)
				{
					_prevClientId = clientId;
					ClientSyncUserInfoRpc(clientId);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSyncUserInfoRpc(ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4102414861u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 4102414861u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player))
				{
					UtilityMethods.Log("[ScrapForge] ClientSyncUserInfoRpc TryGetPlayer failed");
					return;
				}
				_currentPlayer = player;
				UpdateUserInfoUI(_currentPlayer.SteamID.Value);
			}
		}

		private void Awake()
		{
			_avatarImage.sprite = null;
			_avatarImage.gameObject.SetActive(value: false);
			_nameText.text = string.Empty;
			_goldText.text = string.Empty;
			_scrapText.text = string.Empty;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_lever.Setup(this);
			_toolButtons.ForEach((ScrapForgeButton x) =>
			{
				x.Setup(this);
			});
			_levelButtons.ForEach((ScrapForgeButton x) =>
			{
				x.Setup(this);
			});
		}

		public bool OnLeverPull()
		{
			if (_selectedTool == ToolType.Empty || _currentPlayer == null)
			{
				return false;
			}
			ServerRequestUpgradeRpc(_selectedTool, _currentPreviewLevelIndex);
			return true;
		}

		public void OnToolButtonClick(int toolIndex)
		{
			ServerSyncUserInfoRpc(Singleton<PlayersManager>.Instance.LocalPlayer.OwnerClientId);
			ToolType toolType = toolIndex switch
			{
				0 => ToolType.DivingEquipment, 
				1 => ToolType.Harpoon, 
				2 => ToolType.FishingRod, 
				3 => ToolType.Mop, 
				_ => ToolType.Empty, 
			};
			if (toolType == ToolType.Empty)
			{
				UtilityMethods.Log("[ScrapForge] Invalid tool index");
				return;
			}
			_selectedTool = toolType;
			if (!(_currentPlayer == null))
			{
				int playerLevelIndex = GetPlayerLevelIndex(_selectedTool);
				int maxLevelIndex = GetMaxLevelIndex(_selectedTool);
				if (maxLevelIndex >= 0)
				{
					ServerRequestPreviewRpc(targetLevelIndex: _currentPreviewLevelIndex = (byte)Mathf.Clamp(playerLevelIndex, 0, maxLevelIndex), toolType: _selectedTool);
				}
			}
		}

		public void OnLevelButtonClick(int levelIndex)
		{
			if (_selectedTool != ToolType.Empty && !(_currentPlayer == null))
			{
				int maxLevelIndex = GetMaxLevelIndex(_selectedTool);
				if (maxLevelIndex >= 0)
				{
					ServerRequestPreviewRpc(targetLevelIndex: _currentPreviewLevelIndex = (byte)Mathf.Clamp(levelIndex, 0, maxLevelIndex), toolType: _selectedTool);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestUpgradeRpc(ToolType toolType, byte targetLevelIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2734964312u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in targetLevelIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2734964312u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int playerLevelIndex = GetPlayerLevelIndex(toolType);
			int maxLevelIndex = GetMaxLevelIndex(toolType);
			if (maxLevelIndex >= 0)
			{
				targetLevelIndex = (byte)Mathf.Clamp(targetLevelIndex, 0, maxLevelIndex);
				int MoneyCost;
				int ScrapCost;
				GameObject graphicPrefab;
				int maxIndex;
				if (targetLevelIndex <= playerLevelIndex)
				{
					UtilityMethods.Log($"[ScrapForge] Upgrade ignored. PlayerIndex={playerLevelIndex}, TargetIndex={targetLevelIndex}");
				}
				else if (!TryGetToolLevelData(toolType, targetLevelIndex, out MoneyCost, out ScrapCost, out graphicPrefab, out maxIndex))
				{
					UtilityMethods.Log($"[ScrapForge] ServerDoUpgrade: TryGetToolLevelData failed for Tool={toolType}, Index={targetLevelIndex}");
				}
				else
				{
					ClientReceiveUpgradeRpc(toolType, targetLevelIndex);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientReceiveUpgradeRpc(ToolType toolType, byte targetLevelIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3345275152u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in targetLevelIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3345275152u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (GetPlayerToolComponent(toolType) == null)
				{
					UtilityMethods.Log("[ScrapForge] ClientReceiveUpgradeRpc: Tool component not found on player");
					_lever.PlayFailTween();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPreviewRpc(ToolType toolType, byte targetLevelIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(353243386u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in targetLevelIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 353243386u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!(_currentPlayer == null))
			{
				int maxLevelIndex = GetMaxLevelIndex(toolType);
				if (maxLevelIndex >= 0)
				{
					byte b = (byte)Mathf.Clamp(targetLevelIndex, 0, maxLevelIndex);
					byte playerLevelIndex = GetPlayerLevelIndex(toolType);
					_currentPreviewLevelIndex = b;
					ClientPreviewRpc(toolType, b, playerLevelIndex);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPreviewRpc(ToolType toolType, byte previewLevelIndex, byte playerLevelIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3057897343u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in previewLevelIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in playerLevelIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3057897343u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PreviewUpdate(toolType, previewLevelIndex, playerLevelIndex);
			}
		}

		private void PreviewUpdate(ToolType toolType, byte previewLevelIndex, byte playerLevelIndex)
		{
			if (_currentToolGraphic != null)
			{
				Object.Destroy(_currentToolGraphic);
				_currentToolGraphic = null;
			}
			if (!TryGetToolLevelData(toolType, previewLevelIndex, out var MoneyCost, out var ScrapCost, out var graphicPrefab, out var maxIndex))
			{
				_goldText.text = string.Empty;
				_scrapText.text = string.Empty;
				return;
			}
			_selectedTool = toolType;
			_currentPreviewLevelIndex = previewLevelIndex;
			_currentUnlockCost = MoneyCost;
			_currentUpgradeCost = ScrapCost;
			if (graphicPrefab != null)
			{
				_currentToolGraphic = Object.Instantiate(graphicPrefab, _toolPoint.position, _toolPoint.rotation, _toolPoint);
			}
			if (playerLevelIndex >= previewLevelIndex)
			{
				_goldText.text = "OWNED";
				_scrapText.text = "-";
			}
			else
			{
				_goldText.text = MoneyCost.ToString();
				_scrapText.text = ScrapCost.ToString();
			}
			UpdateLevelButtonsVisuals(playerLevelIndex, maxIndex);
		}

		private BaseTool GetPlayerToolComponent(ToolType toolType)
		{
			if (_currentPlayer == null)
			{
				return null;
			}
			return toolType switch
			{
				ToolType.DivingEquipment => _currentPlayer.DivingEquipment, 
				ToolType.Harpoon => _currentPlayer.Harpoon, 
				ToolType.FishingRod => _currentPlayer.FishingRod, 
				ToolType.Mop => _currentPlayer.Mop, 
				_ => null, 
			};
		}

		private byte GetPlayerLevelIndex(ToolType toolType)
		{
			return 0;
		}

		private int GetMaxLevelIndex(ToolType toolType)
		{
			return toolType switch
			{
				ToolType.DivingEquipment => (_suitData != null && _suitData.Levels != null && _suitData.Levels.Count > 0) ? (_suitData.Levels.Count - 1) : (-1), 
				ToolType.Harpoon => (_harpoonData != null && _harpoonData.Levels != null && _harpoonData.Levels.Count > 0) ? (_harpoonData.Levels.Count - 1) : (-1), 
				ToolType.FishingRod => (_rodData != null && _rodData.Levels != null && _rodData.Levels.Count > 0) ? (_rodData.Levels.Count - 1) : (-1), 
				ToolType.Mop => (_mopData != null && _mopData.Levels != null && _mopData.Levels.Count > 0) ? (_mopData.Levels.Count - 1) : (-1), 
				_ => -1, 
			};
		}

		private bool TryGetToolLevelData(ToolType toolType, int levelIndex, out int MoneyCost, out int ScrapCost, out GameObject graphicPrefab, out int maxIndex)
		{
			MoneyCost = 0;
			ScrapCost = 0;
			graphicPrefab = null;
			maxIndex = GetMaxLevelIndex(toolType);
			if (maxIndex < 0)
			{
				return false;
			}
			if (levelIndex < 0 || levelIndex > maxIndex)
			{
				return false;
			}
			switch (toolType)
			{
			case ToolType.DivingEquipment:
			{
				List<DivingEquipmentLevelData> levels3 = _suitData.Levels;
				if (levels3 == null || levels3.Count == 0)
				{
					return false;
				}
				DivingEquipmentLevelData divingEquipmentLevelData = levels3[levelIndex];
				MoneyCost = divingEquipmentLevelData.MoneyCost;
				ScrapCost = divingEquipmentLevelData.ScrapCost;
				graphicPrefab = divingEquipmentLevelData.GraphicPrefab;
				return true;
			}
			case ToolType.Harpoon:
			{
				List<HarpoonLevelData> levels2 = _harpoonData.Levels;
				if (levels2 == null || levels2.Count == 0)
				{
					return false;
				}
				HarpoonLevelData harpoonLevelData = levels2[levelIndex];
				MoneyCost = harpoonLevelData.MoneyCost;
				ScrapCost = harpoonLevelData.ScrapCost;
				graphicPrefab = harpoonLevelData.GraphicPrefab;
				return true;
			}
			case ToolType.FishingRod:
			{
				List<FishingRodLevelData> levels4 = _rodData.Levels;
				if (levels4 == null || levels4.Count == 0)
				{
					return false;
				}
				FishingRodLevelData fishingRodLevelData = levels4[levelIndex];
				MoneyCost = fishingRodLevelData.MoneyCost;
				ScrapCost = fishingRodLevelData.ScrapCost;
				graphicPrefab = fishingRodLevelData.GraphicPrefab;
				return true;
			}
			case ToolType.Mop:
			{
				List<MopLevelData> levels = _mopData.Levels;
				if (levels == null || levels.Count == 0)
				{
					return false;
				}
				MopLevelData mopLevelData = levels[levelIndex];
				MoneyCost = mopLevelData.MoneyCost;
				graphicPrefab = mopLevelData.GraphicPrefab;
				return true;
			}
			default:
				return false;
			}
		}

		private void UpdateLevelButtonsVisuals(int playerLevelIndex, int maxIndex)
		{
			if (_levelButtons == null || _levelButtons.Count == 0)
			{
				return;
			}
			if (playerLevelIndex < 0)
			{
				playerLevelIndex = 0;
			}
			if (maxIndex < 0)
			{
				maxIndex = -1;
			}
			for (int i = 0; i < _levelButtons.Count; i++)
			{
				ScrapForgeButton scrapForgeButton = _levelButtons[i];
				if (!(scrapForgeButton == null))
				{
					int num = i;
					ForgeLevelButtonState state = ((maxIndex >= 0 && num <= maxIndex) ? ((num <= playerLevelIndex) ? ForgeLevelButtonState.Owned : ForgeLevelButtonState.Available) : ForgeLevelButtonState.Disabled);
					bool isSelected = num == playerLevelIndex;
					scrapForgeButton.SetState(state, isSelected);
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3608110651u, __rpc_handler_3608110651, "ServerSyncUserInfoRpc", RpcInvokePermission.Everyone);
			__registerRpc(4102414861u, __rpc_handler_4102414861, "ClientSyncUserInfoRpc", RpcInvokePermission.Everyone);
			__registerRpc(2734964312u, __rpc_handler_2734964312, "ServerRequestUpgradeRpc", RpcInvokePermission.Everyone);
			__registerRpc(3345275152u, __rpc_handler_3345275152, "ClientReceiveUpgradeRpc", RpcInvokePermission.Everyone);
			__registerRpc(353243386u, __rpc_handler_353243386, "ServerRequestPreviewRpc", RpcInvokePermission.Everyone);
			__registerRpc(3057897343u, __rpc_handler_3057897343, "ClientPreviewRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3608110651(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ServerSyncUserInfoRpc(value);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4102414861(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ClientSyncUserInfoRpc(value);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2734964312(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ServerRequestUpgradeRpc(value, value2);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3345275152(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ClientReceiveUpgradeRpc(value, value2);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_353243386(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ServerRequestPreviewRpc(value, value2);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3057897343(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ScrapForge)target).ClientPreviewRpc(value, value2, value3);
				((ScrapForge)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ScrapForge";
		}
	}
}
