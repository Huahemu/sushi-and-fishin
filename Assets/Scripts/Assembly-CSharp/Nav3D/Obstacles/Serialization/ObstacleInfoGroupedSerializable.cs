using System.Collections.Generic;
using System.IO;

namespace Nav3D.Obstacles.Serialization
{
	public class ObstacleInfoGroupedSerializable : ObstacleInfoBaseSerializable
	{
		public int[] ObstacleInfosIDs { get; set; }

		public override ObstacleInfoSerializableType ObstacleType => ObstacleInfoSerializableType.GROUPED;

		public ObstacleInfoGroupedSerializable(ObstacleInfoGrouped _ObstacleInfo, int[] _SingleInfosIDs, int _ID)
			: base(_ObstacleInfo.Bounds, _ObstacleInfo.Triangles, _ID)
		{
			ObstacleInfosIDs = _SingleInfosIDs;
		}

		public ObstacleInfoGroupedSerializable(BinaryReader _Reader)
			: base(_Reader)
		{
			int num = _Reader.ReadInt32();
			ObstacleInfosIDs = new int[num];
			for (int i = 0; i < num; i++)
			{
				ObstacleInfosIDs[i] = _Reader.ReadInt32();
			}
		}

		public override void WriteIntoBinary(BinaryWriter _Writer)
		{
			base.WriteIntoBinary(_Writer);
			_Writer.Write(ObstacleInfosIDs.Length);
			int[] obstacleInfosIDs = ObstacleInfosIDs;
			foreach (int value in obstacleInfosIDs)
			{
				_Writer.Write(value);
			}
		}

		public ObstacleInfoGrouped GetDeserializedInstance(Dictionary<int, ObstacleInfoBase> _ObstacleInfosMap)
		{
			List<ObstacleInfoSingle> list = new List<ObstacleInfoSingle>(ObstacleInfosIDs.Length);
			int[] obstacleInfosIDs = ObstacleInfosIDs;
			foreach (int key in obstacleInfosIDs)
			{
				if (_ObstacleInfosMap.TryGetValue(key, out var value))
				{
					list.Add(value as ObstacleInfoSingle);
				}
			}
			return new ObstacleInfoGrouped(list, GetDeserializedTriangles(), GetDeserializedBounds());
		}
	}
}
