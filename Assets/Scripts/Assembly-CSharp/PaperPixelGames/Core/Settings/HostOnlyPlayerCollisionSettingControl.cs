using Kamgam.SettingsGenerator;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Settings
{
	[RequireComponent(typeof(Toggle))]
	public sealed class HostOnlyPlayerCollisionSettingControl : MonoBehaviour
	{
		private Toggle _toggle;

		private NetworkManager _networkManager;

		private void Awake()
		{
			_toggle = GetComponent<Toggle>();
		}

		private void OnEnable()
		{
			RuntimeSettings.PlayerCollisionChanged += OnPlayerCollisionSettingChanged;
			BindNetworkEvents();
			RefreshInteractableState();
			PlayerCollisionConnection.PullSettingFromAuthoritativeValue();
			RefreshToggleValue(RuntimeSettings.PlayerCollisionEnabled);
		}

		private void OnDisable()
		{
			RuntimeSettings.PlayerCollisionChanged -= OnPlayerCollisionSettingChanged;
			UnbindNetworkEvents();
		}

		private void LateUpdate()
		{
			BindNetworkEvents();
			RefreshInteractableState();
			RefreshToggleValue(RuntimeSettings.PlayerCollisionEnabled);
		}

		private void BindNetworkEvents()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(_networkManager == singleton))
			{
				UnbindNetworkEvents();
				_networkManager = singleton;
				if (!(_networkManager == null))
				{
					_networkManager.OnServerStarted += OnNetworkStateChanged;
					_networkManager.OnClientConnectedCallback += OnNetworkClientStateChanged;
					_networkManager.OnClientDisconnectCallback += OnNetworkClientStateChanged;
				}
			}
		}

		private void UnbindNetworkEvents()
		{
			if (!(_networkManager == null))
			{
				_networkManager.OnServerStarted -= OnNetworkStateChanged;
				_networkManager.OnClientConnectedCallback -= OnNetworkClientStateChanged;
				_networkManager.OnClientDisconnectCallback -= OnNetworkClientStateChanged;
				_networkManager = null;
			}
		}

		private void OnNetworkStateChanged()
		{
			RefreshInteractableState();
		}

		private void OnNetworkClientStateChanged(ulong _)
		{
			RefreshInteractableState();
		}

		private void OnPlayerCollisionSettingChanged(bool enabled)
		{
			PlayerCollisionConnection.PullSettingFromAuthoritativeValue();
			RefreshToggleValue(enabled);
		}

		private void RefreshToggleValue(bool enabled)
		{
			if (_toggle != null && _toggle.isOn != enabled)
			{
				_toggle.SetIsOnWithoutNotify(enabled);
			}
		}

		private void RefreshInteractableState()
		{
			if (!(_toggle == null))
			{
				NetworkManager singleton = NetworkManager.Singleton;
				bool flag = !(singleton != null) || !singleton.IsClient || singleton.IsHost;
				if (_toggle.interactable != flag)
				{
					_toggle.interactable = flag;
				}
			}
		}
	}
}
