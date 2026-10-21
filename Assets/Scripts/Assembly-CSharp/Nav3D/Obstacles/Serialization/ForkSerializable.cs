using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nav3D.Obstacles.Serialization
{
	internal class ForkSerializable : NodeSerializable
	{
		private byte ChildrenMap { get; }

		private int[] ChildrenIDs { get; }

		protected override NodeSerializableType NodeType => NodeSerializableType.FORK;

		public ForkSerializable(int _ID, Vector3Int _Index, float _Size, byte _GridLayer, bool _Occupied, int[] _ChildrenIDs, byte _ChildrenMap)
			: base(_ID, _Index, _Size, _GridLayer, _Occupied)
		{
			ChildrenIDs = _ChildrenIDs;
			ChildrenMap = _ChildrenMap;
		}

		public ForkSerializable(BinaryReader _Reader)
			: base(_Reader)
		{
			int num = _Reader.ReadInt32();
			ChildrenIDs = new int[num];
			for (int i = 0; i < num; i++)
			{
				ChildrenIDs[i] = _Reader.ReadInt32();
			}
			ChildrenMap = _Reader.ReadByte();
		}

		public override void WriteIntoBinary(BinaryWriter _Writer)
		{
			base.WriteIntoBinary(_Writer);
			_Writer.Write(ChildrenIDs.Length);
			int[] childrenIDs = ChildrenIDs;
			foreach (int value in childrenIDs)
			{
				_Writer.Write(value);
			}
			_Writer.Write(ChildrenMap);
		}

		public override Node GetDeserializedInstance()
		{
			return new Fork(base.ID, new Vector3Int(base.IndexX, base.IndexY, base.IndexZ), base.Size, base.GridLayer, base.Occupied > 0);
		}

		public (ForkChildOctIndex OctIndex, int ID)[] GetChildrenData()
		{
			List<(ForkChildOctIndex, int)> list = new List<(ForkChildOctIndex, int)>(8);
			int num = 0;
			byte b = 1;
			for (int i = 0; i < 8; i++)
			{
				if ((ChildrenMap & b) == 0)
				{
					b <<= 1;
					continue;
				}
				list.Add(((ForkChildOctIndex)b, ChildrenIDs[num]));
				num++;
				b <<= 1;
			}
			return list.ToArray();
		}
	}
}
