using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public struct HarpoonSuccessZoneNetData : INetworkSerializable
	{
		public const int MaxZones = 8;

		public byte Count;

		public byte Z0Min;

		public byte Z0Max;

		public byte Z1Min;

		public byte Z1Max;

		public byte Z2Min;

		public byte Z2Max;

		public byte Z3Min;

		public byte Z3Max;

		public byte Z4Min;

		public byte Z4Max;

		public byte Z5Min;

		public byte Z5Max;

		public byte Z6Min;

		public byte Z6Max;

		public byte Z7Min;

		public byte Z7Max;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Count);
			serializer.SerializeValue(ref Z0Min);
			serializer.SerializeValue(ref Z0Max);
			serializer.SerializeValue(ref Z1Min);
			serializer.SerializeValue(ref Z1Max);
			serializer.SerializeValue(ref Z2Min);
			serializer.SerializeValue(ref Z2Max);
			serializer.SerializeValue(ref Z3Min);
			serializer.SerializeValue(ref Z3Max);
			serializer.SerializeValue(ref Z4Min);
			serializer.SerializeValue(ref Z4Max);
			serializer.SerializeValue(ref Z5Min);
			serializer.SerializeValue(ref Z5Max);
			serializer.SerializeValue(ref Z6Min);
			serializer.SerializeValue(ref Z6Max);
			serializer.SerializeValue(ref Z7Min);
			serializer.SerializeValue(ref Z7Max);
		}

		public void Clear()
		{
			Count = 0;
			Z0Min = 0;
			Z0Max = 0;
			Z1Min = 0;
			Z1Max = 0;
			Z2Min = 0;
			Z2Max = 0;
			Z3Min = 0;
			Z3Max = 0;
			Z4Min = 0;
			Z4Max = 0;
			Z5Min = 0;
			Z5Max = 0;
			Z6Min = 0;
			Z6Max = 0;
			Z7Min = 0;
			Z7Max = 0;
		}

		public bool TrySetZone(int index, byte min, byte max)
		{
			if (index < 0 || index >= 8)
			{
				return false;
			}
			switch (index)
			{
			case 0:
				Z0Min = min;
				Z0Max = max;
				break;
			case 1:
				Z1Min = min;
				Z1Max = max;
				break;
			case 2:
				Z2Min = min;
				Z2Max = max;
				break;
			case 3:
				Z3Min = min;
				Z3Max = max;
				break;
			case 4:
				Z4Min = min;
				Z4Max = max;
				break;
			case 5:
				Z5Min = min;
				Z5Max = max;
				break;
			case 6:
				Z6Min = min;
				Z6Max = max;
				break;
			case 7:
				Z7Min = min;
				Z7Max = max;
				break;
			}
			Count = (byte)Mathf.Max(Count, index + 1);
			return true;
		}

		public bool TryGetZone(int index, out byte min, out byte max)
		{
			min = 0;
			max = 0;
			if (index < 0 || index >= Count || index >= 8)
			{
				return false;
			}
			switch (index)
			{
			case 0:
				min = Z0Min;
				max = Z0Max;
				return true;
			case 1:
				min = Z1Min;
				max = Z1Max;
				return true;
			case 2:
				min = Z2Min;
				max = Z2Max;
				return true;
			case 3:
				min = Z3Min;
				max = Z3Max;
				return true;
			case 4:
				min = Z4Min;
				max = Z4Max;
				return true;
			case 5:
				min = Z5Min;
				max = Z5Max;
				return true;
			case 6:
				min = Z6Min;
				max = Z6Max;
				return true;
			case 7:
				min = Z7Min;
				max = Z7Max;
				return true;
			default:
				return false;
			}
		}
	}
}
