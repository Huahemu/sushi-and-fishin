using SushiAndFish.Casino.Original;
using global::System;
using global::System.Collections.Generic;
using global::Dissonance.Networking;
using global::Dissonance.Networking.Server;
using global::JetBrains.Annotations;
using global::Mirror;

namespace Dissonance.Integrations.MirrorIgnorance
{
	public class MirrorIgnoranceServer : BaseServer<MirrorIgnoranceServer, MirrorIgnoranceClient, MirrorConn>
	{
		[NotNull]
		private readonly MirrorIgnoranceCommsNetwork _network;

		private readonly List<NetworkConnection> _addedConnections = new List<NetworkConnection>();

		public MirrorIgnoranceServer([NotNull] MirrorIgnoranceCommsNetwork network)
		{
			if (network == null)
			{
				throw new ArgumentNullException("network");
			}
			_network = network;
		}

		public override void Connect()
		{
			NetworkServer.ReplaceHandler<DissonanceNetworkMessage>(OnMessageReceived);
			base.Connect();
		}

		private void OnMessageReceived(NetworkConnection source, DissonanceNetworkMessage msg)
		{
			using (msg)
			{
				NetworkReceivedPacket(new MirrorConn(source), msg.Data);
			}
		}

		protected override void AddClient([NotNull] ClientInfo<MirrorConn> client)
		{
			base.AddClient(client);
			if (client.PlayerName != _network.PlayerName)
			{
				_addedConnections.Add(client.Connection.Connection);
			}
		}

		public override void Disconnect()
		{
			base.Disconnect();
			NetworkServer.ReplaceHandler<DissonanceNetworkMessage>(MirrorIgnoranceCommsNetwork.NullMessageReceivedHandler);
		}

		protected override void ReadMessages()
		{
		}

		public override ServerState Update()
		{
			for (int num = _addedConnections.Count - 1; num >= 0; num--)
			{
				if (!IsConnected(_addedConnections[num]))
				{
					ClientDisconnected(new MirrorConn(_addedConnections[num]));
					_addedConnections.RemoveAt(num);
				}
			}
			return base.Update();
		}

		private static bool IsConnected([NotNull] NetworkConnection conn)
		{
			NetworkConnectionToClient networkConnectionToClient = (NetworkConnectionToClient)conn;
			if (conn.isReady)
			{
				return NetworkServer.connections.ContainsKey(networkConnectionToClient.connectionId);
			}
			return false;
		}

		protected override void SendReliable(MirrorConn connection, ArraySegment<byte> packet)
		{
			if (!Send(packet, connection, 0))
			{
				FatalError("Failed to send reliable packet (unknown Mirror error)");
			}
		}

		protected override void SendUnreliable(MirrorConn connection, ArraySegment<byte> packet)
		{
			Send(packet, connection, 1);
		}

		private bool Send(ArraySegment<byte> packet, MirrorConn connection, byte channel)
		{
			if (_network.PreprocessPacketToClient(packet, connection))
			{
				return true;
			}
			if (!IsConnected(connection.Connection))
			{
				return true;
			}
			if (connection.Connection == null)
			{
				Log.Error("Cannot send to a null destination");
				return false;
			}
			connection.Connection.Send(new DissonanceNetworkMessage(packet), channel);
			return true;
		}
	}
}
