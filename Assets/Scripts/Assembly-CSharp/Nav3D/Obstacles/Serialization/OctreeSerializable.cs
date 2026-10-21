using System.Collections.Generic;
using System.IO;

namespace Nav3D.Obstacles.Serialization
{
	public class OctreeSerializable
	{
		public int ID { get; }

		private int LayersCount { get; }

		private float MinBucketSizeBase { get; }

		private float MinBucketSizeReal { get; }

		private int[] RootIDs { get; }

		private NodeSerializable[] Nodes { get; }

		public OctreeSerializable(NodeSerializable[] _Nodes, List<int> _RootIDs, int _LayersCount, float _MinBucketSizeBase, float _MinBucketSizeReal, int _ID)
		{
			ID = _ID;
			Nodes = _Nodes;
			RootIDs = _RootIDs.ToArray();
			LayersCount = _LayersCount;
			MinBucketSizeBase = _MinBucketSizeBase;
			MinBucketSizeReal = _MinBucketSizeReal;
		}

		public OctreeSerializable(BinaryReader _Reader, ObstacleDeserializingProgress _Progress)
		{
			ID = _Reader.ReadInt32();
			LayersCount = _Reader.ReadInt32();
			MinBucketSizeBase = _Reader.ReadSingle();
			MinBucketSizeReal = _Reader.ReadSingle();
			int num = _Reader.ReadInt32();
			RootIDs = new int[num];
			for (int i = 0; i < num; i++)
			{
				RootIDs[i] = _Reader.ReadInt32();
			}
			int num2 = _Reader.ReadInt32();
			Nodes = new NodeSerializable[num2];
			_Progress.SetProgress(1f);
			for (int j = 0; j < num2; j++)
			{
				Nodes[j] = NodeSerializable.ReadFromBytes(_Reader);
				_Progress.SetProgress((float)j / (float)num2);
			}
		}

		public void WriteIntoBinary(BinaryWriter _Writer)
		{
			_Writer.Write(ID);
			_Writer.Write(LayersCount);
			_Writer.Write(MinBucketSizeBase);
			_Writer.Write(MinBucketSizeReal);
			_Writer.Write(RootIDs.Length);
			int[] rootIDs = RootIDs;
			foreach (int value in rootIDs)
			{
				_Writer.Write(value);
			}
			_Writer.Write(Nodes.Length);
			NodeSerializable[] nodes = Nodes;
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i].WriteIntoBinary(_Writer);
			}
		}

		public Octree GetDeserializedInstance(ObstacleInfoBase _ObstacleInfo, ObstacleDeserializingProgress _Progress)
		{
			Node[] roots = GetRoots(_Progress, out var _DeserializedNodes);
			Octree octree = new Octree(_ObstacleInfo, roots, LayersCount, MinBucketSizeBase, MinBucketSizeReal, _DeserializedNodes.Count);
			foreach (KeyValuePair<int, Node> item in _DeserializedNodes)
			{
				item.Value.SetOctreeReference(octree);
			}
			return octree;
		}

		private Node[] GetRoots(ObstacleDeserializingProgress _Progress, out Dictionary<int, Node> _DeserializedNodes)
		{
			Dictionary<int, NodeSerializable> dictionary = new Dictionary<int, NodeSerializable>(Nodes.Length);
			NodeSerializable[] nodes = Nodes;
			foreach (NodeSerializable nodeSerializable in nodes)
			{
				dictionary.Add(nodeSerializable.ID, nodeSerializable);
			}
			Dictionary<int, Node> dictionary2 = new Dictionary<int, Node>(dictionary.Count);
			int num = 0;
			foreach (KeyValuePair<int, NodeSerializable> item in dictionary)
			{
				dictionary2.Add(item.Key, item.Value.GetDeserializedInstance());
				num++;
				if (num == 100)
				{
					num = 0;
					_Progress.SetProgress(num / dictionary.Count);
				}
			}
			int num2 = 0;
			foreach (KeyValuePair<int, Node> item2 in dictionary2)
			{
				if (item2.Value is Fork fork)
				{
					fork.SetChildren(dictionary2, ((ForkSerializable)dictionary[item2.Key]).GetChildrenData());
				}
				else if (item2.Value is Leaf leaf)
				{
					leaf.SetFreeAdjacents(dictionary2, ((LeafSerializable)dictionary[item2.Key]).FreeAdjacentIDs);
				}
				num2++;
				if (num2 == 100)
				{
					num2 = 0;
					_Progress.SetProgress(num2 / dictionary.Count);
				}
			}
			_DeserializedNodes = dictionary2;
			List<Node> list = new List<Node>(RootIDs.Length);
			int[] rootIDs = RootIDs;
			foreach (int key in rootIDs)
			{
				list.Add(dictionary2[key]);
			}
			return list.ToArray();
		}
	}
}
