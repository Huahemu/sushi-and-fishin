using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.LocalAvoidance
{
	public class LPSolver
	{
		public abstract class Intersection
		{
			public static Intersection Create(SpatialShape _Shape, ILine _Line)
			{
				return _Shape.CheckIntersection(_Line) switch
				{
					IntersectionType.INTERSECTION => new IntersectionPointSet(_Shape, _Line), 
					IntersectionType.BELONGING => new IntersectionContinuous(_Shape, _Line), 
					_ => null, 
				};
			}

			public abstract Vector3? GetClosestFitPoint(Vector3 _Point, Constraint[] _Constraints, int _StartIndex, int _EndIndex);
		}

		private class IntersectionContinuous : Intersection
		{
			private ILine m_IntersectionOutline;

			public IntersectionContinuous(SpatialShape _Shape, ILine _Line)
			{
				m_IntersectionOutline = _Line;
			}

			public override Vector3? GetClosestFitPoint(Vector3 _Point, Constraint[] _Constraints, int _StartIndex, int _EndIndex)
			{
				Vector3 vector = m_IntersectionOutline.ClosestPoint(_Point);
				if (IsPointFitToRangeSet(_Constraints, _StartIndex, _EndIndex, vector))
				{
					return vector;
				}
				List<Vector3> intersectionPoints = new List<Vector3>();
				_Constraints.ForEach((Constraint _Constraint) =>
				{
					if (_Constraint.MainShape.CheckIntersection(m_IntersectionOutline) == IntersectionType.INTERSECTION)
					{
						intersectionPoints.AddRange(m_IntersectionOutline.Intersection(_Constraint.MainShape));
					}
				});
				return (from _FitPoint in intersectionPoints
					where IsPointFitToRangeSet(_Constraints, _StartIndex, _EndIndex, _FitPoint)
					orderby Vector3.SqrMagnitude(_FitPoint - _Point)
					select _FitPoint).FirstOrDefault();
			}
		}

		private class IntersectionPointSet : Intersection
		{
			private Vector3[] m_PointSet;

			public IntersectionPointSet(SpatialShape _Shape, ILine _Line)
			{
				m_PointSet = _Line.Intersection(_Shape);
			}

			public override Vector3? GetClosestFitPoint(Vector3 _Point, Constraint[] _Constraints, int _StartIndex, int _EndIndex)
			{
				return (from _FitPoint in m_PointSet
					where IsPointFitToRangeSet(_Constraints, _StartIndex, _EndIndex, _FitPoint)
					orderby Vector3.SqrMagnitude(_FitPoint - _Point)
					select _FitPoint).FirstOrDefault();
			}
		}

		public abstract class Constraint
		{
			public abstract SpatialShape MainShape { get; }

			public abstract bool IsPointFitTo(Vector3 _Point);

			public abstract Vector3 FindBestBoundaryValue(Vector3 _Point);

			public abstract SpatialShape Intersection(Constraint _Other);

			public Intersection Intersection(ILine _Line)
			{
				return LPSolver.Intersection.Create(MainShape, _Line);
			}

			public abstract bool IsIntersect(Constraint _Other);

			public abstract bool IsIntersect(ILine _MathLine);
		}

		protected class ConstraintPlanar : Constraint
		{
			private Nav3D.LocalAvoidance.SupportingMath.Plane m_Plane;

			public override SpatialShape MainShape => m_Plane;

			public Nav3D.LocalAvoidance.SupportingMath.Plane Plane => m_Plane;

			public ConstraintPlanar(Nav3D.LocalAvoidance.SupportingMath.Plane _ConstraintPlane)
			{
				m_Plane = _ConstraintPlane;
			}

			public override bool IsPointFitTo(Vector3 _Point)
			{
				return m_Plane.IsBelongsHalfPlane(_Point);
			}

			public override Vector3 FindBestBoundaryValue(Vector3 _Point)
			{
				return m_Plane.GetSurfaceClosestPoint(_Point).Point;
			}

			public override SpatialShape Intersection(Constraint _Other)
			{
				if (_Other is ConstraintPlanar constraintPlanar)
				{
					if (IsIntersect(constraintPlanar))
					{
						return m_Plane.Intersection(constraintPlanar.m_Plane);
					}
					Debug.LogError(m_Plane.Normal.ToString() + "   " + constraintPlanar.m_Plane.Normal);
				}
				if (_Other is ConstraintSpherical constraintSpherical && IsIntersect(constraintSpherical))
				{
					return new Circle(m_Plane, constraintSpherical.Sphere);
				}
				throw new NotImplementedException("[ConstraintLinear] Unknown intersection for type:" + _Other.GetType().FullName);
			}

			public override bool IsIntersect(Constraint _Other)
			{
				if (_Other is ConstraintPlanar constraintPlanar)
				{
					return !Nav3D.LocalAvoidance.SupportingMath.Plane.IsPlanesParallel(m_Plane, constraintPlanar.m_Plane);
				}
				if (_Other is ConstraintSpherical constraintSpherical)
				{
					return constraintSpherical.Sphere.IsIntersect(m_Plane);
				}
				throw new NotImplementedException("[ConstraintLinear] Unknown intersection for type:" + _Other.GetType().FullName);
			}

			public override bool IsIntersect(ILine _MathLine)
			{
				if (_MathLine is Circle circle)
				{
					return !Nav3D.LocalAvoidance.SupportingMath.Plane.IsPlanesParallel(Plane, circle.GeneratrixPlane);
				}
				if (_MathLine is Straight straight)
				{
					return !Mathf.Approximately(0f, Vector3.Dot(Plane.Normal, straight.Direction));
				}
				throw new NotImplementedException("[ConstraintLinear] Unknown intersection for type:" + _MathLine.GetType().FullName);
			}
		}

		protected class ConstraintSpherical : Constraint
		{
			private Sphere m_Sphere;

			public override SpatialShape MainShape => m_Sphere;

			public Sphere Sphere => m_Sphere;

			public ConstraintSpherical(Sphere _ConstraintSphere)
			{
				m_Sphere = _ConstraintSphere;
			}

			public override bool IsPointFitTo(Vector3 _Point)
			{
				return m_Sphere.IsPointInside(_Point);
			}

			public override Vector3 FindBestBoundaryValue(Vector3 _Point)
			{
				return m_Sphere.GetClosestPoint(_Point);
			}

			public override SpatialShape Intersection(Constraint _Other)
			{
				if (_Other is ConstraintPlanar constraintPlanar)
				{
					return new Circle(constraintPlanar.Plane, m_Sphere);
				}
				throw new NotImplementedException("[ConstraintLinear] Unknown intersection for type:" + _Other.GetType().FullName);
			}

			public override bool IsIntersect(Constraint _Other)
			{
				if (_Other is ConstraintPlanar constraintPlanar)
				{
					return m_Sphere.IsIntersect(constraintPlanar.Plane);
				}
				if (_Other is ConstraintSpherical constraintSpherical)
				{
					return m_Sphere.IsIntersect(constraintSpherical.Sphere);
				}
				throw new NotImplementedException("[ConstraintSpherical] Unknown intersection for type:" + _Other.GetType().FullName);
			}

			public override bool IsIntersect(ILine _MathLine)
			{
				if (_MathLine is Straight straight)
				{
					return (straight.GetClosestPoint(Sphere.Center) - Sphere.Center).sqrMagnitude <= Sphere.SqrRadius;
				}
				if (_MathLine is Circle other)
				{
					return Sphere.IsIntersect(other);
				}
				throw new NotImplementedException("[ConstraintLinear] Unknown intersection for type:" + _MathLine.GetType().FullName);
			}
		}

		private static LPSolver m_Instance;

		public static LPSolver Instance => m_Instance ?? (m_Instance = new LPSolver());

		public Vector3 SolveMax(List<Nav3D.LocalAvoidance.SupportingMath.Plane> _PlaneConstraints, Sphere _SphereConstraint, Vector3 _Goal)
		{
			List<Constraint> list = new List<Constraint>(_PlaneConstraints.Count + 1);
			List<ConstraintPlanar> list2 = new List<ConstraintPlanar>(_PlaneConstraints.Count);
			foreach (Nav3D.LocalAvoidance.SupportingMath.Plane _PlaneConstraint in _PlaneConstraints)
			{
				ConstraintPlanar item = new ConstraintPlanar(_PlaneConstraint);
				list.Add(item);
				list2.Add(item);
			}
			ConstraintSpherical constraintSpherical = new ConstraintSpherical(_SphereConstraint);
			if (_SphereConstraint != null)
			{
				list.Add(constraintSpherical);
			}
			Vector3? vector = SolveMax(list.ToArray(), _Goal);
			if (vector.HasValue)
			{
				return GetPrimarySolution(vector.Value);
			}
			return GetExtendedSolution(constraintSpherical, list2.ToArray(), _Goal);
		}

		protected virtual Vector3 GetPrimarySolution(Vector3 _PrimarySolution)
		{
			return _PrimarySolution;
		}

		protected virtual Vector3 GetExtendedSolution(ConstraintSpherical _ConstraintSpherical, ConstraintPlanar[] _PlanarConstraints, Vector3 _GoalValue)
		{
			return SolveExtendedProblem(_ConstraintSpherical, _PlanarConstraints, _GoalValue);
		}

		public static bool IsPointFit(Nav3D.LocalAvoidance.SupportingMath.Plane[] _PlaneConstraints, Sphere _SphereConstraint, Vector3 _Point)
		{
			List<Constraint> list = ((IEnumerable<Nav3D.LocalAvoidance.SupportingMath.Plane>)_PlaneConstraints).Select((Func<Nav3D.LocalAvoidance.SupportingMath.Plane, Constraint>)((Nav3D.LocalAvoidance.SupportingMath.Plane _Plane) => new ConstraintPlanar(_Plane))).ToList();
			if (_SphereConstraint != null)
			{
				list.Add(new ConstraintSpherical(_SphereConstraint));
			}
			return IsPointFitToRangeSet(list.ToArray(), 0, list.Count - 1, _Point);
		}

		private static Vector3? SolveMax(Constraint[] _Constraints, Vector3 _GoalValue)
		{
			Vector3 vector = _GoalValue;
			Constraint constraint = _Constraints[0];
			if (!constraint.IsPointFitTo(vector))
			{
				vector = constraint.FindBestBoundaryValue(_GoalValue);
			}
			for (int i = 1; i < _Constraints.Length; i++)
			{
				Constraint constraint2 = _Constraints[i];
				if (constraint2.IsPointFitTo(vector))
				{
					continue;
				}
				Vector3 vector2 = constraint2.FindBestBoundaryValue(_GoalValue);
				if (IsPointFitToRangeSet(_Constraints, 0, i - 1, vector2))
				{
					vector = vector2;
					continue;
				}
				Vector3? vector3 = SolveForSubset(_Constraints, 0, i - 1, constraint2, _GoalValue);
				if (vector3.HasValue)
				{
					vector = vector3.Value;
					continue;
				}
				return null;
			}
			if (!IsPointFitToRangeSet(_Constraints, 0, _Constraints.Length - 1, vector))
			{
				return null;
			}
			return vector;
		}

		private static (ConstraintPlanar Constraint, float Distance)[] ConstructDistanceSortedTupleArray(ConstraintPlanar[] _PlanarConstraints, ConstraintSpherical _SphericalConstraint)
		{
			return (from _Constraint in _PlanarConstraints
				select (Constraint: _Constraint, Distance: _Constraint.Plane.DistanceToPoint(_SphericalConstraint.Sphere.Center)) into _Tuple
				orderby _Tuple.Distance
				select _Tuple).ToArray();
		}

		private Vector3 SolveExtendedProblem(ConstraintSpherical _SphericalConstraint, ConstraintPlanar[] _PlanarConstraints, Vector3 _GoalValue)
		{
			float radius = _SphericalConstraint.Sphere.Radius;
			(ConstraintPlanar, float)[] source = ConstructDistanceSortedTupleArray(_PlanarConstraints, _SphericalConstraint);
			float item = source.First().Item2;
			List<Constraint> list = new List<Constraint>();
			list.AddRange(_PlanarConstraints);
			list.Add(_SphericalConstraint);
			Constraint[] constraints = list.ToArray();
			Vector3? vector;
			if (item < 0f - radius)
			{
				float delta = item + radius;
				_PlanarConstraints.ForEach((ConstraintPlanar _PlanarConstraint) =>
				{
					_PlanarConstraint.Plane.Translate(_PlanarConstraint.Plane.Normal * (delta - 0.0001f));
				});
				vector = SolveMax(constraints, _GoalValue);
				if (vector.HasValue)
				{
					return vector.Value;
				}
			}
			do
			{
				source = ConstructDistanceSortedTupleArray(_PlanarConstraints, _SphericalConstraint);
				IEnumerable<(ConstraintPlanar, float)> source2 = source.Where(((ConstraintPlanar Constraint, float Distance) _Tuple) => _Tuple.Distance < 0f);
				float maxNegativeDistance = (source2.Any() ? (source2.Max(((ConstraintPlanar Constraint, float Distance) _TupleFiltered) => _TupleFiltered.Distance) - 0.0001f) : (0f - source.Min(((ConstraintPlanar Constraint, float Distance) _Tuple) => _Tuple.Distance)));
				_PlanarConstraints.ForEach((ConstraintPlanar _PlanarConstraint) =>
				{
					_PlanarConstraint.Plane.Translate(_PlanarConstraint.Plane.Normal * maxNegativeDistance);
				});
				vector = SolveMax(constraints, _GoalValue);
			}
			while (!vector.HasValue);
			return vector.Value;
		}

		private static Vector3? SolveForSubset(Constraint[] _Constraints, int _StartIndex, int _EndIndex, Constraint _SolverConstraint, Vector3 _GoalValue)
		{
			float num = float.PositiveInfinity;
			Vector3 value = _GoalValue;
			bool flag = false;
			for (int i = _StartIndex; i <= _EndIndex; i++)
			{
				Constraint other = _Constraints[i];
				if (!_SolverConstraint.IsIntersect(other))
				{
					continue;
				}
				SpatialShape spatialShape = _SolverConstraint.Intersection(other);
				Vector3 closestPoint = spatialShape.GetClosestPoint(_GoalValue);
				if (IsPointFitToRangeSet(_Constraints, _StartIndex, _EndIndex, closestPoint))
				{
					flag = true;
					float sqrMagnitude = (_GoalValue - closestPoint).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						value = closestPoint;
					}
				}
				else
				{
					if (!(spatialShape is ILine intersectionLine))
					{
						continue;
					}
					Vector3? bestPointForIntersectionLine = GetBestPointForIntersectionLine(intersectionLine, _Constraints, 0, _Constraints.Length - 1, _GoalValue);
					if (bestPointForIntersectionLine.HasValue)
					{
						flag = true;
						float sqrMagnitude2 = (_GoalValue - bestPointForIntersectionLine.Value).sqrMagnitude;
						if (sqrMagnitude2 < num)
						{
							num = sqrMagnitude2;
							value = bestPointForIntersectionLine.Value;
						}
					}
				}
			}
			if (!flag)
			{
				return null;
			}
			return value;
		}

		private static Vector3? GetBestPointForIntersectionLine(ILine _IntersectionLine, Constraint[] _Constraints, int _StartIndex, int _EndIndex, Vector3 _Goal)
		{
			float num = float.PositiveInfinity;
			Vector3? result = null;
			for (int i = _StartIndex; i <= _EndIndex; i++)
			{
				Constraint constraint = _Constraints[i];
				if (!constraint.IsIntersect(_IntersectionLine))
				{
					continue;
				}
				Intersection intersection = Intersection.Create(constraint.MainShape, _IntersectionLine);
				if (intersection == null)
				{
					continue;
				}
				Vector3? closestFitPoint = intersection.GetClosestFitPoint(_Goal, _Constraints, 0, _Constraints.Length - 1);
				if (closestFitPoint.HasValue)
				{
					float sqrMagnitude = (_Goal - closestFitPoint.Value).sqrMagnitude;
					if (sqrMagnitude <= num)
					{
						num = sqrMagnitude;
						result = closestFitPoint.Value;
					}
				}
			}
			return result;
		}

		private static bool IsPointFitToRangeSet(Constraint[] _Constraints, int _StartIndex, int _EndIndex, Vector3 _Point)
		{
			for (int i = _StartIndex; i <= _EndIndex; i++)
			{
				if (!_Constraints[i].IsPointFitTo(_Point))
				{
					return false;
				}
			}
			return true;
		}
	}
}
