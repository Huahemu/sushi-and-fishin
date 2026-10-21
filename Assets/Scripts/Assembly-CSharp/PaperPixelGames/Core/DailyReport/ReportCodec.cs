using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace PaperPixelGames.Core.DailyReport
{
	public static class ReportCodec
	{
		public static byte[] ToBytes<T>(T value, bool gzip)
		{
			string s = JsonUtility.ToJson(value);
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			if (!gzip)
			{
				return bytes;
			}
			using MemoryStream memoryStream = new MemoryStream();
			using (GZipStream gZipStream = new GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
			{
				gZipStream.Write(bytes, 0, bytes.Length);
			}
			return memoryStream.ToArray();
		}

		public static T FromBytes<T>(byte[] bytes, bool gzip)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return default;
			}
			byte[] bytes2;
			if (!gzip)
			{
				bytes2 = bytes;
			}
			else
			{
				using MemoryStream stream = new MemoryStream(bytes);
				using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
				using MemoryStream memoryStream = new MemoryStream();
				gZipStream.CopyTo(memoryStream);
				bytes2 = memoryStream.ToArray();
			}
			return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes2));
		}
	}
}
