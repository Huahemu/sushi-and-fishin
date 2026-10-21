using System.Collections.Generic;
using UnityEngine;

namespace Nav3D.Common
{
	public static class LineVoxelizer
	{
		public static List<Vector3Int> GetVoxels(Segment3 _Line, float _VoxelSize)
		{
			float num = 1f;
			List<Vector3Int> list = new List<Vector3Int>();
			Bounds bucketBounds = UtilsSpatialGrid.GetBucketBounds(_Line.Start, _VoxelSize);
			Vector3 min = bucketBounds.min;
			Vector3 max = bucketBounds.max;
			Vector3 start = _Line.Start;
			Vector3 point = _Line.Start + _Line.DirectionMagn;
			Vector3Int bucketIndex = UtilsSpatialGrid.GetBucketIndex(start, _VoxelSize);
			Vector3Int bucketIndex2 = UtilsSpatialGrid.GetBucketIndex(point, _VoxelSize);
			if (bucketIndex != bucketIndex2 && _Line.DirectionMagn.sqrMagnitude < 1E-08f)
			{
				return new List<Vector3Int> { bucketIndex };
			}
			int num2 = bucketIndex.x;
			int x = bucketIndex2.x;
			int num3;
			float num4;
			float num5;
			if (_Line.DirectionMagn.x > 0f)
			{
				num3 = 1;
				num4 = _VoxelSize / _Line.DirectionMagn.x;
				num5 = Mathf.Abs((max.x - start.x) / _Line.DirectionMagn.x);
			}
			else if (_Line.DirectionMagn.x < 0f)
			{
				num3 = -1;
				num4 = _VoxelSize / (0f - _Line.DirectionMagn.x);
				num5 = Mathf.Abs((start.x - min.x) / _Line.DirectionMagn.x);
			}
			else
			{
				num3 = 0;
				num4 = num;
				num5 = num;
			}
			int num6 = bucketIndex.y;
			int y = bucketIndex2.y;
			int num7;
			float num8;
			float num9;
			if (_Line.DirectionMagn.y > 0f)
			{
				num7 = 1;
				num8 = _VoxelSize / _Line.DirectionMagn.y;
				num9 = Mathf.Abs((max.y - start.y) / _Line.DirectionMagn.y);
			}
			else if (_Line.DirectionMagn.y < 0f)
			{
				num7 = -1;
				num8 = _VoxelSize / (0f - _Line.DirectionMagn.y);
				num9 = Mathf.Abs((start.y - min.y) / _Line.DirectionMagn.y);
			}
			else
			{
				num7 = 0;
				num8 = num;
				num9 = num;
			}
			int num10 = bucketIndex.z;
			int z = bucketIndex2.z;
			int num11;
			float num12;
			float num13;
			if (_Line.DirectionMagn.z > 0f)
			{
				num11 = 1;
				num12 = _VoxelSize / _Line.DirectionMagn.z;
				num13 = Mathf.Abs((max.z - start.z) / _Line.DirectionMagn.z);
			}
			else if (_Line.DirectionMagn.z < 0f)
			{
				num11 = -1;
				num12 = _VoxelSize / (0f - _Line.DirectionMagn.z);
				num13 = Mathf.Abs((start.z - min.z) / _Line.DirectionMagn.z);
			}
			else
			{
				num11 = 0;
				num12 = num;
				num13 = num;
			}
			list.Add(new Vector3Int(num2, num6, num10));
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			while (num2 != x || num6 != y || num10 != z)
			{
				if (num5 < num9 && num5 < num13)
				{
					if (flag)
					{
						break;
					}
					num2 += num3;
					num5 += num4;
					if (num2 == x)
					{
						flag = true;
					}
				}
				else if (num9 < num13)
				{
					if (flag2)
					{
						break;
					}
					num6 += num7;
					num9 += num8;
					if (num6 == y)
					{
						flag2 = true;
					}
				}
				else
				{
					if (flag3)
					{
						break;
					}
					num10 += num11;
					num13 += num12;
					if (num10 == z)
					{
						flag3 = true;
					}
				}
				list.Add(new Vector3Int(num2, num6, num10));
			}
			return list;
		}
	}
}
