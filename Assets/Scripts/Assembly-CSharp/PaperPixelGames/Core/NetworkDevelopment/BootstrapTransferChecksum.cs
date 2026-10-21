using System;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public static class BootstrapTransferChecksum
	{
		private static readonly uint[] Table = BuildTable();

		private static uint[] BuildTable()
		{
			uint[] array = new uint[256];
			for (uint num = 0u; num < array.Length; num++)
			{
				uint num2 = num;
				for (int i = 0; i < 8; i++)
				{
					num2 = (num2 >> 1) ^ (uint)(((num2 & 1) != 0) ? (-306674912) : 0);
				}
				array[num] = num2;
			}
			return array;
		}

		public static uint Compute(byte[] data)
		{
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}
			uint num = uint.MaxValue;
			for (int i = 0; i < data.Length; i++)
			{
				num = Table[(num ^ data[i]) & 0xFF] ^ (num >> 8);
			}
			return ~num;
		}
	}
}
