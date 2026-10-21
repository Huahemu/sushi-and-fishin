using SushiAndFish.Casino.Original;
using global::System;
using global::System.Collections.Generic;
using global::Dissonance.Datastructures;
using global::Dissonance.Extensions;
using global::Dissonance.Networking;
using global::Mirror;
using global::UnityEngine;

namespace Dissonance.Integrations.MirrorIgnorance
{
	[HelpURL("https://placeholder-software.co.uk/dissonance/docs/Basics/Quick-Start-MirrorIgnorance/")]
	public class MirrorIgnoranceCommsNetwork : BaseCommsNetwork<MirrorIgnoranceServer, MirrorIgnoranceClient, MirrorConn, Unit, Unit>
	{
		internal const byte ReliableSequencedChannel = 0;

		internal const byte UnreliableChannel = 1;

		private readonly Dissonance.Datastructures.ConcurrentPool<byte[]> _loopbackBuffers = new Dissonance.Datastructures.ConcurrentPool<byte[]>(8, () => new byte[1024]);

		private readonly List<ArraySegment<byte>> _loopbackQueue = new List<ArraySegment<byte>>();

		protected override MirrorIgnoranceServer CreateServer(Unit details)
		{
			return new MirrorIgnoranceServer(this);
		}

		protected override MirrorIgnoranceClient CreateClient(Unit details)
		{
			return new MirrorIgnoranceClient(this);
		}

		protected override void Update()
		{
			if (base.IsInitialized)
			{
				if ((object)NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive && (NetworkServer.active || NetworkClient.active) && (!NetworkClient.active || (NetworkClient.connection != null && NetworkClient.connection.isReady)))
				{
					bool active = NetworkServer.active;
					bool active2 = NetworkClient.active;
					if (base.Mode.IsServerEnabled() != active || base.Mode.IsClientEnabled() != active2)
					{
						if (active & active2)
						{
							RunAsHost(Unit.None, Unit.None);
						}
						else if (active)
						{
							RunAsDedicatedServer(Unit.None);
						}
						else if (active2)
						{
							RunAsClient(Unit.None);
						}
					}
				}
				else if (base.Mode != NetworkMode.None)
				{
					Stop();
					_loopbackQueue.Clear();
				}
				for (int i = 0; i < _loopbackQueue.Count; i++)
				{
					base.Client?.NetworkReceivedPacket(_loopbackQueue[i]);
					_loopbackBuffers.Put(_loopbackQueue[i].Array);
				}
				_loopbackQueue.Clear();
			}
			base.Update();
		}

		protected override void Initialize()
		{
			NetworkServer.ReplaceHandler<DissonanceNetworkMessage>(NullMessageReceivedHandler);
			base.Initialize();
		}

		internal bool PreprocessPacketToClient(ArraySegment<byte> packet, MirrorConn destination)
		{
			if (base.Server == null)
			{
				throw Log.CreatePossibleBugException("server packet preprocessing running, but this peer is not a server", "8f9dc0a0-1b48-4a7f-9bb6-f767b2542ab1");
			}
			if (base.Client == null)
			{
				return false;
			}
			if (NetworkClient.connection != destination.Connection)
			{
				return false;
			}
			if (base.Client != null)
			{
				_loopbackQueue.Add(packet.CopyToSegment(_loopbackBuffers.Get()));
			}
			return true;
		}

		internal bool PreprocessPacketToServer(ArraySegment<byte> packet)
		{
			if (base.Client == null)
			{
				throw Log.CreatePossibleBugException("client packet processing running, but this peer is not a client", "dd75dce4-e85c-4bb3-96ec-3a3636cc4fbe");
			}
			if (base.Server == null)
			{
				return false;
			}
			base.Server.NetworkReceivedPacket(new MirrorConn(NetworkClient.connection), packet);
			return true;
		}

		internal static void NullMessageReceivedHandler(DissonanceNetworkMessage msg)
		{
			if (Logs.GetLogLevel(LogCategory.Network) <= LogLevel.Trace)
			{
				Debug.Log("Discarding Dissonance network message");
			}
			msg.Dispose();
		}
	}
}
