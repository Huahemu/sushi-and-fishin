using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Pathfinding
{
	public static class Pathfinder
	{
		private const float SQRDISTANCE_EPSILON = 1E-05f;

		private const string COMPOUND_FIND_PATH_FRAGMENT_RESULT = "Fragment {0}/{1} stats: Start: {2}. Target: {3}, ResultCode: {4}, Duration(ms): {5}";

		private const string COMPOUND_FIND_PATH_POSTPROCESS_RESULT = "Postprocess stats(ms): Optimizing duration: {0}, Smoothing duration: {1}";

		private static readonly string FIND_PATH_INVOKE = "FindPath: for points {0} invoked";

		public static async Task<PathfindingResult> FindPath(PathfindingRequest _Request, CancellationToken _CancellationTokenExternal, CancellationToken _CancellationTokenTimeout, Action<string> _StatusCallback, Log _Log)
		{
			PathfindingResult result = default;
			object obj;
			int num2;
			try
			{
				_Log?.WriteFormat(FIND_PATH_INVOKE, UtilsCommon.GetPointsString(_Request.Points));
				Vector3[] extendedPoints = _Request.GetExtendedPoints();
				Vector3[] array = ((!_Request.Loop) ? extendedPoints.Copy() : ((extendedPoints.First() != extendedPoints.Last()) ? new Vector3[1] { extendedPoints.Last() }.Concat(extendedPoints).ToArray() : extendedPoints.Copy()));
				int num = array.Length - 1;
				List<List<Vector3>> list = new List<List<Vector3>>(num);
				List<(Vector3, PathfindingResultCode)> list2 = new List<(Vector3, PathfindingResultCode)>(num);
				List<(Vector3, TimeSpan)> list3 = new List<(Vector3, TimeSpan)>(num);
				TimeSpan pathfindingDuration = TimeSpan.Zero;
				for (int i = 1; i <= num; i++)
				{
					Vector3 vector = array[i - 1];
					Vector3 vector2 = array[i];
					bool tryRepositionStartToFreeLeaf = i == 1 && !_Request.Loop && _Request.TryRepositionStartIfOccupied;
					bool tryRepositionTargetToFreeLeaf = i == num && !_Request.Loop && _Request.TryRepositionTargetIfOccupied;
					_StatusCallback($"Pathfinding fragment {i}/{num}");
					List<Vector3> item = FindPath(vector, vector2, tryRepositionStartToFreeLeaf, tryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout, out var _Code, out var _PathfindingDuration);
					_Log?.WriteFormat("Fragment {0}/{1} stats: Start: {2}. Target: {3}, ResultCode: {4}, Duration(ms): {5}", i, num, vector.ToStringExt(), vector2.ToStringExt(), _Code, _PathfindingDuration.TotalMilliseconds);
					pathfindingDuration = pathfindingDuration.Add(_PathfindingDuration);
					list2.Add((vector2, _Code));
					list3.Add((vector2, _PathfindingDuration));
					if (_Code != PathfindingResultCode.SUCCEEDED)
					{
						if (i == 1 && _Code == PathfindingResultCode.START_POINT_INSIDE_OBSTACLE)
						{
							result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, new List<(Vector3, PathfindingResultCode)> { (vector2, _Code) }, new List<(Vector3, TimeSpan)> { (vector2, _PathfindingDuration) }, _Code));
							return result;
						}
						if (!_Request.SkipUnpassableTargets)
						{
							result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, (_Code == PathfindingResultCode.START_POINT_INSIDE_OBSTACLE) ? PathfindingResultCode.TARGET_POINT_INSIDE_OBSTACLE : _Code));
							return result;
						}
					}
					else
					{
						list.Add(item);
					}
				}
				_Log?.Write($"Pathfinding finished ({pathfindingDuration.TotalMilliseconds} ms.)");
				List<Vector3> list4 = new List<Vector3>(list.Select((List<Vector3> _Path) => _Path.Count).Sum());
				foreach (List<Vector3> item2 in list)
				{
					list4.AddRange(item2);
				}
				if (_CancellationTokenExternal.IsCancellationRequested)
				{
					result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, PathfindingResultCode.CANCELLED));
					return result;
				}
				if (_CancellationTokenTimeout.IsCancellationRequested)
				{
					result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, PathfindingResultCode.TIMEOUT));
					return result;
				}
				if (list.Count == 0)
				{
					PathfindingResultCode resultCode = ((list2.Count <= 0) ? PathfindingResultCode.PATH_DOES_NOT_EXIST : list2[list2.Count - 1].Item2);
					result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, resultCode));
					return result;
				}
				PostprocessPath(list, _Request.Loop, _Request.Smooth, _Request.SmoothRatio, _StatusCallback, _Log, out var _PathOptimized, out var _PathSmoothed, out var _TargetIndices, out var _OptimizingDuration, out var _SmoothingDuration);
				_Log?.WriteFormat("Postprocess stats(ms): Optimizing duration: {0}, Smoothing duration: {1}", _OptimizingDuration.TotalMilliseconds, _SmoothingDuration.TotalMilliseconds);
				_StatusCallback("Finished");
				if (_CancellationTokenExternal.IsCancellationRequested)
				{
					result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, PathfindingResultCode.CANCELLED));
					return result;
				}
				if (_CancellationTokenTimeout.IsCancellationRequested)
				{
					result = await Task.FromResult(new PathfindingResult(extendedPoints, pathfindingDuration, _Request.Timeout, list2, list3, PathfindingResultCode.TIMEOUT));
					return result;
				}
				result = await Task.FromResult(new PathfindingResult(list4.ToArray(), _PathOptimized, _PathSmoothed, pathfindingDuration, _OptimizingDuration, _SmoothingDuration, _TargetIndices, list2, list3));
				return result;
			}
			catch (Exception ex)
			{
				obj = ex;
				num2 = 1;
			}
			if (num2 != 1)
			{
				return result;
			}
			Debug.LogException((Exception)obj);
			return await Task.FromResult(new PathfindingResult(_Request.Points, TimeSpan.Zero, _Request.Timeout, new List<(Vector3, PathfindingResultCode)>(), new List<(Vector3, TimeSpan)>(), PathfindingResultCode.UNKNOWN));
		}

		private static List<Vector3> FindPath(Vector3 _Start, Vector3 _Target, bool _TryRepositionStartToFreeLeaf, bool _TryRepositionTargetToFreeLeaf, CancellationToken _CancellationTokenExternal, CancellationToken _CancellationTokenTimeout, out PathfindingResultCode _Code, out TimeSpan _PathfindingDuration, bool _SkipStart = false)
		{
			DateTime now = DateTime.Now;
			if ((_Start - _Target).sqrMagnitude < 1E-05f)
			{
				_Code = PathfindingResultCode.SUCCEEDED;
				_PathfindingDuration = DateTime.Now - now;
				return new List<Vector3> { _Start, _Target };
			}
			List<Obstacle> list = (ObstacleManager.Doomed ? new List<Obstacle>() : ObstacleManager.Instance.GetObstaclesCrossingTheLine(_Start, _Target));
			if (list.Count > 1)
			{
				list = list.OrderBy((Obstacle _Obstacle) => _Obstacle.Bounds.GetIntersection(new Segment3(_Start, _Target)).Min((Vector3 _Point) => Vector3.SqrMagnitude(_Start - _Point))).ToList();
			}
			List<Vector3> list2 = new List<Vector3>();
			if (!_SkipStart)
			{
				list2.Add(_Start);
			}
			Segment3 segment = new Segment3(_Start, _Target);
			for (int num = 0; num < list.Count; num++)
			{
				Obstacle obstacle = list[num];
				bool tryRepositionStartToFreeLeaf = (num == 0) & _TryRepositionStartToFreeLeaf;
				bool tryRepositionTargetToFreeLeaf = (num == list.Count - 1) & _TryRepositionTargetToFreeLeaf;
				if (_CancellationTokenExternal.IsCancellationRequested || _CancellationTokenTimeout.IsCancellationRequested)
				{
					_Code = (_CancellationTokenExternal.IsCancellationRequested ? PathfindingResultCode.CANCELLED : PathfindingResultCode.TIMEOUT);
					_PathfindingDuration = DateTime.Now - now;
					return new List<Vector3> { _Start, _Target };
				}
				List<Vector3> list3 = (from _Point in obstacle.Bounds.GetIntersection(segment)
					orderby Vector3.SqrMagnitude(_Start - _Point)
					select _Point).ToList();
				if (list3.Count == 0)
				{
					OctreePathfindingResult octreePathfindingResult = obstacle.FindPath(_Start, _Target, tryRepositionStartToFreeLeaf, tryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
					if (octreePathfindingResult.Failed)
					{
						_Code = octreePathfindingResult.ResultCode;
						_PathfindingDuration = TimeSpan.Zero;
						return null;
					}
					list2 = octreePathfindingResult.Path;
					continue;
				}
				if (list3.Count == 2)
				{
					OctreePathfindingResult octreePathfindingResult = obstacle.FindPath(list3.First(), list3.Last(), tryRepositionStartToFreeLeaf, tryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
					if (octreePathfindingResult.Failed)
					{
						_Code = octreePathfindingResult.ResultCode;
						_PathfindingDuration = TimeSpan.Zero;
						return null;
					}
					list2.AddRange(octreePathfindingResult.Path);
					continue;
				}
				if (obstacle.Bounds.Contains(_Start))
				{
					OctreePathfindingResult octreePathfindingResult = obstacle.FindPath(_Start, list3.First(), tryRepositionStartToFreeLeaf, tryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
					if (octreePathfindingResult.Failed)
					{
						_Code = octreePathfindingResult.ResultCode;
						_PathfindingDuration = TimeSpan.Zero;
						return null;
					}
					list2.AddRange(octreePathfindingResult.Path);
					continue;
				}
				if (obstacle.Bounds.Contains(_Target))
				{
					OctreePathfindingResult octreePathfindingResult = obstacle.FindPath(list3.First(), _Target, tryRepositionStartToFreeLeaf, tryRepositionTargetToFreeLeaf, _CancellationTokenExternal, _CancellationTokenTimeout);
					if (octreePathfindingResult.Failed)
					{
						_Code = octreePathfindingResult.ResultCode;
						_PathfindingDuration = TimeSpan.Zero;
						return null;
					}
					list2.AddRange(octreePathfindingResult.Path);
					continue;
				}
				Vector3 min = obstacle.Bounds.min;
				Vector3 max = obstacle.Bounds.max;
				Debug.LogError($"Bounds: min:{{{min.x}, {min.y}, " + $"{min.z}}}, max:{{{max.x}, {max.y}, {max.z}}}" + "Ray: A: " + _Start.ToStringExt() + ", " + _Target.ToStringExt());
				_Code = PathfindingResultCode.UNKNOWN;
				_PathfindingDuration = TimeSpan.Zero;
				return null;
			}
			TimeSpan timeSpan = DateTime.Now - now;
			now = DateTime.Now;
			list2.Add(_Target);
			_Code = PathfindingResultCode.SUCCEEDED;
			_PathfindingDuration = timeSpan;
			return list2;
		}

		private static void PostprocessPath(List<List<Vector3>> _PathFragments, bool _Loop, bool _Smooth, int _PerMinBucketSmoothSamples, Action<string> _StatusCallback, Log _Log, out Vector3[] _PathOptimized, out Vector3[] _PathSmoothed, out int[] _TargetIndices, out TimeSpan _OptimizingDuration, out TimeSpan _SmoothingDuration)
		{
			List<Vector3> pathOptimized = new List<Vector3>(_PathFragments.Sum((List<Vector3> _Fragment) => _Fragment.Count));
			List<int> list = new List<int>(_PathFragments.Count + 1);
			DateTime now = DateTime.Now;
			List<Obstacle> obstaclesCrossingTheBounds = ObstacleManager.Instance.GetObstaclesCrossingTheBounds(ExtensionBounds.PointBounds(_PathFragments.SelectMany((List<Vector3> _Fragment) => _Fragment).ToArray()));
			_Log?.Write("Postprocessing fragments:");
			for (int num = 0; num < _PathFragments.Count; num++)
			{
				_Log?.Write($"{num + 1}/{_PathFragments.Count}");
				List<Vector3> list2 = _PathFragments[num];
				if (list2.Count > 2)
				{
					_StatusCallback($"Postprocessing : Optimizing fragment {num + 1}/{_PathFragments.Count} : Detailing");
					list2 = DetailPath(list2);
					_StatusCallback($"Postprocessing : Optimizing fragment {num + 1}/{_PathFragments.Count} : Pruning");
					list2 = ShortenPath(list2, obstaclesCrossingTheBounds);
					list2.Reverse();
					_StatusCallback($"Postprocessing : Optimizing fragment {num + 1}/{_PathFragments.Count} : Detailing");
					list2 = DetailPath(list2);
					_StatusCallback($"Postprocessing : Optimizing fragment {num + 1}/{_PathFragments.Count} : Pruning");
					list2 = ShortenPath(list2, obstaclesCrossingTheBounds);
					list2.Reverse();
				}
				if (list2.Count == 0)
				{
					continue;
				}
				if (pathOptimized.Count == 0)
				{
					pathOptimized.AddRange(list2);
					list.Add(0);
				}
				else
				{
					for (int num2 = ((pathOptimized[pathOptimized.Count - 1] == list2[0]) ? 1 : 0); num2 < list2.Count; num2++)
					{
						pathOptimized.Add(list2[num2]);
					}
				}
				list.Add(pathOptimized.Count - 1);
			}
			_TargetIndices = list.ToArray();
			TimeSpan timeSpan = DateTime.Now - now;
			_Log?.Write($"Postprocessing finished({timeSpan.TotalMilliseconds} ms.).");
			now = DateTime.Now;
			Vector3[] array;
			TimeSpan timeSpan2;
			if (_Smooth)
			{
				_Log?.Write("Path smoothing started.");
				_StatusCallback("Postprocessing : Smoothing");
				array = ((pathOptimized.Count > 2) ? Smooth(pathOptimized.ToArray(), _TargetIndices.Select((int index) => pathOptimized[index]).ToArray(), _Loop, _PerMinBucketSmoothSamples, out _TargetIndices) : pathOptimized.ToArray());
				timeSpan2 = DateTime.Now - now;
				_Log?.Write($"Path smoothing finished({timeSpan2.TotalMilliseconds} ms.).");
			}
			else
			{
				array = pathOptimized.ToArray();
				timeSpan2 = TimeSpan.Zero;
			}
			_PathOptimized = pathOptimized.ToArray();
			_PathSmoothed = array;
			_OptimizingDuration = timeSpan;
			_SmoothingDuration = timeSpan2;
		}

		private static Vector3[] Smooth(Vector3[] _SourcePath, Vector3[] _Targets, bool _Loop, int _PerMinBucketSmoothSamples, out int[] _TargetIndices)
		{
			Bounds bounds = ExtensionBounds.PointBounds(AddExtremePointsToCatmullRomArray(_SourcePath));
			List<Obstacle> obstaclesCrossingTheBounds = ObstacleManager.Instance.GetObstaclesCrossingTheBounds(bounds);
			return SmoothPath(UtilsCommon.TrimEqualPoints(_SourcePath), _Targets, _Loop, obstaclesCrossingTheBounds, _PerMinBucketSmoothSamples, out _TargetIndices);
		}

		private static Vector3[] AddExtremePointsToCatmullRomArray(Vector3[] _SourceArray)
		{
			if (_SourceArray.Length < 4)
			{
				return _SourceArray;
			}
			int num = _SourceArray.Length;
			List<Vector3> list = new List<Vector3>((num - 2) * 3 + 2);
			list.AddRange(_SourceArray);
			for (int i = 1; i < num - 2; i += 2)
			{
				Vector3 vector = _SourceArray[i];
				Vector3 vector2 = _SourceArray[i + 1];
				Vector3 vector3 = _SourceArray[i - 1];
				Vector3 vector4 = _SourceArray[i + 2];
				list.Add(vector + (vector2 - vector3));
				list.Add(vector + (vector - vector4));
				list.Add(vector2 + (vector2 - vector3));
				list.Add(vector2 + (vector - vector4));
			}
			return list.ToArray();
		}

		private static List<Vector3> DetailPath(List<Vector3> _Path)
		{
			float minBucketSize = ObstacleManager.Instance.MinBucketSize;
			float minBucketSizeSqr = ObstacleManager.Instance.MinBucketSizeSqr;
			int num = 1;
			for (int i = 0; i < _Path.Count - 1; i++)
			{
				float num2 = Vector3.Distance(_Path[i], _Path[i + 1]);
				num += Mathf.CeilToInt(num2 / minBucketSize);
			}
			List<Vector3> list = new List<Vector3>(num);
			Vector3 lhs = (_Path.Second() - _Path.First()).normalized;
			for (int j = 0; j < _Path.Count - 1; j++)
			{
				Vector3 vector = _Path[j];
				Vector3 vector2 = _Path[j + 1];
				list.Add(vector);
				Vector3 normalized = (vector2 - vector).normalized;
				if (Vector3.Dot(lhs, normalized) > 0.99619f)
				{
					lhs = normalized;
					continue;
				}
				Vector3 vector3 = normalized * minBucketSize;
				while ((vector2 - vector).sqrMagnitude > minBucketSizeSqr)
				{
					vector += vector3;
					list.Add(vector);
				}
			}
			list.Add(_Path.Last());
			return list;
		}

		private static List<Vector3> ShortenPath(List<Vector3> _Path, List<Obstacle> _InfluencingObstacles)
		{
			List<Vector3> list = new List<Vector3>(_Path);
			for (int i = 0; i < list.Count; i++)
			{
				Vector3 start = list[i];
				int num;
				for (num = 2; num < list.Count - i; num++)
				{
					Vector3 end = list[i + num];
					if (RayIntersectOccupiedLeaf(start, end, _InfluencingObstacles))
					{
						break;
					}
					list.RemoveAt(i + num - 1);
					num--;
				}
			}
			return list;
		}

		private static Vector3[] SmoothPath(Vector3[] _Path, Vector3[] _Targets, bool _Loop, List<Obstacle> _InfluencingObstacles, int _PerMinBucketSmoothSamples, out int[] _TargetIndices)
		{
			try
			{
				if (_Path.Length == 2)
				{
					_TargetIndices = new int[2] { 0, 1 };
					return _Path.ToArray();
				}
				if (ObstacleManager.Doomed)
				{
					_TargetIndices = null;
					return null;
				}
				float minBucketSize = ObstacleManager.Instance.MinBucketSize;
				float num = minBucketSize / (float)_PerMinBucketSmoothSamples;
				List<Vector3> list = new List<Vector3>(_Path);
				int num2 = 1;
				for (int i = 0; i < _Path.Length - 1; i++)
				{
					float num3 = Vector3.Distance(_Path[i], _Path[i + 1]);
					num2 += Mathf.CeilToInt(num3 / minBucketSize);
				}
				List<Vector3> list2 = new List<Vector3>(num2) { list[0] };
				if (_Loop)
				{
					list.Insert(0, list[list.Count - 2]);
					list.Add(list[2]);
				}
				else
				{
					list.Insert(0, list[0] + list[0] - list[1]);
					list.Add(list.Last() + (list.Last() - list[list.Count - 2]));
				}
				int num4 = 0;
				for (int j = 1; j <= list.Count - 3; j++)
				{
					list2.Add(list[j]);
					int num5 = (int)(Vector3.Distance(list[j], list[j + 1]) / num);
					if (num5 == 0)
					{
						continue;
					}
					float num6 = 1f / (float)num5;
					float num7 = 0f;
					Vector3 vector = list[j];
					int num8 = 0;
					for (int k = 0; k < num5 - 1; k++)
					{
						num7 += num6;
						Vector3 vector2 = CatmullRomEq(list[j - 1], list[j], list[j + 1], list[j + 2], num7);
						if (vector != vector2 && RayIntersectOccupiedLeaf(vector, vector2, _InfluencingObstacles))
						{
							if (j != list.Count - 3)
							{
								list.Insert(j + 2, list[j + 1] + (list[j + 2] - list[j + 1]) * 0.5f);
							}
							list.Insert(j + 1, list[j] + (list[j + 1] - list[j]) * 0.5f);
							for (int l = 0; l < k; l++)
							{
								list2.RemoveLast();
							}
							if (j != 1)
							{
								list.Insert(j, list[j - 1] + (list[j] - list[j - 1]) * 0.5f);
								j--;
								for (int m = 0; m < num4; m++)
								{
									list2.RemoveLast();
								}
							}
							num8 = 0;
							break;
						}
						list2.Add(vector2);
						num8++;
						vector = vector2;
					}
					num4 = num8;
				}
				list2.Add(_Path.Last());
				_TargetIndices = GetTargetIndices(list2.ToArray(), _Targets);
				return list2.ToArray();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				_TargetIndices = null;
				return null;
			}
		}

		private static int[] GetTargetIndices(Vector3[] _Path, Vector3[] _Targets)
		{
			if (_Path == null || _Path.Length == 0 || _Targets == null || _Targets.Length == 0)
			{
				return Array.Empty<int>();
			}
			int[] array = new int[_Targets.Length];
			int num = 0;
			for (int i = 0; i < _Targets.Length; i++)
			{
				Vector3 vector = _Targets[i];
				int num2 = -1;
				for (int j = num; j < _Path.Length; j++)
				{
					if (!(_Path[j] != vector))
					{
						num2 = j;
						break;
					}
				}
				if (num2 < 0)
				{
					float num3 = float.MaxValue;
					for (int k = num; k < _Path.Length; k++)
					{
						float sqrMagnitude = (_Path[k] - vector).sqrMagnitude;
						if (!(sqrMagnitude >= num3))
						{
							num3 = sqrMagnitude;
							num2 = k;
						}
					}
				}
				num = (array[i] = Mathf.Clamp(num2, num, _Path.Length - 1));
			}
			return array;
		}

		private static Vector3 CatmullRomEq(Vector3 _P0, Vector3 _P1, Vector3 _P2, Vector3 _P3, float _T)
		{
			return 0.5f * ((0f - _T) * (1f - _T) * (1f - _T) * _P0 + (2f - 5f * _T * _T + 3f * _T * _T * _T) * _P1 + _T * (1f + 4f * _T - 3f * _T * _T) * _P2 - _T * _T * (1f - _T) * _P3);
		}

		private static bool RayIntersectOccupiedLeaf(Vector3 _Start, Vector3 _End, List<Obstacle> _Obstacles)
		{
			Segment3 segment = ((_Start.LexCompare(_End) >= 0) ? new Segment3(_End, _Start) : new Segment3(_Start, _End));
			return _Obstacles.Any((Obstacle _Obstacle) => _Obstacle.SegmentIntersectOccupiedLeaf(segment));
		}
	}
}
