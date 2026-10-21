using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using Dissonance.Extensions;
using Dissonance.Networking;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Netcode;

namespace Dissonance.Integrations.Unity_NFGO
{
	public class NfgoCommsNetwork : BaseCommsNetwork<NfgoServer, NfgoClient, NfgoConn, Unit, Unit>
	{
		private readonly ConcurrentPool<byte[]> _loopbackBuffers = new ConcurrentPool<byte[]>(8, () => new byte[1024]);

		private readonly List<ArraySegment<byte>> _loopbackQueueToServer = new List<ArraySegment<byte>>();

		private readonly List<ArraySegment<byte>> _loopbackQueueToClient = new List<ArraySegment<byte>>();

		protected override NfgoClient CreateClient(Unit connectionParameters)
		{
			return new NfgoClient(this);
		}

		protected override NfgoServer CreateServer(Unit connectionParameters)
		{
			return new NfgoServer(this);
		}

		protected override void Update()
		{
			NetworkManager singleton;
			int num;
			int num2;
			if (base.IsInitialized)
			{
				singleton = NetworkManager.Singleton;
				if ((object)singleton != null)
				{
					num = (((bool)singleton) ? 1 : 0);
					if (num != 0 && singleton.IsClient)
					{
						num2 = (singleton.IsConnectedClient ? 1 : 0);
						goto IL_0031;
					}
				}
				else
				{
					num = 0;
				}
				num2 = 0;
				goto IL_0031;
			}
			goto IL_01c2;
			IL_0031:
			bool flag = (byte)num2 != 0;
			bool flag2 = num != 0 && singleton.IsServer;
			if (num != 0 && singleton.isActiveAndEnabled && (flag | flag2))
			{
				bool isServer = singleton.IsServer;
				bool isClient = singleton.IsClient;
				if (base.Mode.IsServerEnabled() != isServer || base.Mode.IsClientEnabled() != isClient)
				{
					if (isServer & isClient)
					{
						RunAsHost(Unit.None, Unit.None);
					}
					else if (isServer)
					{
						RunAsDedicatedServer(Unit.None);
					}
					else if (isClient)
					{
						RunAsClient(Unit.None);
					}
				}
			}
			else if (base.Mode != NetworkMode.None)
			{
				Stop();
				_loopbackQueueToClient.Clear();
				_loopbackQueueToServer.Clear();
			}
			if (base.Client != null)
			{
				foreach (ArraySegment<byte> item in _loopbackQueueToClient)
				{
					if (item.Array != null)
					{
						base.Client.NetworkReceivedPacket(item);
						_loopbackBuffers.Put(item.Array);
					}
				}
			}
			_loopbackQueueToClient.Clear();
			if (base.Server != null)
			{
				foreach (ArraySegment<byte> item2 in _loopbackQueueToServer)
				{
					if (item2.Array != null)
					{
						base.Server.NetworkReceivedPacket(new NfgoConn(null), item2);
						_loopbackBuffers.Put(item2.Array);
					}
				}
			}
			_loopbackQueueToServer.Clear();
			goto IL_01c2;
			IL_01c2:
			base.Update();
		}

		internal void SendToServer(ArraySegment<byte> packet, bool reliable, [NotNull] NetworkManager netManager)
		{
			if (packet.Array == null)
			{
				throw new ArgumentException("packet is null");
			}
			if (base.Server != null)
			{
				_loopbackQueueToServer.Add(packet.CopyToSegment(_loopbackBuffers.Get()));
				return;
			}
			using FastBufferWriter messageStream = WritePacket(packet);
			netManager.CustomMessagingManager.SendNamedMessage("DissonanceToServer", 0uL, messageStream, reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.Unreliable);
		}

		internal void SendToClient(ArraySegment<byte> packet, NfgoConn client, bool reliable, [NotNull] NetworkManager netManager)
		{
			if (packet.Array == null)
			{
				throw new ArgumentException("packet is null");
			}
			if (!client.ClientId.HasValue)
			{
				_loopbackQueueToClient.Add(packet.CopyToSegment(_loopbackBuffers.Get()));
			}
			else if (reliable || netManager.ConnectedClients.ContainsKey(client.ClientId.Value))
			{
				using (FastBufferWriter messageStream = WritePacket(packet))
				{
					netManager.CustomMessagingManager.SendNamedMessage("DissonanceToClient", client.ClientId.Value, messageStream, reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.Unreliable);
				}
			}
		}

		private static FastBufferWriter WritePacket(ArraySegment<byte> packet)
		{
			FastBufferWriter result = new FastBufferWriter(packet.Count + 4, Allocator.Temp);
			result.WriteValueSafe<uint>((uint)packet.Count, default(FastBufferWriter.ForPrimitives));
			result.WriteBytesSafe(packet.Array, packet.Count, packet.Offset);
			return result;
		}

		internal static ArraySegment<byte> ReadPacket(ref FastBufferReader reader, [CanBeNull] ref byte[] buffer)
		{
			reader.ReadValueSafe(out uint value, default(FastBufferWriter.ForPrimitives));
			if (buffer == null || buffer.Length < value)
			{
				buffer = new byte[Math.Max(1024u, value)];
			}
			for (int i = 0; i < value; i++)
			{
				reader.ReadByteSafe(out var value2);
				buffer[i] = value2;
			}
			return new ArraySegment<byte>(buffer, 0, (int)value);
		}
	}
}
