using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAnalyticsSDK.Setup
{
	public class Settings : ScriptableObject
	{
		public enum HelpTypes
		{
			None = 0,
			IncludeSystemSpecsHelp = 1,
			ProvideCustomUserID = 2
		}

		public enum MessageTypes
		{
			None = 0,
			Error = 1,
			Info = 2,
			Warning = 3
		}

		public struct HelpInfo
		{
			public string Message;

			public MessageTypes MsgType;

			public HelpTypes HelpType;
		}

		public enum InspectorStates
		{
			Account = 0,
			Basic = 1,
			Debugging = 2,
			Pref = 3
		}

		[HideInInspector]
		public static string VERSION = "7.10.4";

		[HideInInspector]
		public static bool CheckingForUpdates = false;

		public int TotalMessagesSubmitted;

		public int TotalMessagesFailed;

		public int DesignMessagesSubmitted;

		public int DesignMessagesFailed;

		public int QualityMessagesSubmitted;

		public int QualityMessagesFailed;

		public int ErrorMessagesSubmitted;

		public int ErrorMessagesFailed;

		public int BusinessMessagesSubmitted;

		public int BusinessMessagesFailed;

		public int UserMessagesSubmitted;

		public int UserMessagesFailed;

		public string CustomArea = string.Empty;

		[SerializeField]
		private List<string> gameKey = new List<string>();

		[SerializeField]
		private List<string> secretKey = new List<string>();

		[SerializeField]
		public List<string> Build = new List<string>();

		[SerializeField]
		public List<string> SelectedPlatformOrganization = new List<string>();

		[SerializeField]
		public List<string> SelectedPlatformStudio = new List<string>();

		[SerializeField]
		public List<string> SelectedPlatformGame = new List<string>();

		[SerializeField]
		public List<int> SelectedPlatformGameID = new List<int>();

		[SerializeField]
		public List<int> SelectedOrganization = new List<int>();

		[SerializeField]
		public List<int> SelectedStudio = new List<int>();

		[SerializeField]
		public List<int> SelectedGame = new List<int>();

		public string NewVersion = "";

		public string Changes = "";

		public bool SignUpOpen = true;

		public string StudioName = "";

		public string GameName = "";

		public string OrganizationName = "";

		public string OrganizationIdentifier = "";

		public string EmailGA = "";

		[NonSerialized]
		public string PasswordGA = "";

		[NonSerialized]
		public string TokenGA = "";

		[NonSerialized]
		public string ExpireTime = "";

		[NonSerialized]
		public string LoginStatus = "Not logged in.";

		[NonSerialized]
		public bool JustSignedUp;

		[NonSerialized]
		public bool HideSignupWarning;

		public bool IntroScreen = true;

		[NonSerialized]
		public List<Organization> Organizations;

		public bool InfoLogEditor = true;

		public bool InfoLogBuild = true;

		public bool VerboseLogBuild;

		public bool UseManualSessionHandling;

		public bool SendExampleGameDataToMyGame;

		public bool InternetConnectivity;

		public List<string> CustomDimensions01 = new List<string>();

		public List<string> CustomDimensions02 = new List<string>();

		public List<string> CustomDimensions03 = new List<string>();

		public List<string> ResourceItemTypes = new List<string>();

		public List<string> ResourceCurrencies = new List<string>();

		public RuntimePlatform LastCreatedGamePlatform;

		public List<RuntimePlatform> Platforms = new List<RuntimePlatform>();

		public InspectorStates CurrentInspectorState;

		public List<HelpTypes> ClosedHints = new List<HelpTypes>();

		public bool DisplayHints;

		public Vector2 DisplayHintsScrollState;

		public Texture2D Logo;

		public Texture2D UpdateIcon;

		public Texture2D InfoIcon;

		public Texture2D DeleteIcon;

		public Texture2D GameIcon;

		public Texture2D HomeIcon;

		public Texture2D InstrumentIcon;

		public Texture2D QuestionIcon;

		public Texture2D UserIcon;

		public Texture2D AmazonIcon;

		public Texture2D GooglePlayIcon;

		public Texture2D iosIcon;

		public Texture2D macIcon;

		public Texture2D windowsPhoneIcon;

		[NonSerialized]
		public GUIStyle SignupButton;

		public bool UsePlayerSettingsBuildNumber;

		public bool SubmitErrors = true;

		public bool NativeErrorReporting;

		public int MaxErrorCount = 10;

		public bool SubmitFpsAverage;

		public bool SubmitFpsCritical;

		public bool IncludeGooglePlay = true;

		public int FpsCriticalThreshold = 20;

		public int FpsCirticalSubmitInterval = 1;

		public List<bool> PlatformFoldOut = new List<bool>();

		public bool CustomDimensions01FoldOut;

		public bool CustomDimensions02FoldOut;

		public bool CustomDimensions03FoldOut;

		public bool ResourceItemTypesFoldOut;

		public bool ResourceCurrenciesFoldOut;

		public bool EnableMemoryHistogram;

		public bool EnableHealthEvent;

		public bool EnableFPSHistogram;

		public bool EnableSDKInitEvent;

		public bool EnableHardwareTracking;

		public bool EnableMemoryTracking;

		public static readonly RuntimePlatform[] AvailablePlatforms = new RuntimePlatform[8]
		{
			RuntimePlatform.Android,
			RuntimePlatform.IPhonePlayer,
			RuntimePlatform.LinuxPlayer,
			RuntimePlatform.OSXPlayer,
			RuntimePlatform.tvOS,
			RuntimePlatform.WebGLPlayer,
			RuntimePlatform.WindowsPlayer,
			RuntimePlatform.MetroPlayerARM
		};

		public void SetCustomUserID(string customID)
		{
			_ = customID != string.Empty;
		}

		public void RemovePlatformAtIndex(int index)
		{
			if (index >= 0 && index < Platforms.Count)
			{
				gameKey.RemoveAt(index);
				secretKey.RemoveAt(index);
				Build.RemoveAt(index);
				SelectedPlatformOrganization.RemoveAt(index);
				SelectedPlatformStudio.RemoveAt(index);
				SelectedPlatformGame.RemoveAt(index);
				SelectedPlatformGameID.RemoveAt(index);
				SelectedOrganization.RemoveAt(index);
				SelectedStudio.RemoveAt(index);
				SelectedGame.RemoveAt(index);
				PlatformFoldOut.RemoveAt(index);
				Platforms.RemoveAt(index);
			}
		}

		public void AddPlatform(RuntimePlatform platform)
		{
			gameKey.Add("");
			secretKey.Add("");
			Build.Add("0.1");
			SelectedPlatformOrganization.Add("");
			SelectedPlatformStudio.Add("");
			SelectedPlatformGame.Add("");
			SelectedPlatformGameID.Add(-1);
			SelectedOrganization.Add(0);
			SelectedStudio.Add(0);
			SelectedGame.Add(0);
			PlatformFoldOut.Add(item: true);
			Platforms.Add(platform);
		}

		public string[] GetAvailablePlatforms()
		{
			List<string> list = new List<string>();
			for (int i = 0; i < AvailablePlatforms.Length; i++)
			{
				RuntimePlatform runtimePlatform = AvailablePlatforms[i];
				switch (runtimePlatform)
				{
				case RuntimePlatform.IPhonePlayer:
					if (!Platforms.Contains(RuntimePlatform.tvOS) && !Platforms.Contains(runtimePlatform))
					{
						list.Add(runtimePlatform.ToString());
					}
					else if (!Platforms.Contains(runtimePlatform))
					{
						list.Add(runtimePlatform.ToString());
					}
					break;
				case RuntimePlatform.tvOS:
					if (!Platforms.Contains(RuntimePlatform.IPhonePlayer) && !Platforms.Contains(runtimePlatform))
					{
						list.Add(runtimePlatform.ToString());
					}
					else if (!Platforms.Contains(runtimePlatform))
					{
						list.Add(runtimePlatform.ToString());
					}
					break;
				case RuntimePlatform.MetroPlayerARM:
					if (!Platforms.Contains(runtimePlatform))
					{
						list.Add("WSA");
					}
					break;
				default:
					if (!Platforms.Contains(runtimePlatform))
					{
						list.Add(runtimePlatform.ToString());
					}
					break;
				}
			}
			return list.ToArray();
		}

		public bool IsGameKeyValid(int index, string value)
		{
			bool result = true;
			for (int i = 0; i < Platforms.Count; i++)
			{
				if (index != i && value.Equals(gameKey[i]))
				{
					result = false;
					break;
				}
			}
			return result;
		}

		public bool IsSecretKeyValid(int index, string value)
		{
			bool result = true;
			for (int i = 0; i < Platforms.Count; i++)
			{
				if (index != i && value.Equals(secretKey[i]))
				{
					result = false;
					break;
				}
			}
			return result;
		}

		public static void UpdateKeys(int index, string gameKey, string secretKey)
		{
			GameAnalytics.SettingsGA.gameKey[index] = gameKey;
			GameAnalytics.SettingsGA.secretKey[index] = secretKey;
		}

		public void UpdateGameKey(int index, string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (IsGameKeyValid(index, value))
				{
					gameKey[index] = value;
				}
				else if (gameKey[index].Equals(value))
				{
					gameKey[index] = "";
				}
			}
			else
			{
				gameKey[index] = value;
			}
		}

		public void UpdateSecretKey(int index, string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (IsSecretKeyValid(index, value))
				{
					secretKey[index] = value;
				}
				else if (secretKey[index].Equals(value))
				{
					secretKey[index] = "";
				}
			}
			else
			{
				secretKey[index] = value;
			}
		}

		public string GetGameKey(int index)
		{
			return gameKey[index];
		}

		public string GetSecretKey(int index)
		{
			return secretKey[index];
		}

		public void SetCustomArea(string customArea)
		{
		}

		public void SetKeys(string gamekey, string secretkey)
		{
		}
	}
}
