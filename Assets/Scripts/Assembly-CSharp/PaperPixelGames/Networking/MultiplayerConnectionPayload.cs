using System;
using System.Text;

namespace PaperPixelGames.Networking
{
	public static class MultiplayerConnectionPayload
	{
		public const ushort ProtocolVersion = 5;

		private const int MaxReleaseVersionByteLength = 64;

		private const int FixedPayloadSize = 24;

		public static byte[] Create(int clientVersion, ulong platformId, string releaseVersion)
		{
			return Create(clientVersion, platformId, releaseVersion, 0uL);
		}

		public static byte[] Create(int clientVersion, ulong platformId, string releaseVersion, ulong sessionEpoch)
		{
			string text = releaseVersion?.Trim();
			if (string.IsNullOrEmpty(text))
			{
				return Array.Empty<byte>();
			}
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			if (bytes.Length == 0 || bytes.Length > 64)
			{
				return Array.Empty<byte>();
			}
			byte[] array = new byte[24 + bytes.Length];
			int offset = 0;
			WriteBytes(BitConverter.GetBytes((ushort)5), array, ref offset);
			WriteBytes(BitConverter.GetBytes(clientVersion), array, ref offset);
			WriteBytes(BitConverter.GetBytes(platformId), array, ref offset);
			WriteBytes(BitConverter.GetBytes(sessionEpoch), array, ref offset);
			WriteBytes(BitConverter.GetBytes((ushort)bytes.Length), array, ref offset);
			WriteBytes(bytes, array, ref offset);
			return array;
		}

		public static bool TryParse(byte[] payload, out ParsedPayload parsed)
		{
			parsed = default;
			if (payload == null || payload.Length < 24)
			{
				return false;
			}
			try
			{
				int num = 0;
				ushort protocolVersion = BitConverter.ToUInt16(payload, num);
				num += 2;
				int clientVersion = BitConverter.ToInt32(payload, num);
				num += 4;
				ulong num2 = BitConverter.ToUInt64(payload, num);
				num += 8;
				ulong sessionEpoch = BitConverter.ToUInt64(payload, num);
				num += 8;
				ushort num3 = BitConverter.ToUInt16(payload, num);
				num += 2;
				if (num3 == 0 || num3 > 64 || payload.Length != 24 + num3)
				{
					return false;
				}
				string text = Encoding.UTF8.GetString(payload, num, num3).Trim();
				if (string.IsNullOrEmpty(text))
				{
					return false;
				}
				parsed = new ParsedPayload(protocolVersion, num2, clientVersion, text, sessionEpoch);
				return num2 != 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static void WriteBytes(byte[] source, byte[] destination, ref int offset)
		{
			Buffer.BlockCopy(source, 0, destination, offset, source.Length);
			offset += source.Length;
		}
	}
}
