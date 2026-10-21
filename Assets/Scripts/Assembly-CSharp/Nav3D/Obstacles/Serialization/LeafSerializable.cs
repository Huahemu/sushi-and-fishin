using System.IO;
using UnityEngine;

namespace Nav3D.Obstacles.Serialization
{
	internal class LeafSerializable : NodeSerializable
	{
		public int[] FreeAdjacentIDs { get; }

		protected override NodeSerializableType NodeType => NodeSerializableType.LEAF;

		public LeafSerializable(int _ID, Vector3Int _Index, float _Size, byte _GridLayer, bool _Occupied, int[] _FreeAdjacentIDs)
			: base(_ID, _Index, _Size, _GridLayer, _Occupied)
		{
			FreeAdjacentIDs = _FreeAdjacentIDs;
		}

		public LeafSerializable(BinaryReader _Reader)
			: base(_Reader)
		{
			int num = _Reader.ReadInt32();
			FreeAdjacentIDs = new int[num];
			for (int i = 0; i < num; i++)
			{
				FreeAdjacentIDs[i] = _Reader.ReadInt32();
			}
		}

		public override void WriteIntoBinary(BinaryWriter _Writer)
		{
			base.WriteIntoBinary(_Writer);
			_Writer.Write(FreeAdjacentIDs.Length);
			int[] freeAdjacentIDs = FreeAdjacentIDs;
			foreach (int value in freeAdjacentIDs)
			{
				_Writer.Write(value);
			}
		}

		public override Node GetDeserializedInstance()
		{
			return new Leaf(base.ID, new Vector3Int(base.IndexX, base.IndexY, base.IndexZ), base.Size, base.GridLayer, base.Occupied > 0);
		}
	}
}
