using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Transports.Facepunch
{
	public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
	{
		private readonly struct DisconnectContext : IDisposable
		{
			private readonly FacepunchTransport owner;

			private readonly uint generation;

			private readonly DisconnectEvents previousEvent;

			private readonly string previousMessage;

			public DisconnectContext(FacepunchTransport owner, DisconnectEvents reason, string message = null)
			{
				this.owner = owner;
				generation = owner.disconnectContextGeneration;
				previousEvent = owner.DisconnectEvent;
				previousMessage = owner.DisconnectEventMessage;
				owner.SetDisconnectEvent(reason, message);
			}

			public void Dispose()
			{
				if (!(owner == null) && generation == owner.disconnectContextGeneration && (owner.DisconnectEvent != DisconnectEvents.TransportShutdown || previousEvent == DisconnectEvents.TransportShutdown))
				{
					owner.SetDisconnectEvent(previousEvent, previousMessage);
				}
			}
		}

		private class Client
		{
			public SteamId steamId;

			public Connection connection;

			public ReliableSendQueue.Peer sendPeer;
		}

		private sealed class ClientCallbacks : IConnectionManager
		{
			private readonly FacepunchTransport owner;

			private readonly ReliableSendQueue.Peer peer;

			private bool Current
			{
				get
				{
					if (owner.clientSendPeer == peer)
					{
						return owner.sendQueue.IsCurrent(peer);
					}
					return false;
				}
			}

			public ClientCallbacks(FacepunchTransport owner, ReliableSendQueue.Peer peer)
			{
				this.owner = owner;
				this.peer = peer;
			}

			public void OnConnecting(ConnectionInfo info)
			{
				if (Current)
				{
					((IConnectionManager)owner).OnConnecting(info);
				}
			}

			public void OnConnected(ConnectionInfo info)
			{
				if (Current && peer.Failure == TransportSendFailure.None)
				{
					((IConnectionManager)owner).OnConnected(info);
				}
			}

			public void OnDisconnected(ConnectionInfo info)
			{
				if (Current)
				{
					((IConnectionManager)owner).OnDisconnected(info);
				}
			}

			public void OnMessage(IntPtr data, int size, long number, long time, int channel)
			{
				if (Current && peer.Failure == TransportSendFailure.None)
				{
					((IConnectionManager)owner).OnMessage(data, size, number, time, channel);
				}
			}
		}

		private sealed class SocketCallbacks : ISocketManager
		{
			private readonly FacepunchTransport owner;

			private bool Current
			{
				get
				{
					if (owner.socketCallbacks == this)
					{
						return owner.socketManager != null;
					}
					return false;
				}
			}

			public SocketCallbacks(FacepunchTransport owner)
			{
				this.owner = owner;
			}

			public void OnConnecting(Connection connection, ConnectionInfo info)
			{
				if (Current)
				{
					((ISocketManager)owner).OnConnecting(connection, info);
				}
			}

			public void OnConnected(Connection connection, ConnectionInfo info)
			{
				if (Current)
				{
					((ISocketManager)owner).OnConnected(connection, info);
				}
			}

			public void OnDisconnected(Connection connection, ConnectionInfo info)
			{
				if (Current)
				{
					((ISocketManager)owner).OnDisconnected(connection, info);
				}
			}

			public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long number, long time, int channel)
			{
				if (Current)
				{
					((ISocketManager)owner).OnMessage(connection, identity, data, size, number, time, channel);
				}
			}
		}

		private ConnectionManager connectionManager;

		private SocketManager socketManager;

		private Dictionary<ulong, Client> connectedClients;

		private readonly ReliableSendQueue sendQueue = new ReliableSendQueue();

		private ReliableSendQueue.Peer clientSendPeer;

		private ReliableSendQueue.SendPacket sendPacket;

		private SocketCallbacks socketCallbacks;

		private uint disconnectContextGeneration;

		private readonly Queue<ulong> recentlyClosedPeers = new Queue<ulong>();

		private const int RecentCloseLimit = 32;

		[Space]
		[Tooltip("The Steam App ID of your game. Technically you're not allowed to use 480, but Valve doesn't do anything about it so it's fine for testing purposes.")]
		[SerializeField]
		private uint steamAppId = 480u;

		[Tooltip("The Steam ID of the user targeted when joining as a client.")]
		[SerializeField]
		public ulong targetSteamId;

		[Header("Info")]
		[ReadOnly]
		[Tooltip("When in play mode, this will display your Steam ID.")]
		[SerializeField]
		private ulong userSteamId;

		private byte[] payloadCache = new byte[4096];

		private LogLevel LogLevel
		{
			get
			{
				if (!(NetworkManager.Singleton != null))
				{
					return LogLevel.Normal;
				}
				return NetworkManager.Singleton.LogLevel;
			}
		}

		public bool IsInitialized { get; set; }

		public override ulong ServerClientId => 0uL;

		private void Awake()
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) { enabled = false; return; }
			try
			{
				SteamClient.Init(steamAppId, asyncCallbacks: false);
			}
			catch (Exception arg)
			{
				if (LogLevel <= LogLevel.Error)
				{
					Debug.LogError(string.Format("[{0}] - Caught an exeption during initialization of Steam client: {1}", "FacepunchTransport", arg));
				}
			}
			finally
			{
				StartCoroutine(InitSteamworks());
			}
		}

		private void Update()
		{
			SteamClient.RunCallbacks();
			sendQueue.Drain(MultiplayerDiagnostics.Now, SendNative);
		}

		private void OnDestroy()
		{
			if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled) return;
			Shutdown();
			SteamClient.Shutdown();
		}

		public override void DisconnectLocalClient()
		{
			CloseClientConnection();
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Disconnecting local client.");
			}
		}

		public override void DisconnectRemoteClient(ulong clientId)
		{
			MultiplayerDiagnostics.Record("transport_close_requested", $"transportId={clientId}", ulong.MaxValue);
			if (connectedClients != null && connectedClients.TryGetValue(clientId, out var value))
			{
				connectedClients.Remove(clientId);
				sendQueue.Close(value.sendPeer);
				RememberClosedPeer(clientId);
				value.connection.Flush();
				value.connection.Close();
				if (LogLevel <= LogLevel.Developer)
				{
					Debug.Log(string.Format("[{0}] - Disconnecting remote client with ID {1}.", "FacepunchTransport", clientId));
				}
			}
			else if (recentlyClosedPeers.Contains(clientId))
			{
				MultiplayerDiagnostics.Record("transport_close_ignored", $"transportId={clientId} reason=AlreadyClosed", ulong.MaxValue);
			}
			else if (LogLevel <= LogLevel.Normal)
			{
				Debug.LogWarning(string.Format("[{0}] - Failed to disconnect remote client with ID {1}, client not connected.", "FacepunchTransport", clientId));
			}
		}

		public static void DisconnectClientWithReason(NetworkManager manager, ulong clientId, string reason = null)
		{
			if (manager == null)
			{
				throw new ArgumentNullException("manager");
			}
			FacepunchTransport facepunchTransport = manager.NetworkConfig.NetworkTransport as FacepunchTransport;
			using ((facepunchTransport != null) ? new DisconnectContext(facepunchTransport, DisconnectEvents.ClosedRemoteConnection) : default(DisconnectContext))
			{
				if (reason == null)
				{
					manager.DisconnectClient(clientId);
				}
				else
				{
					manager.DisconnectClient(clientId, reason);
				}
			}
		}

		public override ulong GetCurrentRtt(ulong clientId)
		{
			try
			{
				if (TryGetConnection(clientId, out var connection, out var _))
				{
					return (ulong)Math.Max(0, connection.QuickStatus().Ping);
				}
			}
			catch (Exception)
			{
			}
			return 0uL;
		}

		public bool TryGetRemoteSteamId(ulong transportClientId, out ulong steamId)
		{
			steamId = 0uL;
			if (connectedClients == null || !connectedClients.TryGetValue(transportClientId, out var value))
			{
				return false;
			}
			steamId = value.steamId.Value;
			return steamId != 0;
		}

		public override void Initialize(NetworkManager networkManager = null)
		{
			ResetDisconnectContext(DisconnectEvents.Disconnected);
			CloseClientConnection();
			CloseServerSocket();
			sendQueue.Clear();
			sendPacket = SendNative;
			connectedClients = new Dictionary<ulong, Client>();
			MultiplayerDiagnostics.Record("reliable_queue_policy", $"peerBytes={2097152} totalBytes={8388608} maxAgeSeconds={60.0} maxMessageBytes={524288} peerMessages=256 totalMessages=1024 drainCalls=32 drainBytes=524288 retryDelayMs=50", ulong.MaxValue);
		}

		private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
		{
			return delivery switch
			{
				NetworkDelivery.Reliable => SendType.Reliable, 
				NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable, 
				NetworkDelivery.ReliableSequenced => SendType.Reliable, 
				NetworkDelivery.Unreliable => SendType.Unreliable, 
				NetworkDelivery.UnreliableSequenced => SendType.Unreliable, 
				_ => SendType.Reliable, 
			};
		}

		public override void Shutdown()
		{
			MultiplayerDiagnostics.Flush("transport_shutdown");
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Shutting down.");
			}
			CloseClientConnection();
			CloseServerSocket();
			connectedClients?.Clear();
			sendQueue.Clear();
			ResetDisconnectContext(DisconnectEvents.TransportShutdown);
		}

		public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
		{
			SendType sendType = NetworkDeliveryToSendType(delivery);
			if (!TryGetConnection(clientId, out var _, out var peer))
			{
				MultiplayerDiagnostics.ObserveSend(Result.NoConnection, data.Count, sendType == SendType.Reliable, clientId);
				return;
			}
			bool flag = peer.PendingCount == 0;
			if ((sendQueue.Send(peer, data, sendType == SendType.Reliable, MultiplayerDiagnostics.Now, SendNative) == TransportSendOutcome.Queued) & flag)
			{
				MultiplayerDiagnostics.Record("reliable_queue_started", $"transportId={clientId} bytes={peer.PendingBytes}", ulong.MaxValue);
			}
		}

		public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
		{
			clientId = 0uL;
			receiveTime = Time.realtimeSinceStartup;
			payload = default;
			if (sendQueue.TryTakeFailure(out var peer))
			{
				clientId = peer.Id;
				try
				{
					Client value;
					if (clientId == ServerClientId)
					{
						CloseClientConnection();
					}
					else if (connectedClients != null && connectedClients.TryGetValue(clientId, out value))
					{
						connectedClients.Remove(clientId);
						sendQueue.Close(value.sendPeer);
						value.connection.Close(linger: false, 1001, "Reliable send failed");
					}
				}
				catch (Exception ex)
				{
					MultiplayerDiagnostics.Record("transport_failed_peer_close_error", $"transportId={clientId} error={ex.GetType().Name}", ulong.MaxValue, critical: true);
				}
				sendQueue.Close(peer);
				RememberClosedPeer(peer.Id);
				NotifyDisconnect(peer.Id, peer);
				clientId = 0uL;
				return NetworkEvent.Nothing;
			}
			connectionManager?.Receive();
			socketManager?.Receive();
			clientId = 0uL;
			receiveTime = Time.realtimeSinceStartup;
			payload = default;
			return NetworkEvent.Nothing;
		}

		public override bool StartClient()
		{
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Starting as client.");
			}
			if (!SteamClient.IsValid)
			{
				Debug.LogWarning("[FacepunchTransport] - Client start blocked because Steam is unavailable.");
				return false;
			}
			if (targetSteamId == 0L || targetSteamId == SteamClient.SteamId.Value)
			{
				Debug.LogWarning("[FacepunchTransport] - Client start blocked because the relay target is invalid. " + $"TargetSteamId={targetSteamId}, LocalSteamId={SteamClient.SteamId.Value}.");
				return false;
			}
			ResetDisconnectContext(DisconnectEvents.Disconnected);
			CloseClientConnection();
			try
			{
				clientSendPeer = sendQueue.Open(ServerClientId);
				connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
				connectionManager.Interface = new ClientCallbacks(this, clientSendPeer);
				return true;
			}
			catch (ArgumentException ex)
			{
				CloseClientConnection();
				Debug.LogWarning("[FacepunchTransport] - Steam relay connection could not be created. " + $"TargetSteamId={targetSteamId}, Error={ex.Message}");
				return false;
			}
		}

		public override bool StartServer()
		{
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Starting as server.");
			}
			ResetDisconnectContext(DisconnectEvents.Disconnected);
			CloseServerSocket();
			if (connectedClients == null)
			{
				connectedClients = new Dictionary<ulong, Client>();
			}
			socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
			socketCallbacks = new SocketCallbacks(this);
			socketManager.Interface = socketCallbacks;
			return true;
		}

		private void EnsurePayloadCapacity(int size)
		{
			if (payloadCache.Length < size)
			{
				payloadCache = new byte[Math.Max(payloadCache.Length * 2, size)];
			}
		}

		void IConnectionManager.OnConnecting(ConnectionInfo info)
		{
			RecordConnection("transport_connecting", ServerClientId, info);
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log(string.Format("[{0}] - Connecting with Steam user {1}.", "FacepunchTransport", info.Identity.SteamId));
			}
		}

		void IConnectionManager.OnConnected(ConnectionInfo info)
		{
			RecordConnection("transport_connected", ServerClientId, info);
			InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log(string.Format("[{0}] - Connected with Steam user {1}.", "FacepunchTransport", info.Identity.SteamId));
			}
		}

		void IConnectionManager.OnDisconnected(ConnectionInfo info)
		{
			ReliableSendQueue.Peer peer = clientSendPeer;
			if (sendQueue.IsCurrent(peer))
			{
				RecordConnection("transport_disconnected", ServerClientId, info);
				MultiplayerDiagnostics.Flush("transport_disconnected");
				sendQueue.Close(peer);
				clientSendPeer = null;
				NotifyDisconnect(ServerClientId, peer);
				if (LogLevel <= LogLevel.Developer)
				{
					Debug.Log(string.Format("[{0}] - Disconnected Steam user {1}.", "FacepunchTransport", info.Identity.SteamId));
				}
			}
		}

		unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			MultiplayerDiagnostics.ObserveReceive(size);
			EnsurePayloadCapacity(size);
			fixed (byte* destination = payloadCache)
			{
				UnsafeUtility.MemCpy(destination, (void*)data, size);
			}
			InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
		}

		void ISocketManager.OnConnecting(Connection connection, ConnectionInfo info)
		{
			MultiplayerDiagnostics.WatchStartup();
			RecordConnection("transport_connecting", connection.Id, info);
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log(string.Format("[{0}] - Accepting connection from Steam user {1}.", "FacepunchTransport", info.Identity.SteamId));
			}
			connection.Accept();
		}

		void ISocketManager.OnConnected(Connection connection, ConnectionInfo info)
		{
			RecordConnection("transport_connected", connection.Id, info);
			if (!connectedClients.ContainsKey(connection.Id))
			{
				connectedClients.Add(connection.Id, new Client
				{
					connection = connection,
					steamId = info.Identity.SteamId,
					sendPeer = sendQueue.Open(connection.Id)
				});
				InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);
				if (LogLevel <= LogLevel.Developer)
				{
					Debug.Log(string.Format("[{0}] - Connected with Steam user {1}.", "FacepunchTransport", info.Identity.SteamId));
				}
			}
			else if (LogLevel <= LogLevel.Normal)
			{
				Debug.LogWarning(string.Format("[{0}] - Failed to connect client with ID {1}, client already connected.", "FacepunchTransport", connection.Id));
			}
		}

		void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info)
		{
			RecordConnection("transport_disconnected", connection.Id, info);
			MultiplayerDiagnostics.Flush("remote_transport_disconnected");
			if (connectedClients != null && connectedClients.TryGetValue(connection.Id, out var value))
			{
				connectedClients.Remove(connection.Id);
				sendQueue.Close(value.sendPeer);
				RememberClosedPeer(connection.Id);
				NotifyDisconnect(connection.Id, value.sendPeer);
				if (LogLevel <= LogLevel.Developer)
				{
					Debug.Log(string.Format("[{0}] - Disconnected Steam user {1}", "FacepunchTransport", info.Identity.SteamId));
				}
			}
		}

		unsafe void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			if (connectedClients != null && connectedClients.TryGetValue(connection.Id, out var value) && value.sendPeer.Failure == TransportSendFailure.None)
			{
				MultiplayerDiagnostics.ObserveReceive(size);
				EnsurePayloadCapacity(size);
				fixed (byte* destination = payloadCache)
				{
					UnsafeUtility.MemCpy(destination, (void*)data, size);
				}
				InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
			}
		}

		private bool TryGetConnection(ulong id, out Connection connection, out ReliableSendQueue.Peer peer)
		{
			if (id == ServerClientId && connectionManager != null && sendQueue.IsCurrent(clientSendPeer))
			{
				connection = connectionManager.Connection;
				peer = clientSendPeer;
				return true;
			}
			if (connectedClients != null && connectedClients.TryGetValue(id, out var value) && sendQueue.IsCurrent(value.sendPeer))
			{
				connection = value.connection;
				peer = value.sendPeer;
				return true;
			}
			connection = default;
			peer = null;
			return false;
		}

		private Result SendNative(ReliableSendQueue.Peer peer, ArraySegment<byte> data, bool reliable)
		{
			Result result = Result.NoConnection;
			try
			{
				if (TryGetConnection(peer.Id, out var connection, out var peer2) && peer == peer2)
				{
					result = connection.SendMessage(data.Array, data.Offset, data.Count, reliable ? SendType.Reliable : SendType.Unreliable, 0);
				}
			}
			catch (Exception)
			{
				result = Result.Fail;
			}
			MultiplayerDiagnostics.ObserveSend(result, data.Count, reliable, peer.Id);
			return result;
		}

		private void NotifyDisconnect(ulong transportId, ReliableSendQueue.Peer peer)
		{
			DisconnectEvents reason = DisconnectEvents.ClosedByRemote;
			string message = null;
			if (peer.Failure != TransportSendFailure.None)
			{
				string text = $"transportId={peer.Id} failure={peer.Failure} result={peer.NativeResult} messageBytes={peer.FailureBytes}";
				MultiplayerDiagnostics.Record("transport_send_failed", text, ulong.MaxValue, critical: true);
				reason = ((peer.Failure == TransportSendFailure.QueueExpired) ? DisconnectEvents.ProtocolTimeout : DisconnectEvents.ProtocolError);
				message = "Transport send failed: " + text;
			}
			using (new DisconnectContext(this, reason, message))
			{
				InvokeOnTransportEvent(NetworkEvent.Disconnect, transportId, default, Time.realtimeSinceStartup);
			}
		}

		private void ResetDisconnectContext(DisconnectEvents reason)
		{
			disconnectContextGeneration++;
			recentlyClosedPeers.Clear();
			SetDisconnectEvent(reason);
		}

		private void RememberClosedPeer(ulong id)
		{
			if (!recentlyClosedPeers.Contains(id))
			{
				if (recentlyClosedPeers.Count == 32)
				{
					recentlyClosedPeers.Dequeue();
				}
				recentlyClosedPeers.Enqueue(id);
			}
		}

		private static void RecordConnection(string name, ulong transportId, ConnectionInfo info)
		{
			MultiplayerDiagnostics.Record(name, $"transportId={transportId} peerSteam={info.Identity.SteamId} state={info.State} endReason={info.EndReason} endReasonCode={(int)info.EndReason}", ulong.MaxValue, name == "transport_disconnected");
		}

		public void SampleDiagnostics()
		{
			MultiplayerDiagnostics.Record("reliable_queue_status", $"pendingBytes={sendQueue.PendingBytes} pendingMessages={sendQueue.PendingCount} enqueued={sendQueue.EnqueuedCount} retryAccepted={sendQueue.RetryAcceptedCount}", ulong.MaxValue);
			try
			{
				if (connectionManager != null)
				{
					RecordStatus(ServerClientId, connectionManager.Connection);
				}
				if (connectedClients == null)
				{
					return;
				}
				int num = 8;
				foreach (KeyValuePair<ulong, Client> connectedClient in connectedClients)
				{
					if (num-- != 0)
					{
						RecordStatus(connectedClient.Key, connectedClient.Value.connection);
						continue;
					}
					break;
				}
			}
			catch (Exception)
			{
				MultiplayerDiagnostics.Record("transport_status_unavailable", "", ulong.MaxValue);
			}
		}

		private void RecordStatus(ulong transportId, Connection connection)
		{
			ConnectionStatus connectionStatus = connection.QuickStatus();
			TryGetConnection(transportId, out var _, out var peer);
			MultiplayerDiagnostics.Record("transport_status", FormattableString.Invariant($"transportId={transportId} pingMs={connectionStatus.Ping} qualityLocal={connectionStatus.ConnectionQualityLocal:F3} qualityRemote={connectionStatus.ConnectionQualityRemote:F3} pendingReliableBytes={connectionStatus.PendingReliable} pendingUnreliableBytes={connectionStatus.PendingUnreliable} unackedReliableBytes={connectionStatus.SentUnackedReliable} inBytesPerSec={connectionStatus.InBytesPerSec:F0} outBytesPerSec={connectionStatus.OutBytesPerSec:F0} appPendingReliableBytes={peer?.PendingBytes ?? 0} appPendingReliableMessages={peer?.PendingCount ?? 0} appOldestReliableSeconds={peer?.OldestAge(MultiplayerDiagnostics.Now) ?? 0.0:F3}"), ulong.MaxValue);
		}

		private void CloseClientConnection()
		{
			sendQueue.Close(clientSendPeer);
			clientSendPeer = null;
			ConnectionManager connectionManager = this.connectionManager;
			this.connectionManager = null;
			if (connectionManager == null)
			{
				return;
			}
			try
			{
				connectionManager.Interface = null;
				connectionManager.Close();
			}
			catch (Exception ex)
			{
				if (LogLevel <= LogLevel.Error)
				{
					Debug.LogError("[FacepunchTransport] - Failed to close the client connection: " + ex.Message);
				}
			}
			finally
			{
				this.connectionManager = null;
			}
		}

		private void CloseServerSocket()
		{
			socketCallbacks = null;
			if (connectedClients != null)
			{
				foreach (Client value in connectedClients.Values)
				{
					sendQueue.Close(value.sendPeer);
				}
				connectedClients.Clear();
			}
			SocketManager socketManager = this.socketManager;
			this.socketManager = null;
			if (socketManager == null)
			{
				return;
			}
			try
			{
				socketManager.Interface = null;
				socketManager.Close();
			}
			catch (Exception ex)
			{
				if (LogLevel <= LogLevel.Error)
				{
					Debug.LogError("[FacepunchTransport] - Failed to close the server socket: " + ex.Message);
				}
			}
			finally
			{
				this.socketManager = null;
			}
		}

		private IEnumerator InitSteamworks()
		{
			yield return new WaitUntil(() => SteamClient.IsValid);
			SteamNetworkingUtils.InitRelayNetworkAccess();
			IsInitialized = true;
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Initialized access to Steam Relay Network.");
			}
			userSteamId = SteamClient.SteamId;
			if (LogLevel <= LogLevel.Developer)
			{
				Debug.Log("[FacepunchTransport] - Fetched user Steam ID.");
			}
		}
	}
}
