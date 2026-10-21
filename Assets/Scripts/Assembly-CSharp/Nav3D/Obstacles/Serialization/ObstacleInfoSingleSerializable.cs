using System;
using System.IO;

namespace Nav3D.Obstacles.Serialization
{
	internal class ObstacleInfoSingleSerializable : ObstacleInfoBaseSerializable
	{
		public enum ObstacleInfoSingleType
		{
			MESH = 0,
			TERRAIN = 1
		}

		private const string UNKNOWN_DESERIALIZATION_OBSTACLE_INFO_TYPE = "Unknown obstacle info type: {0}";

		private const string UNKNOWN_SERIALIZATION_OBSTACLE_INFO_TYPE = "Unknown obstacle info single type: {0}";

		public int ObstacleControllerID { get; set; }

		public ObstacleInfoSingleType ObstacleInfoType { get; set; }

		public override ObstacleInfoSerializableType ObstacleType => ObstacleInfoSerializableType.SINGLE;

		public ObstacleInfoSingleSerializable(ObstacleInfoSingle _ObstacleInfo, int _ID)
			: base(_ObstacleInfo.Bounds, _ObstacleInfo.Triangles, _ID)
		{
			ObstacleControllerID = _ObstacleInfo.ObstacleControllerID;
			if (_ObstacleInfo is ObstacleInfoMesh)
			{
				ObstacleInfoType = ObstacleInfoSingleType.MESH;
				return;
			}
			if (_ObstacleInfo is ObstacleInfoTerrain)
			{
				ObstacleInfoType = ObstacleInfoSingleType.TERRAIN;
				return;
			}
			throw new Exception($"Unknown obstacle info single type: {_ObstacleInfo.GetType().FullName}");
		}

		public ObstacleInfoSingleSerializable(BinaryReader _Reader)
			: base(_Reader)
		{
			ObstacleControllerID = _Reader.ReadInt32();
			ObstacleInfoType = (ObstacleInfoSingleType)_Reader.ReadInt32();
		}

		public override void WriteIntoBinary(BinaryWriter _Writer)
		{
			base.WriteIntoBinary(_Writer);
			_Writer.Write(ObstacleControllerID);
			_Writer.Write((int)ObstacleInfoType);
		}

		public ObstacleInfoSingle GetDeserializedInstance()
		{
			if (ObstacleInfoType == ObstacleInfoSingleType.MESH)
			{
				return new ObstacleInfoMesh(ObstacleControllerID, GetDeserializedBounds(), GetDeserializedTriangles());
			}
			if (ObstacleInfoType == ObstacleInfoSingleType.TERRAIN)
			{
				return new ObstacleInfoTerrain(ObstacleControllerID, GetDeserializedBounds(), GetDeserializedTriangles());
			}
			throw new Exception($"Unknown obstacle info type: {ObstacleInfoType}");
		}
	}
}
