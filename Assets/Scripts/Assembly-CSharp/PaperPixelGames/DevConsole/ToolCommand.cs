using System;
using System.Collections.Generic;
using System.Reflection;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Wheel;
using Unity.Netcode;

namespace PaperPixelGames.DevConsole
{
	public class ToolCommand : IDevCommand
	{
		private static readonly Dictionary<string, string> ToolNameMap = new Dictionary<string, string>
		{
			{ "diving", "DivingEquipment" },
			{ "dive", "DivingEquipment" },
			{ "oxygen", "DivingEquipment" },
			{ "divingequipment", "DivingEquipment" },
			{ "harpoon", "Harpoon" },
			{ "spear", "Harpoon" },
			{ "poseidon", "PoseidonTrident" },
			{ "trident", "PoseidonTrident" },
			{ "poseidontrident", "PoseidonTrident" },
			{ "rod", "FishingRod" },
			{ "fishingrod", "FishingRod" },
			{ "fishing", "FishingRod" },
			{ "mop", "Mop" },
			{ "compass", "Compass" },
			{ "scooter", "Scooter" },
			{ "seascooter", "Scooter" }
		};

		public string Name => "tool";

		public string Description => "tool status | tool off | tool <name> on | tool <name> off | tool <name> <level>\nExamples: tool harpoon on, tool harpoon off, tool harpoon 2, tool trident on";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length < 2)
			{
				feedback(Description);
				return;
			}
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
			{
				feedback("Only the server can execute 'tool' commands.");
				return;
			}
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				feedback("PlayersManager not found.");
				return;
			}
			PPG_Player localPlayer = instance.LocalPlayer;
			if (localPlayer == null)
			{
				feedback("LocalPlayer not found.");
				return;
			}
			string text = args[1].Trim().ToLowerInvariant();
			if (text == "off")
			{
				localPlayer.DisableActiveTool();
				feedback("Active tool disabled.");
				return;
			}
			if (text == "status")
			{
				feedback(GetToolStatusString(localPlayer));
				return;
			}
			if (args.Length < 3)
			{
				feedback(Description);
				return;
			}
			if (!TryResolveToolPrefix(args[1], out var prefix))
			{
				feedback("Unknown tool name: '" + args[1] + "'");
				return;
			}
			string text2 = args[2].Trim().ToLowerInvariant();
			if (byte.TryParse(text2, out var result))
			{
				string error;
				if (result > 4)
				{
					feedback("Invalid level. Use 0..4.");
				}
				else if (!TrySetToolLevel(localPlayer, prefix, result, out error))
				{
					feedback(error);
				}
				else
				{
					feedback($"{prefix} level set to {result}.");
				}
				return;
			}
			if (TryParseToggle(text2, out var value))
			{
				if (!TrySetToolUnlocked(localPlayer, prefix, value, out var error2))
				{
					feedback(error2);
				}
				else if (value && prefix == "PoseidonTrident")
				{
					Singleton<PPG_WheelManager>.Instance?.UnlockPoseidonTrident(status: true);
					localPlayer.TogglePoseidonTrident(forceActive: true);
					feedback("Poseidon Trident unlocked and equip attempted.");
				}
				else
				{
					feedback(value ? (prefix + " unlocked.") : (prefix + " locked."));
				}
				return;
			}
			switch (text2)
			{
			case "loss":
			{
				bool result4;
				string error6;
				if (args.Length < 4)
				{
					feedback(Description);
				}
				else if (!bool.TryParse(args[3], out result4))
				{
					feedback("Invalid loss value. Use true / false");
				}
				else if (prefix != "DivingEquipment")
				{
					feedback("loss is only supported for DivingEquipment.");
				}
				else if (!TrySetDivingPreventLoss(localPlayer, result4, out error6))
				{
					feedback(error6);
				}
				else
				{
					feedback($"DivingEquipment PreventOxygenLoss set to {result4}.");
				}
				break;
			}
			case "lock":
			{
				if (!TrySetToolUnlocked(localPlayer, prefix, unlocked: false, out var error5))
				{
					feedback(error5);
				}
				else
				{
					feedback(prefix + " locked.");
				}
				break;
			}
			case "unlock":
			{
				bool result3;
				string error4;
				if (args.Length < 4)
				{
					feedback(Description);
				}
				else if (!bool.TryParse(args[3], out result3))
				{
					feedback("Invalid unlock value. Use true / false");
				}
				else if (!TrySetToolUnlocked(localPlayer, prefix, result3, out error4))
				{
					feedback(error4);
				}
				else
				{
					feedback($"{prefix} unlock set to {result3}.");
				}
				break;
			}
			case "level":
			{
				byte result2;
				string error3;
				if (args.Length < 4)
				{
					feedback(Description);
				}
				else if (!byte.TryParse(args[3], out result2) || result2 > 4)
				{
					feedback("Invalid level.");
				}
				else if (!TrySetToolLevel(localPlayer, prefix, result2, out error3))
				{
					feedback(error3);
				}
				else
				{
					feedback($"{prefix} level set to {result2}.");
				}
				break;
			}
			default:
				feedback("Unknown operation: '" + text2 + "'\n" + Description);
				break;
			}
		}

		private bool TrySetDivingPreventLoss(PPG_Player player, bool prevent, out string error)
		{
			error = null;
			player.DivingEquipment.SetDebugPreventOxygenLoss(prevent);
			return true;
		}

		private bool TryResolveToolPrefix(string raw, out string prefix)
		{
			prefix = null;
			if (string.IsNullOrWhiteSpace(raw))
			{
				return false;
			}
			raw = raw.Trim().ToLowerInvariant();
			return ToolNameMap.TryGetValue(raw, out prefix);
		}

		private bool TryParseToggle(string raw, out bool value)
		{
			value = false;
			if (string.IsNullOrWhiteSpace(raw))
			{
				return false;
			}
			switch (raw.Trim().ToLowerInvariant())
			{
			case "1":
			case "on":
			case "true":
			case "unlock":
			case "unlocked":
			case "enable":
			case "enabled":
				value = true;
				return true;
			case "0":
			case "off":
			case "false":
			case "lock":
			case "locked":
			case "disable":
			case "disabled":
				value = false;
				return true;
			default:
				return false;
			}
		}

		private bool TrySetToolUnlocked(PPG_Player player, string prefix, bool unlocked, out string error)
		{
			error = null;
			if (!TryGetToolDataNetworkVariable(player, out var toolDataNV, out error))
			{
				return false;
			}
			if (!TryGetNetworkVariableValue(toolDataNV, out var value, out error))
			{
				return false;
			}
			string memberName = prefix + "Unlocked";
			if (!TrySetMember(value, memberName, unlocked, out error))
			{
				return false;
			}
			if (!TrySetNetworkVariableValue(toolDataNV, value, out error))
			{
				return false;
			}
			return true;
		}

		private bool TrySetToolLevel(PPG_Player player, string prefix, byte level, out string error)
		{
			error = null;
			if (!TryGetToolDataNetworkVariable(player, out var toolDataNV, out error))
			{
				return false;
			}
			if (!TryGetNetworkVariableValue(toolDataNV, out var value, out error))
			{
				return false;
			}
			string memberName = prefix + "Level";
			if (!TrySetMember(value, memberName, level, out error))
			{
				return false;
			}
			if (!TrySetNetworkVariableValue(toolDataNV, value, out error))
			{
				return false;
			}
			return true;
		}

		private bool TryGetToolDataNetworkVariable(PPG_Player player, out object toolDataNV, out string error)
		{
			toolDataNV = null;
			error = null;
			FieldInfo field = typeof(PPG_Player).GetField("_relay", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				error = "PPG_Player._relay field not found (reflection).";
				return false;
			}
			object value = field.GetValue(player);
			if (value == null)
			{
				error = "PPG_Player._relay is null.";
				return false;
			}
			MemberInfo memberInfo = (MemberInfo)(((object)value.GetType().GetProperty("ToolDataNV", BindingFlags.Instance | BindingFlags.Public)) ?? ((object)value.GetType().GetField("ToolDataNV", BindingFlags.Instance | BindingFlags.Public)));
			if (memberInfo == null)
			{
				error = "RpcRelay.ToolDataNV not found (public property/field).";
				return false;
			}
			object obj = ((memberInfo is PropertyInfo propertyInfo) ? propertyInfo.GetValue(value) : ((!(memberInfo is FieldInfo fieldInfo)) ? null : fieldInfo.GetValue(value)));
			toolDataNV = obj;
			if (toolDataNV == null)
			{
				error = "ToolDataNV is null.";
				return false;
			}
			return true;
		}

		private bool TryGetNetworkVariableValue(object nvObj, out object value, out string error)
		{
			value = null;
			error = null;
			PropertyInfo property = nvObj.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
			if (property == null)
			{
				error = "NetworkVariable.Value property not found.";
				return false;
			}
			value = property.GetValue(nvObj);
			if (value == null)
			{
				error = "ToolDataNV.Value returned null.";
				return false;
			}
			return true;
		}

		private bool TrySetNetworkVariableValue(object nvObj, object newValue, out string error)
		{
			error = null;
			PropertyInfo property = nvObj.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
			if (property == null || !property.CanWrite)
			{
				error = "NetworkVariable.Value setter not found / not writable.";
				return false;
			}
			property.SetValue(nvObj, newValue);
			return true;
		}

		private bool TrySetMember(object boxedStruct, string memberName, object newValue, out string error)
		{
			error = null;
			Type type = boxedStruct.GetType();
			PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
			if (property != null && property.CanWrite)
			{
				object value = ConvertIfNeeded(newValue, property.PropertyType, out error);
				if (error != null)
				{
					return false;
				}
				property.SetValue(boxedStruct, value);
				return true;
			}
			FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
			if (field != null)
			{
				object value2 = ConvertIfNeeded(newValue, field.FieldType, out error);
				if (error != null)
				{
					return false;
				}
				field.SetValue(boxedStruct, value2);
				return true;
			}
			error = "ToolDataNet member not found: '" + memberName + "'";
			return false;
		}

		private object ConvertIfNeeded(object value, Type targetType, out string error)
		{
			error = null;
			if (value == null)
			{
				return null;
			}
			Type type = value.GetType();
			if (targetType.IsAssignableFrom(type))
			{
				return value;
			}
			try
			{
				return Convert.ChangeType(value, targetType);
			}
			catch
			{
				error = "Cannot convert '" + type.Name + "' to '" + targetType.Name + "'.";
				return null;
			}
		}

		private string GetToolStatusString(PPG_Player player)
		{
			try
			{
				ToolDataNet toolsData = player.ToolsData;
				return $"ToolsData: {toolsData}";
			}
			catch
			{
				return "ToolsData: (unavailable)";
			}
		}
	}
}
