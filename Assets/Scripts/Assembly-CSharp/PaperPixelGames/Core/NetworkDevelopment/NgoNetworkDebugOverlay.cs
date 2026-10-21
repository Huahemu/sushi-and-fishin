using System;
using System.Collections.Generic;
using System.Text;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class NgoNetworkDebugOverlay : MonoBehaviour
	{
		private struct PendingDebugPing
		{
			public uint Sequence;

			public float SentTime;
		}

		private const KeyCode ToggleKey = KeyCode.F11;

		private const float FpsSmoothFactor = 0.1f;

		private const float DebugPingInterval = 1f;

		private const float DebugPingStaleSeconds = 5f;

		private const string DebugPingMessageName = "NgoNetworkDebugPing";

		private const string DebugPongMessageName = "NgoNetworkDebugPong";

		private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.62f);

		private static readonly Color TextColor = Color.white;

		private static NgoNetworkDebugOverlay _instance;

		private readonly StringBuilder _builder = new StringBuilder(1024);

		private readonly Dictionary<ulong, PendingDebugPing> _pendingDebugPings = new Dictionary<ulong, PendingDebugPing>();

		private readonly Dictionary<ulong, float> _debugRttMsByClientId = new Dictionary<ulong, float>();

		private readonly HashSet<ulong> _loggedDebugPingSendFailures = new HashSet<ulong>();

		private readonly List<ulong> _staleClientIds = new List<ulong>();

		private GUIStyle _labelStyle;

		private GUIStyle _titleStyle;

		private Texture2D _panelTexture;

		private NetworkManager _registeredNetworkManager;

		private bool _visible;

		private bool _messagesRegistered;

		private float _nextDebugPingTime;

		private float _smoothedFps;

		private uint _nextDebugPingSequence;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else
			{
				_instance = this;
			}
		}

		private void Update()
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (unscaledDeltaTime > 0f)
			{
				float num = 1f / unscaledDeltaTime;
				_smoothedFps = ((_smoothedFps <= 0f) ? num : Mathf.Lerp(_smoothedFps, num, 0.1f));
			}
			if (Input.GetKeyDown(KeyCode.F11))
			{
				_visible = !_visible;
			}
			NetworkManager singleton = NetworkManager.Singleton;
			MaintainDebugMessaging(singleton);
			TickDebugPing(singleton);
		}

		private void OnDestroy()
		{
			UnregisterDebugMessaging();
			if (_instance == this)
			{
				_instance = null;
			}
		}

		private void OnGUI()
		{
			if (_visible)
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (singleton == null || !singleton.IsListening)
				{
					DrawPanel("NGO Network Debug\nNot listening");
				}
				else
				{
					DrawPanel(BuildDebugText(singleton));
				}
			}
		}

		private string BuildDebugText(NetworkManager networkManager)
		{
			_builder.Clear();
			_builder.AppendLine("NGO Network Debug");
			_builder.Append("Role: ").Append(GetRole(networkManager)).AppendLine();
			_builder.Append("TickRate: ").Append(networkManager.NetworkConfig.TickRate).AppendLine();
			_builder.Append("LocalTick: ").Append(networkManager.LocalTime.Tick).AppendLine();
			_builder.Append("ServerTick: ").Append(networkManager.ServerTime.Tick).AppendLine();
			_builder.Append("FPS: ").Append(_smoothedFps.ToString("0")).AppendLine();
			_builder.Append("ConnectedClients: ").Append(GetConnectedClientCount(networkManager)).AppendLine();
			if (networkManager.NetworkTimeSystem != null)
			{
				_builder.Append("TickLatency: ").Append(networkManager.NetworkTimeSystem.TickLatency).AppendLine();
				_builder.Append("LocalBufferSec: ").Append(networkManager.NetworkTimeSystem.LocalBufferSec.ToString("0.000")).AppendLine();
				_builder.Append("ServerBufferSec: ").Append(networkManager.NetworkTimeSystem.ServerBufferSec.ToString("0.000")).AppendLine();
			}
			AppendDebugRttLines(networkManager);
			return _builder.ToString();
		}

		private static string GetRole(NetworkManager networkManager)
		{
			if (networkManager.IsHost)
			{
				return "Host";
			}
			if (networkManager.IsServer)
			{
				return "Server";
			}
			if (networkManager.IsClient)
			{
				return "Client";
			}
			return "Unknown";
		}

		private static int GetConnectedClientCount(NetworkManager networkManager)
		{
			if (!networkManager.IsServer)
			{
				if (!networkManager.IsClient)
				{
					return 0;
				}
				return 1;
			}
			return networkManager.ConnectedClientsIds.Count;
		}

		private void AppendDebugRttLines(NetworkManager networkManager)
		{
			if (networkManager.IsServer)
			{
				foreach (ulong connectedClientsId in networkManager.ConnectedClientsIds)
				{
					if (connectedClientsId != networkManager.LocalClientId)
					{
						_builder.Append("DebugRTT client ").Append(connectedClientsId).Append(": ")
							.Append(GetDebugRttText(connectedClientsId))
							.AppendLine();
					}
				}
				return;
			}
			if (networkManager.IsClient)
			{
				_builder.Append("DebugRTT server: ").Append(GetDebugRttText(0uL)).AppendLine();
			}
		}

		private string GetDebugRttText(ulong clientId)
		{
			if (!_debugRttMsByClientId.TryGetValue(clientId, out var value))
			{
				return "N/A";
			}
			return $"{value:0.0} ms";
		}

		private void MaintainDebugMessaging(NetworkManager networkManager)
		{
			if (networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
			{
				UnregisterDebugMessaging();
			}
			else if (!(_registeredNetworkManager == networkManager) || !_messagesRegistered)
			{
				UnregisterDebugMessaging();
				_registeredNetworkManager = networkManager;
				networkManager.CustomMessagingManager.RegisterNamedMessageHandler("NgoNetworkDebugPing", OnDebugPingMessage);
				networkManager.CustomMessagingManager.RegisterNamedMessageHandler("NgoNetworkDebugPong", OnDebugPongMessage);
				_messagesRegistered = true;
				_nextDebugPingTime = 0f;
			}
		}

		private void UnregisterDebugMessaging()
		{
			if (_registeredNetworkManager != null && _messagesRegistered && _registeredNetworkManager.CustomMessagingManager != null)
			{
				_registeredNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler("NgoNetworkDebugPing");
				_registeredNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler("NgoNetworkDebugPong");
			}
			_registeredNetworkManager = null;
			_messagesRegistered = false;
			_pendingDebugPings.Clear();
			_debugRttMsByClientId.Clear();
			_loggedDebugPingSendFailures.Clear();
		}

		private void TickDebugPing(NetworkManager networkManager)
		{
			if (!_visible || !_messagesRegistered || networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			PruneDisconnectedDebugPeers(networkManager, realtimeSinceStartup);
			if (realtimeSinceStartup < _nextDebugPingTime)
			{
				return;
			}
			_nextDebugPingTime = realtimeSinceStartup + 1f;
			if (networkManager.IsServer)
			{
				foreach (ulong connectedClientsId in networkManager.ConnectedClientsIds)
				{
					if (connectedClientsId != networkManager.LocalClientId)
					{
						SendDebugPing(connectedClientsId, realtimeSinceStartup);
					}
				}
				return;
			}
			if (networkManager.IsClient)
			{
				SendDebugPing(0uL, realtimeSinceStartup);
			}
		}

		private void SendDebugPing(ulong targetClientId, float sentTime)
		{
			NetworkManager registeredNetworkManager = _registeredNetworkManager;
			if (registeredNetworkManager == null || registeredNetworkManager.CustomMessagingManager == null)
			{
				return;
			}
			uint value = ++_nextDebugPingSequence;
			using FastBufferWriter messageStream = new FastBufferWriter(32, Allocator.Temp);
			messageStream.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			messageStream.WriteValueSafe(in sentTime, default(FastBufferWriter.ForPrimitives));
			try
			{
				registeredNetworkManager.CustomMessagingManager.SendNamedMessage("NgoNetworkDebugPing", targetClientId, messageStream);
				_pendingDebugPings[targetClientId] = new PendingDebugPing
				{
					Sequence = value,
					SentTime = sentTime
				};
				_loggedDebugPingSendFailures.Remove(targetClientId);
			}
			catch (Exception ex)
			{
				if (_loggedDebugPingSendFailures.Add(targetClientId))
				{
					UtilityMethods.Log($"[NgoNetworkDebug] Failed to send debug ping. TargetClientId:{targetClientId} Exception:{ex.Message}", LogType.Warning);
				}
			}
		}

		private void OnDebugPingMessage(ulong senderClientId, FastBufferReader reader)
		{
			if (!_messagesRegistered || _registeredNetworkManager == null || _registeredNetworkManager.CustomMessagingManager == null)
			{
				return;
			}
			try
			{
				reader.ReadValueSafe(out uint value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				using FastBufferWriter messageStream = new FastBufferWriter(32, Allocator.Temp);
				messageStream.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				messageStream.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				_registeredNetworkManager.CustomMessagingManager.SendNamedMessage("NgoNetworkDebugPong", senderClientId, messageStream);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log($"[NgoNetworkDebug] Failed to handle debug ping. SenderClientId:{senderClientId} Exception:{ex.Message}", LogType.Warning);
			}
		}

		private void OnDebugPongMessage(ulong senderClientId, FastBufferReader reader)
		{
			try
			{
				reader.ReadValueSafe(out uint value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				if (_pendingDebugPings.TryGetValue(senderClientId, out var value3) && value3.Sequence == value)
				{
					_pendingDebugPings.Remove(senderClientId);
					_debugRttMsByClientId[senderClientId] = Mathf.Max(0f, (Time.realtimeSinceStartup - value2) * 1000f);
				}
			}
			catch (Exception ex)
			{
				UtilityMethods.Log($"[NgoNetworkDebug] Failed to handle debug pong. SenderClientId:{senderClientId} Exception:{ex.Message}", LogType.Warning);
			}
		}

		private void PruneDisconnectedDebugPeers(NetworkManager networkManager, float now)
		{
			_staleClientIds.Clear();
			foreach (KeyValuePair<ulong, float> item in _debugRttMsByClientId)
			{
				if (!IsDebugPeerValid(networkManager, item.Key))
				{
					_staleClientIds.Add(item.Key);
				}
			}
			foreach (ulong staleClientId in _staleClientIds)
			{
				_debugRttMsByClientId.Remove(staleClientId);
			}
			_staleClientIds.Clear();
			foreach (KeyValuePair<ulong, PendingDebugPing> pendingDebugPing in _pendingDebugPings)
			{
				if (!IsDebugPeerValid(networkManager, pendingDebugPing.Key) || now - pendingDebugPing.Value.SentTime > 5f)
				{
					_staleClientIds.Add(pendingDebugPing.Key);
				}
			}
			foreach (ulong staleClientId2 in _staleClientIds)
			{
				_pendingDebugPings.Remove(staleClientId2);
			}
		}

		private static bool IsDebugPeerValid(NetworkManager networkManager, ulong clientId)
		{
			if (networkManager.IsServer)
			{
				foreach (ulong connectedClientsId in networkManager.ConnectedClientsIds)
				{
					if (connectedClientsId == clientId)
					{
						return true;
					}
				}
				return false;
			}
			if (networkManager.IsClient)
			{
				return clientId == 0;
			}
			return false;
		}

		private void DrawPanel(string text)
		{
			EnsureStyles();
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "NGO Network Debug\nNo debug text";
			}
			string[] array = text.Split('\n');
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				if (!string.IsNullOrEmpty(array[i]))
				{
					num++;
				}
			}
			float height = 20f + (float)Mathf.Max(1, num) * 20f + 4f;
			Color color = GUI.color;
			Color contentColor = GUI.contentColor;
			bool flag = GUI.enabled;
			GUI.enabled = true;
			GUI.color = PanelColor;
			GUI.DrawTexture(new Rect(20f, 20f, 420f, height), _panelTexture, ScaleMode.StretchToFill, alphaBlend: true);
			GUI.color = TextColor;
			GUI.contentColor = TextColor;
			float num2 = 30f;
			for (int j = 0; j < array.Length; j++)
			{
				if (!string.IsNullOrEmpty(array[j]))
				{
					GUIStyle style = ((j == 0) ? _titleStyle : _labelStyle);
					GUI.Label(new Rect(32f, num2, 396f, 20f), array[j], style);
					num2 += 20f;
				}
			}
			GUI.color = color;
			GUI.contentColor = contentColor;
			GUI.enabled = flag;
		}

		private void EnsureStyles()
		{
			if (_panelTexture == null)
			{
				_panelTexture = new Texture2D(1, 1)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				_panelTexture.SetPixel(0, 0, Color.white);
				_panelTexture.Apply();
			}
			if (_labelStyle == null)
			{
				_labelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 13,
					alignment = TextAnchor.MiddleLeft,
					clipping = TextClipping.Clip,
					richText = false,
					normal = 
					{
						textColor = TextColor
					},
					hover = 
					{
						textColor = TextColor
					},
					active = 
					{
						textColor = TextColor
					},
					focused = 
					{
						textColor = TextColor
					},
					onNormal = 
					{
						textColor = TextColor
					},
					onHover = 
					{
						textColor = TextColor
					},
					onActive = 
					{
						textColor = TextColor
					},
					onFocused = 
					{
						textColor = TextColor
					}
				};
			}
			if (_titleStyle == null)
			{
				_titleStyle = new GUIStyle(_labelStyle)
				{
					fontSize = 15,
					fontStyle = FontStyle.Bold,
					normal = 
					{
						textColor = TextColor
					},
					hover = 
					{
						textColor = TextColor
					},
					active = 
					{
						textColor = TextColor
					},
					focused = 
					{
						textColor = TextColor
					},
					onNormal = 
					{
						textColor = TextColor
					},
					onHover = 
					{
						textColor = TextColor
					},
					onActive = 
					{
						textColor = TextColor
					},
					onFocused = 
					{
						textColor = TextColor
					}
				};
			}
		}
	}
}
