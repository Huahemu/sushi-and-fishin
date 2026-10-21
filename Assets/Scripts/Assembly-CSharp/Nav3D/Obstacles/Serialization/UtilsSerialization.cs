using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles.Serialization
{
	public static class UtilsSerialization
	{
		public static void SerializeObstacleData(ObstacleDataSerializable _ObstacleData, string _FilePath, ObstacleSerializingProgress _Progress)
		{
			try
			{
				_Progress.SetStatus(ObstacleSerializingProgress.ObstacleSerializingStatus.SERIALIZING_DATA);
				using MemoryStream memoryStream = new MemoryStream();
				_ObstacleData.WriteIntoStream(memoryStream);
				_Progress.SetStatus(ObstacleSerializingProgress.ObstacleSerializingStatus.COMPRESSING_DATA);
				using FileStream fileStream = new FileStream(_FilePath, FileMode.OpenOrCreate);
				fileStream.SetLength(0L);
				using DeflateStream destination = new DeflateStream(fileStream, System.IO.Compression.CompressionLevel.Optimal);
				memoryStream.Position = 0L;
				memoryStream.CopyTo(destination);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		public static ObstacleDataSerializable DeserializeObstacleData(byte[] _Binary, ObstacleDeserializingProgress _Progress)
		{
			try
			{
				_Progress.SetStatus(ObstacleDeserializingProgress.ObstacleDeserializingStatus.DECOMPRESSION);
				using MemoryStream memoryStream = new MemoryStream(_Binary);
				memoryStream.Position = 0L;
				using MemoryStream memoryStream2 = new MemoryStream();
				using DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
				deflateStream.CopyTo(memoryStream2);
				_Progress.SetProgress(1f);
				_Progress.SetStatus(ObstacleDeserializingProgress.ObstacleDeserializingStatus.DESERIALIZING);
				memoryStream2.Position = 0L;
				return new ObstacleDataSerializable(memoryStream2, _Progress);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
			return null;
		}

		public static byte[] TrianglesToBytes(IEnumerable<Triangle> _Triangles)
		{
			List<byte> list = new List<byte>(_Triangles.Count() * 36);
			foreach (Triangle _Triangle in _Triangles)
			{
				list.AddRange(BitConverter.GetBytes(_Triangle.V1.x));
				list.AddRange(BitConverter.GetBytes(_Triangle.V1.y));
				list.AddRange(BitConverter.GetBytes(_Triangle.V1.z));
				list.AddRange(BitConverter.GetBytes(_Triangle.V2.x));
				list.AddRange(BitConverter.GetBytes(_Triangle.V2.y));
				list.AddRange(BitConverter.GetBytes(_Triangle.V2.z));
				list.AddRange(BitConverter.GetBytes(_Triangle.V3.x));
				list.AddRange(BitConverter.GetBytes(_Triangle.V3.y));
				list.AddRange(BitConverter.GetBytes(_Triangle.V3.z));
			}
			return list.ToArray();
		}

		public static List<Triangle> ToTriangles(byte[] _Bytes, int _StartIndex, int _Count)
		{
			int num = _Count / 36;
			List<Triangle> list = new List<Triangle>(num);
			for (int i = _StartIndex; i < num; i++)
			{
				int num2 = i * 36;
				list.Add(new Triangle(new Vector3(BitConverter.ToSingle(_Bytes, num2), BitConverter.ToSingle(_Bytes, num2 + 4), BitConverter.ToSingle(_Bytes, num2 + 8)), new Vector3(BitConverter.ToSingle(_Bytes, num2 + 12), BitConverter.ToSingle(_Bytes, num2 + 16), BitConverter.ToSingle(_Bytes, num2 + 20)), new Vector3(BitConverter.ToSingle(_Bytes, num2 + 24), BitConverter.ToSingle(_Bytes, num2 + 28), BitConverter.ToSingle(_Bytes, num2 + 32))));
			}
			return list;
		}
	}
}
