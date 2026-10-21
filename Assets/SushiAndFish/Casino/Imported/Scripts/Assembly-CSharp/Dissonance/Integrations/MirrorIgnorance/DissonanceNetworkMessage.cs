using SushiAndFish.Casino.Original;
using global::System;
using global::Dissonance.Extensions;
using global::Mirror;

namespace Dissonance.Integrations.MirrorIgnorance
{
	internal struct DissonanceNetworkMessage : NetworkMessage, IDisposable
	{
		public ArraySegment<byte> Data;

		public DissonanceNetworkMessage(ArraySegment<byte> packet)
		{
			Data = packet.CopyToSegment(DissonanceNetworkMessageExtensions.SerializationBuffers.Get());
		}

		public void Dispose()
		{
			byte[] array = Data.Array;
			if (array != null && array.Length == 1024)
			{
				DissonanceNetworkMessageExtensions.SerializationBuffers.Put(array);
				Data = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);
			}
		}
	}
}
