using System;
using System.Collections.Generic;
using System.IO;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Obstacles.Serialization
{
	public abstract class ObstacleInfoBaseSerializable
	{
		public enum ObstacleInfoSerializableType
		{
			SINGLE = 0,
			GROUPED = 1
		}

		private static readonly string UNKNOWN_OBSTACLE_TYPE = "Unknown ObstacleInfoSerializableType value: {0}";

		public int ID { get; set; }

		public float BoundsSizeX { get; set; }

		public float BoundsSizeY { get; set; }

		public float BoundsSizeZ { get; set; }

		public float BoundsCenterX { get; set; }

		public float BoundsCenterY { get; set; }

		public float BoundsCenterZ { get; set; }

		public byte[] Triangles { get; set; }

		public abstract ObstacleInfoSerializableType ObstacleType { get; }

		public static ObstacleInfoBaseSerializable ReadFromBytes(BinaryReader _Reader)
		{
			ObstacleInfoSerializableType obstacleInfoSerializableType = (ObstacleInfoSerializableType)_Reader.ReadInt32();
			return obstacleInfoSerializableType switch
			{
				ObstacleInfoSerializableType.SINGLE => new ObstacleInfoSingleSerializable(_Reader), 
				ObstacleInfoSerializableType.GROUPED => new ObstacleInfoGroupedSerializable(_Reader), 
				_ => throw new Exception(string.Format(UNKNOWN_OBSTACLE_TYPE, obstacleInfoSerializableType)), 
			};
		}

		public ObstacleInfoBaseSerializable(Bounds _Bounds, List<Triangle> _Triangles, int _ID)
		{
			ID = _ID;
			BoundsSizeX = _Bounds.size.x;
			BoundsSizeY = _Bounds.size.y;
			BoundsSizeZ = _Bounds.size.z;
			BoundsCenterX = _Bounds.center.x;
			BoundsCenterY = _Bounds.center.y;
			BoundsCenterZ = _Bounds.center.z;
			Triangles = UtilsSerialization.TrianglesToBytes(_Triangles);
		}

		protected ObstacleInfoBaseSerializable(BinaryReader _Reader)
		{
			ID = _Reader.ReadInt32();
			BoundsSizeX = _Reader.ReadSingle();
			BoundsSizeY = _Reader.ReadSingle();
			BoundsSizeZ = _Reader.ReadSingle();
			BoundsCenterX = _Reader.ReadSingle();
			BoundsCenterY = _Reader.ReadSingle();
			BoundsCenterZ = _Reader.ReadSingle();
			Triangles = _Reader.ReadBytes(_Reader.ReadInt32());
		}

		public virtual void WriteIntoBinary(BinaryWriter _Writer)
		{
			_Writer.Write((int)ObstacleType);
			_Writer.Write(ID);
			_Writer.Write(BoundsSizeX);
			_Writer.Write(BoundsSizeY);
			_Writer.Write(BoundsSizeZ);
			_Writer.Write(BoundsCenterX);
			_Writer.Write(BoundsCenterY);
			_Writer.Write(BoundsCenterZ);
			_Writer.Write(Triangles.Length);
			_Writer.Write(Triangles);
		}

		protected List<Triangle> GetDeserializedTriangles()
		{
			return UtilsSerialization.ToTriangles(Triangles, 0, Triangles.Length);
		}

		protected Bounds GetDeserializedBounds()
		{
			return new Bounds(new Vector3(BoundsCenterX, BoundsCenterY, BoundsCenterZ), new Vector3(BoundsSizeX, BoundsSizeY, BoundsSizeZ));
		}
	}
}
