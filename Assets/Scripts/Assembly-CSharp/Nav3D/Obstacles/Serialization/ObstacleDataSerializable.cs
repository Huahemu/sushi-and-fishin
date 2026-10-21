using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nav3D.Obstacles.Serialization
{
	public class ObstacleDataSerializable
	{
		public string[] ImprintData { get; set; }

		public float MinBucketSize { get; set; }

		public ObstacleInfoBaseSerializable[] ObstacleInfos { get; set; }

		public OctreeSerializable[] Octrees { get; set; }

		public ObstacleDataSerializable(Dictionary<ObstacleInfoBase, Obstacle> _ObstacleDatas, string[] _ImprintData, ObstacleSerializingProgress _Progress)
		{
			ImprintData = _ImprintData;
			List<ObstacleInfoBaseSerializable> list = new List<ObstacleInfoBaseSerializable>();
			List<OctreeSerializable> list2 = new List<OctreeSerializable>();
			int num = 0;
			foreach (KeyValuePair<ObstacleInfoBase, Obstacle> _ObstacleData in _ObstacleDatas)
			{
				if (_Progress.CancellationToken.IsCancellationRequested)
				{
					return;
				}
				ObstacleInfoBase key = _ObstacleData.Key;
				int iD = num;
				num++;
				if (key is ObstacleInfoSingle obstacleInfoSingle)
				{
					list.Add(obstacleInfoSingle.GetSerializableInstance(iD));
				}
				else if (key is ObstacleInfoGrouped obstacleInfoGrouped)
				{
					List<int> list3 = new List<int>();
					foreach (KeyValuePair<int, List<ObstacleInfoSingle>> obstacleInfo in obstacleInfoGrouped.ObstacleInfos)
					{
						if (!obstacleInfo.Value.Any())
						{
							continue;
						}
						foreach (ObstacleInfoSingle item in obstacleInfo.Value)
						{
							list3.Add(num);
							list.Add(item.GetSerializableInstance(num));
							num++;
						}
					}
					list.Add(obstacleInfoGrouped.GetSerializableInstance(list3.ToArray(), iD));
				}
				if (_Progress.CancellationToken.IsCancellationRequested)
				{
					return;
				}
				list2.Add(_ObstacleData.Value.GetSerializableOctreeInstance(_Progress, iD));
			}
			ObstacleInfos = list.ToArray();
			Octrees = list2.ToArray();
		}

		public ObstacleDataSerializable(Stream _Stream, ObstacleDeserializingProgress _Progress)
		{
			_Stream.Position = 0L;
			using BinaryReader binaryReader = new BinaryReader(_Stream);
			int num = binaryReader.ReadInt32();
			ImprintData = new string[num];
			for (int i = 0; i < num; i++)
			{
				ImprintData[i] = binaryReader.ReadString();
			}
			MinBucketSize = binaryReader.ReadSingle();
			int num2 = binaryReader.ReadInt32();
			ObstacleInfos = new ObstacleInfoBaseSerializable[num2];
			for (int j = 0; j < num2; j++)
			{
				ObstacleInfos[j] = ObstacleInfoBaseSerializable.ReadFromBytes(binaryReader);
				_Progress.SetProgress((float)j / (float)num2);
			}
			int num3 = binaryReader.ReadInt32();
			Octrees = new OctreeSerializable[num3];
			for (int k = 0; k < num3; k++)
			{
				Octrees[k] = new OctreeSerializable(binaryReader, _Progress);
			}
		}

		public void WriteIntoStream(Stream _Stream)
		{
			_Stream.Position = 0L;
			using BinaryWriter binaryWriter = new BinaryWriter(_Stream, Encoding.UTF8, leaveOpen: true);
			binaryWriter.Write(ImprintData.Length);
			for (int i = 0; i < ImprintData.Length; i++)
			{
				binaryWriter.Write(ImprintData[i]);
			}
			binaryWriter.Write(MinBucketSize);
			binaryWriter.Write(ObstacleInfos.Length);
			ObstacleInfoBaseSerializable[] obstacleInfos = ObstacleInfos;
			for (int j = 0; j < obstacleInfos.Length; j++)
			{
				obstacleInfos[j].WriteIntoBinary(binaryWriter);
			}
			binaryWriter.Write(Octrees.Length);
			OctreeSerializable[] octrees = Octrees;
			for (int j = 0; j < octrees.Length; j++)
			{
				octrees[j].WriteIntoBinary(binaryWriter);
			}
		}

		public Dictionary<ObstacleInfoBase, Obstacle> Unpack(ObstacleDeserializingProgress _Progress)
		{
			Dictionary<int, ObstacleInfoBaseSerializable> dictionary = new Dictionary<int, ObstacleInfoBaseSerializable>(ObstacleInfos.Length);
			ObstacleInfoBaseSerializable[] obstacleInfos = ObstacleInfos;
			foreach (ObstacleInfoBaseSerializable obstacleInfoBaseSerializable in obstacleInfos)
			{
				dictionary.Add(obstacleInfoBaseSerializable.ID, obstacleInfoBaseSerializable);
			}
			Dictionary<int, ObstacleInfoBase> dictionary2 = new Dictionary<int, ObstacleInfoBase>();
			foreach (KeyValuePair<int, ObstacleInfoBaseSerializable> item in dictionary)
			{
				if (item.Value is ObstacleInfoSingleSerializable obstacleInfoSingleSerializable)
				{
					dictionary2.Add(item.Key, obstacleInfoSingleSerializable.GetDeserializedInstance());
					_Progress.SetProgress((float)dictionary2.Count / (float)dictionary.Count);
				}
			}
			foreach (int key in dictionary2.Keys)
			{
				dictionary.Remove(key);
			}
			int num = 0;
			foreach (KeyValuePair<int, ObstacleInfoBaseSerializable> item2 in dictionary)
			{
				ObstacleInfoGrouped deserializedInstance = (item2.Value as ObstacleInfoGroupedSerializable).GetDeserializedInstance(dictionary2);
				dictionary2.Add(item2.Key, deserializedInstance);
				_Progress.SetProgress((float)num / (float)dictionary.Count);
				num++;
			}
			Dictionary<ObstacleInfoBase, Obstacle> dictionary3 = new Dictionary<ObstacleInfoBase, Obstacle>(Octrees.Length);
			OctreeSerializable[] octrees = Octrees;
			foreach (OctreeSerializable octreeSerializable in octrees)
			{
				ObstacleInfoBase obstacleInfoBase = dictionary2[octreeSerializable.ID];
				Octree deserializedInstance2 = octreeSerializable.GetDeserializedInstance(obstacleInfoBase, _Progress);
				Obstacle value = new Obstacle(obstacleInfoBase, deserializedInstance2, MinBucketSize);
				dictionary3.Add(obstacleInfoBase, value);
			}
			return dictionary3;
		}
	}
}
