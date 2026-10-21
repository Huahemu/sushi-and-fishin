using SushiAndFish.Casino.Original;
using global::System;
using global::Dissonance.Networking;
using global::JetBrains.Annotations;
using global::Mirror;

namespace Dissonance.Integrations.MirrorIgnorance
{
	public class MirrorIgnoranceClient : BaseClient<MirrorIgnoranceServer, MirrorIgnoranceClient, MirrorConn>
	{
		private readonly MirrorIgnoranceCommsNetwork _network;

		public MirrorIgnoranceClient([NotNull] MirrorIgnoranceCommsNetwork network)
			: base((ICommsNetworkState)network)
		{
			if (network == null)
			{
				throw new ArgumentNullException("network");
			}
			_network = network;
		}

		public override void Connect()
		{
			if (!_network.Mode.IsServerEnabled())
			{
				NetworkClient.ReplaceHandler<DissonanceNetworkMessage>(OnMessageReceived);
			}
			Connected();
		}

		public override void Disconnect()
		{
			if (!_network.Mode.IsServerEnabled())
			{
				NetworkClient.ReplaceHandler<DissonanceNetworkMessage>(MirrorIgnoranceCommsNetwork.NullMessageReceivedHandler);
			}
			base.Disconnect();
		}

		private void OnMessageReceived(DissonanceNetworkMessage msg)
		{
			using (msg)
			{
				NetworkReceivedPacket(msg.Data);
			}
		}

		protected override void ReadMessages()
		{
		}

		protected override void SendReliable(ArraySegment<byte> packet)
		{
			if (!Send(packet, 0))
			{
				FatalError("Failed to send reliable packet (unknown Mirror error)");
			}
		}

		protected override void SendUnreliable(ArraySegment<byte> packet)
		{
			Send(packet, 1);
		}

		private bool Send(ArraySegment<byte> packet, byte channel)
		{
			if (_network.PreprocessPacketToServer(packet))
			{
				return true;
			}
			NetworkClient.connection.Send(new DissonanceNetworkMessage(packet), channel);
			return true;
		}
	}
}
