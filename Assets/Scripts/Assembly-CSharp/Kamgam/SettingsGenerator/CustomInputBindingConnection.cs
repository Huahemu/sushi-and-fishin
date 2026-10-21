using UnityEngine;
using UnityEngine.InputSystem;

namespace Kamgam.SettingsGenerator
{
	public class CustomInputBindingConnection : Connection<string>
	{
		public static bool LogErrorOnBindingFail = true;

		protected InputActionAsset _inputActionAsset;

		protected string _bindingId;

		public void SetBindingId(string id)
		{
			_bindingId = id;
		}

		public string GetBindingId()
		{
			return _bindingId;
		}

		public void SetInputActionAsset(InputActionAsset asset)
		{
			_inputActionAsset = asset;
		}

		public InputActionAsset GetInputActionAsset()
		{
			return _inputActionAsset;
		}

		public void ClearOverride()
		{
			if (!(_inputActionAsset == null))
			{
				_inputActionAsset.ClearOverride(_bindingId);
			}
		}

		public override string Get()
		{
			if (_inputActionAsset == null)
			{
				logNoInputAssetError();
				return null;
			}
			if (_inputActionAsset.FindBinding(_bindingId, out var binding))
			{
				return binding.effectivePath;
			}
			logNoBindingError();
			return null;
		}

		public override string GetDefault()
		{
			if (_inputActionAsset == null)
			{
				logNoInputAssetError();
				return null;
			}
			if (_inputActionAsset.FindBinding(_bindingId, out var binding))
			{
				return binding.path;
			}
			logNoBindingError();
			return null;
		}

		private static void logNoInputAssetError()
		{
			if (LogErrorOnBindingFail)
			{
				Debug.LogError("The InputActionAsset is NULL.");
			}
		}

		private void logNoBindingError()
		{
			if (LogErrorOnBindingFail)
			{
				Debug.LogError("No binding for ID '" + _bindingId + "' found.");
			}
		}

		public override void Set(string overridePath)
		{
			if (_inputActionAsset == null)
			{
				if (LogErrorOnBindingFail)
				{
					Debug.LogError("The InputActionAsset is NULL.");
				}
				return;
			}
			if (!_inputActionAsset.ApplyBindingOverrideWithResult(_bindingId, overridePath) && LogErrorOnBindingFail)
			{
				Debug.LogError("No binding for ID '" + _bindingId + "' found.");
			}
			NotifyListenersIfChanged(overridePath);
		}
	}
}
