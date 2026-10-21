namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Dissonance;
using global::Dissonance.Integrations.FMOD_Recording;

namespace SettingsSystem
{
	public class InputSettingsApplier : ISettingsApplier
	{
		private bool _pushToTalkHooked;

		public void Apply(SettingItemBase entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.key))
			{
				return;
			}
			if (entry is RebindSettingItem rebindSettingItem)
			{
				if (!string.IsNullOrWhiteSpace(rebindSettingItem.overridePath))
				{
					InputReader.Instance?.ApplyBindingOverride(rebindSettingItem.actionName, rebindSettingItem.bindingIndex, rebindSettingItem.overridePath);
				}
				return;
			}
			string text = entry.key.Trim().ToLowerInvariant();
			if (text == "inputvolume" && entry is SliderSettingItem sliderSettingItem)
			{
				FMODMicrophoneInput.InputGain = sliderSettingItem.value;
			}
			else if (text == "proximityvoicechatmode" && entry is DropdownSettingItem dropdownSettingItem)
			{
				EnsurePushToTalkHook();
				InputEvents.SetProximityVoiceChatMode(dropdownSettingItem.CurrentOption);
				ApplyDissonanceVoiceChatMode(InputEvents.ProximityVoiceChatMode);
			}
		}

		public void ApplyAll(SettingsLayout layout)
		{
			if (layout == null)
			{
				return;
			}
			foreach (SettingsLayout.Tab tab in layout.tabs)
			{
				if (tab == null)
				{
					continue;
				}
				foreach (SettingItemBase entry in tab.entries)
				{
					Apply(entry);
				}
			}
		}

		private void EnsurePushToTalkHook()
		{
			if (!_pushToTalkHooked)
			{
				InputEvents.OnPushToTalkEvent = (Action<bool>)Delegate.Combine(InputEvents.OnPushToTalkEvent, new Action<bool>(OnPushToTalkChanged));
				_pushToTalkHooked = true;
			}
		}

		private static void OnPushToTalkChanged(bool isPressed)
		{
			if (InputEvents.ProximityVoiceChatMode == VoiceChatInputMode.PushToTalk)
			{
				SetTriggersMuted(!isPressed);
			}
		}

		private static void ApplyDissonanceVoiceChatMode(VoiceChatInputMode mode)
		{
			CommActivationMode mode2 = ((mode != VoiceChatInputMode.PushToTalk) ? CommActivationMode.VoiceActivation : CommActivationMode.PushToTalk);
			bool isMuted = mode == VoiceChatInputMode.PushToTalk && !InputEvents.IsPushToTalkPressed;
			GetDissonanceTriggers(out var voiceTrigger, out var proximityTrigger);
			ApplyTriggerModeAndMute(voiceTrigger, mode2, isMuted);
			ApplyTriggerModeAndMute(proximityTrigger, mode2, isMuted);
		}

		private static void SetTriggersMuted(bool isMuted)
		{
			GetDissonanceTriggers(out var voiceTrigger, out var proximityTrigger);
			SetTriggerMuted(voiceTrigger, isMuted);
			SetTriggerMuted(proximityTrigger, isMuted);
		}

		private static void GetDissonanceTriggers(out VoiceBroadcastTrigger voiceTrigger, out VoiceProximityBroadcastTrigger proximityTrigger)
		{
			DissonanceComms singleton = DissonanceComms.GetSingleton();
			if (singleton == null)
			{
				voiceTrigger = null;
				proximityTrigger = null;
			}
			else
			{
				singleton.TryGetComponent<VoiceBroadcastTrigger>(out voiceTrigger);
				singleton.TryGetComponent<VoiceProximityBroadcastTrigger>(out proximityTrigger);
			}
		}

		private static void ApplyTriggerModeAndMute(IVoiceBroadcastTrigger trigger, CommActivationMode mode, bool isMuted)
		{
			if (trigger != null)
			{
				trigger.Mode = mode;
				trigger.IsMuted = isMuted;
			}
		}

		private static void SetTriggerMuted(IVoiceBroadcastTrigger trigger, bool isMuted)
		{
			if (trigger != null)
			{
				trigger.IsMuted = isMuted;
			}
		}
	}
}

}
