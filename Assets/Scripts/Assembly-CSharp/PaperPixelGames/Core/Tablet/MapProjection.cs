using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapProjection
	{
		private const int CoefficientCount = 8;

		private const float PivotEpsilon = 1E-08f;

		private readonly double[] _planarCoefficients = new double[8];

		private readonly double[] _tiltedCoefficients = new double[8];

		private readonly double[] _inversePlanarCoefficients = new double[8];

		public bool IsValid { get; private set; }

		public bool CanMapToWorldXZ { get; private set; }

		public bool UsesElevation { get; private set; }

		public Vector3 TiltedViewDirection { get; private set; }

		public string FailureReason { get; private set; }

		public float RootMeanSquareErrorPixels { get; private set; }

		public float MaximumErrorPixels { get; private set; }

		public bool FitWorldRect(Rect worldXZ)
		{
			IsValid = worldXZ.width > 0f && worldXZ.height > 0f;
			CanMapToWorldXZ = IsValid;
			UsesElevation = false;
			TiltedViewDirection = Vector3.down;
			FailureReason = IsValid ? string.Empty : "Live map bounds are empty.";
			RootMeanSquareErrorPixels = MaximumErrorPixels = 0f;
			Array.Clear(_planarCoefficients, 0, 8);
			Array.Clear(_inversePlanarCoefficients, 0, 8);
			if (!IsValid) return false;
			_planarCoefficients[0] = 1.0 / worldXZ.width;
			_planarCoefficients[2] = -worldXZ.xMin / worldXZ.width;
			_planarCoefficients[4] = 1.0 / worldXZ.height;
			_planarCoefficients[5] = -worldXZ.yMin / worldXZ.height;
			_inversePlanarCoefficients[0] = worldXZ.width;
			_inversePlanarCoefficients[2] = worldXZ.xMin;
			_inversePlanarCoefficients[4] = worldXZ.height;
			_inversePlanarCoefficients[5] = worldXZ.yMin;
			return true;
		}

		public bool Fit(MapDefinitionSO definition)
		{
			IsValid = false;
			CanMapToWorldXZ = false;
			UsesElevation = false;
			TiltedViewDirection = Vector3.zero;
			FailureReason = string.Empty;
			RootMeanSquareErrorPixels = 0f;
			MaximumErrorPixels = 0f;
			if (definition == null)
			{
				FailureReason = "Map definition is null.";
				return false;
			}
			CanMapToWorldXZ = TryFitMapToWorldXZ(definition);
			if (definition.ProjectionMode == MapProjectionMode.TiltedOrthographic)
			{
				if (TryFitTiltedOrthographic(definition) && TryResolveTiltedViewDirection(definition, out var viewDirection))
				{
					IsValid = true;
					UsesElevation = true;
					TiltedViewDirection = viewDirection;
					CalculateErrorMetrics(definition);
					return true;
				}
				if (string.IsNullOrEmpty(FailureReason))
				{
					FailureReason = "The tilted map does not define a valid downward view direction.";
				}
				if (definition.TiltedViewDirection.sqrMagnitude > 1E-16f)
				{
					return false;
				}
			}
			if (!TryFitPlanarProjective(definition))
			{
				FailureReason = "The planar projective anchor solve is degenerate.";
				return false;
			}
			IsValid = true;
			CalculateErrorMetrics(definition);
			return true;
		}

		private bool TryFitPlanarProjective(MapDefinitionSO definition)
		{
			IReadOnlyList<MapCalibrationAnchor> calibrationAnchors = definition.CalibrationAnchors;
			if (calibrationAnchors == null)
			{
				FailureReason = "The tilted map has no calibration anchor list.";
				return false;
			}
			double[,] array = new double[8, 8];
			double[] array2 = new double[8];
			int num = 0;
			for (int i = 0; i < calibrationAnchors.Count; i++)
			{
				MapCalibrationAnchor mapCalibrationAnchor = calibrationAnchors[i];
				if (mapCalibrationAnchor != null && mapCalibrationAnchor.Enabled)
				{
					double num2 = mapCalibrationAnchor.WorldPosition.x;
					double num3 = mapCalibrationAnchor.WorldPosition.z;
					double num4 = mapCalibrationAnchor.MapUV.x;
					double num5 = mapCalibrationAnchor.MapUV.y;
					double[] row = new double[8]
					{
						num2,
						num3,
						1.0,
						0.0,
						0.0,
						0.0,
						(0.0 - num4) * num2,
						(0.0 - num4) * num3
					};
					double[] row2 = new double[8]
					{
						0.0,
						0.0,
						0.0,
						num2,
						num3,
						1.0,
						(0.0 - num5) * num2,
						(0.0 - num5) * num3
					};
					AccumulateNormalEquation(array, array2, row, num4);
					AccumulateNormalEquation(array, array2, row2, num5);
					num++;
				}
			}
			if (num >= 4)
			{
				return TrySolve(array, array2, _planarCoefficients);
			}
			return false;
		}

		private bool TryFitMapToWorldXZ(MapDefinitionSO definition)
		{
			IReadOnlyList<MapCalibrationAnchor> calibrationAnchors = definition.CalibrationAnchors;
			if (calibrationAnchors == null)
			{
				return false;
			}
			double[,] array = new double[8, 8];
			double[] array2 = new double[8];
			int num = 0;
			for (int i = 0; i < calibrationAnchors.Count; i++)
			{
				MapCalibrationAnchor mapCalibrationAnchor = calibrationAnchors[i];
				if (mapCalibrationAnchor != null && mapCalibrationAnchor.Enabled)
				{
					double num2 = mapCalibrationAnchor.MapUV.x;
					double num3 = mapCalibrationAnchor.MapUV.y;
					double num4 = mapCalibrationAnchor.WorldPosition.x;
					double num5 = mapCalibrationAnchor.WorldPosition.z;
					double[] row = new double[8]
					{
						num2,
						num3,
						1.0,
						0.0,
						0.0,
						0.0,
						(0.0 - num4) * num2,
						(0.0 - num4) * num3
					};
					double[] row2 = new double[8]
					{
						0.0,
						0.0,
						0.0,
						num2,
						num3,
						1.0,
						(0.0 - num5) * num2,
						(0.0 - num5) * num3
					};
					AccumulateNormalEquation(array, array2, row, num4);
					AccumulateNormalEquation(array, array2, row2, num5);
					num++;
				}
			}
			if (num >= 4)
			{
				return TrySolve(array, array2, _inversePlanarCoefficients);
			}
			return false;
		}

		private bool TryFitTiltedOrthographic(MapDefinitionSO definition)
		{
			Vector3 tiltedViewDirection = definition.TiltedViewDirection;
			if (tiltedViewDirection.sqrMagnitude > 1E-16f)
			{
				return TryFitConstrainedTiltedOrthographic(definition, tiltedViewDirection);
			}
			IReadOnlyList<MapCalibrationAnchor> calibrationAnchors = definition.CalibrationAnchors;
			if (calibrationAnchors == null)
			{
				return false;
			}
			double[,] array = new double[4, 4];
			double[] array2 = new double[4];
			double[] array3 = new double[4];
			int num = 0;
			for (int i = 0; i < calibrationAnchors.Count; i++)
			{
				MapCalibrationAnchor mapCalibrationAnchor = calibrationAnchors[i];
				if (mapCalibrationAnchor == null || !mapCalibrationAnchor.Enabled)
				{
					continue;
				}
				double[] array4 = new double[4]
				{
					mapCalibrationAnchor.WorldPosition.x,
					mapCalibrationAnchor.WorldPosition.y,
					mapCalibrationAnchor.WorldPosition.z,
					1.0
				};
				for (int j = 0; j < 4; j++)
				{
					array2[j] += array4[j] * (double)mapCalibrationAnchor.MapUV.x;
					array3[j] += array4[j] * (double)mapCalibrationAnchor.MapUV.y;
					for (int k = 0; k < 4; k++)
					{
						array[j, k] += array4[j] * array4[k];
					}
				}
				num++;
			}
			if (num < 4 || !TrySolve(array, array2, _tiltedCoefficients, 4) || !TrySolve(array, array3, _tiltedCoefficients, 4, 4))
			{
				return false;
			}
			return true;
		}

		private bool TryFitConstrainedTiltedOrthographic(MapDefinitionSO definition, Vector3 configuredViewDirection)
		{
			IReadOnlyList<MapCalibrationAnchor> calibrationAnchors = definition.CalibrationAnchors;
			if (calibrationAnchors == null)
			{
				return false;
			}
			Vector3 vector = configuredViewDirection.normalized;
			if (vector.y > 0f)
			{
				vector = -vector;
			}
			Vector3 vector2 = Vector3.Cross(Vector3.up, vector);
			if (vector2.sqrMagnitude <= 1E-16f)
			{
				vector2 = Vector3.right;
			}
			else
			{
				vector2.Normalize();
			}
			Vector3 normalized = Vector3.Cross(vector, vector2).normalized;
			int num = 0;
			double num2 = 0.0;
			double num3 = 0.0;
			for (int i = 0; i < calibrationAnchors.Count; i++)
			{
				MapCalibrationAnchor mapCalibrationAnchor = calibrationAnchors[i];
				if (mapCalibrationAnchor != null && mapCalibrationAnchor.Enabled)
				{
					double num4 = Vector3.Dot(vector2, mapCalibrationAnchor.WorldPosition);
					double num5 = Vector3.Dot(normalized, mapCalibrationAnchor.WorldPosition);
					if (!IsFinite(num4) || !IsFinite(num5) || !IsFinite(mapCalibrationAnchor.MapUV.x) || !IsFinite(mapCalibrationAnchor.MapUV.y))
					{
						FailureReason = "Anchor '" + mapCalibrationAnchor.Name + "' contains a non-finite world or map value.";
						return false;
					}
					num2 += num4;
					num3 += num5;
					num++;
				}
			}
			if (num < 3)
			{
				FailureReason = $"The constrained tilted solve requires at least {3} enabled anchors.";
				return false;
			}
			num2 /= (double)num;
			num3 /= (double)num;
			double num6 = 0.0;
			double num7 = 0.0;
			for (int j = 0; j < calibrationAnchors.Count; j++)
			{
				MapCalibrationAnchor mapCalibrationAnchor2 = calibrationAnchors[j];
				if (mapCalibrationAnchor2 != null && mapCalibrationAnchor2.Enabled)
				{
					double num8 = Vector3.Dot(vector2, mapCalibrationAnchor2.WorldPosition);
					double num9 = Vector3.Dot(normalized, mapCalibrationAnchor2.WorldPosition);
					num6 = Math.Max(num6, Math.Abs(num8 - num2));
					num7 = Math.Max(num7, Math.Abs(num9 - num3));
				}
			}
			if (num6 < 9.99999993922529E-09 || num7 < 9.99999993922529E-09)
			{
				FailureReason = $"Anchor world positions are degenerate in the configured camera plane. Horizontal spread: {num6:G6}, vertical spread: {num7:G6}.";
				return false;
			}
			double[,] array = new double[3, 3];
			double[] array2 = new double[3];
			double[] array3 = new double[3];
			for (int k = 0; k < calibrationAnchors.Count; k++)
			{
				MapCalibrationAnchor mapCalibrationAnchor3 = calibrationAnchors[k];
				if (mapCalibrationAnchor3 == null || !mapCalibrationAnchor3.Enabled)
				{
					continue;
				}
				double[] array4 = new double[3]
				{
					((double)Vector3.Dot(vector2, mapCalibrationAnchor3.WorldPosition) - num2) / num6,
					((double)Vector3.Dot(normalized, mapCalibrationAnchor3.WorldPosition) - num3) / num7,
					1.0
				};
				for (int l = 0; l < 3; l++)
				{
					array2[l] += array4[l] * (double)mapCalibrationAnchor3.MapUV.x;
					array3[l] += array4[l] * (double)mapCalibrationAnchor3.MapUV.y;
					for (int m = 0; m < 3; m++)
					{
						array[l, m] += array4[l] * array4[m];
					}
				}
			}
			double[] array5 = new double[3];
			double[] array6 = new double[3];
			if (num < 3 || !TrySolve(array, array2, array5, 3) || !TrySolve(array, array3, array6, 3))
			{
				FailureReason = "The normalized constrained tilted linear solve is singular.";
				return false;
			}
			WriteConstrainedCoefficients(_tiltedCoefficients, 0, vector2, normalized, array5, num2, num3, num6, num7);
			WriteConstrainedCoefficients(_tiltedCoefficients, 4, vector2, normalized, array6, num2, num3, num6, num7);
			return true;
		}

		private static void WriteConstrainedCoefficients(double[] destination, int destinationOffset, Vector3 horizontalBasis, Vector3 verticalBasis, double[] fittedCoefficients, double horizontalMean, double verticalMean, double horizontalScale, double verticalScale)
		{
			double num = fittedCoefficients[0] / horizontalScale;
			double num2 = fittedCoefficients[1] / verticalScale;
			destination[destinationOffset] = num * (double)horizontalBasis.x + num2 * (double)verticalBasis.x;
			destination[destinationOffset + 1] = num * (double)horizontalBasis.y + num2 * (double)verticalBasis.y;
			destination[destinationOffset + 2] = num * (double)horizontalBasis.z + num2 * (double)verticalBasis.z;
			destination[destinationOffset + 3] = fittedCoefficients[2] - num * horizontalMean - num2 * verticalMean;
		}

		private static bool IsFinite(double value)
		{
			if (!double.IsNaN(value))
			{
				return !double.IsInfinity(value);
			}
			return false;
		}

		public bool TryWorldToMapUV(Vector3 worldPosition, out Vector2 mapUV)
		{
			mapUV = default;
			if (!IsValid)
			{
				return false;
			}
			if (UsesElevation)
			{
				mapUV = new Vector2((float)(_tiltedCoefficients[0] * (double)worldPosition.x + _tiltedCoefficients[1] * (double)worldPosition.y + _tiltedCoefficients[2] * (double)worldPosition.z + _tiltedCoefficients[3]), (float)(_tiltedCoefficients[4] * (double)worldPosition.x + _tiltedCoefficients[5] * (double)worldPosition.y + _tiltedCoefficients[6] * (double)worldPosition.z + _tiltedCoefficients[7]));
				if (!float.IsNaN(mapUV.x) && !float.IsInfinity(mapUV.x) && !float.IsNaN(mapUV.y))
				{
					return !float.IsInfinity(mapUV.y);
				}
				return false;
			}
			double num = worldPosition.x;
			double num2 = worldPosition.z;
			double num3 = _planarCoefficients[6] * num + _planarCoefficients[7] * num2 + 1.0;
			if (Math.Abs(num3) < 9.99999993922529E-09)
			{
				return false;
			}
			double num4 = (_planarCoefficients[0] * num + _planarCoefficients[1] * num2 + _planarCoefficients[2]) / num3;
			double num5 = (_planarCoefficients[3] * num + _planarCoefficients[4] * num2 + _planarCoefficients[5]) / num3;
			if (double.IsNaN(num4) || double.IsInfinity(num4) || double.IsNaN(num5) || double.IsInfinity(num5))
			{
				return false;
			}
			mapUV = new Vector2((float)num4, (float)num5);
			return true;
		}

		public bool TryMapUVToWorldXZ(Vector2 mapUV, out Vector2 worldXZ)
		{
			worldXZ = default;
			if (!CanMapToWorldXZ)
			{
				return false;
			}
			double num = mapUV.x;
			double num2 = mapUV.y;
			double num3 = _inversePlanarCoefficients[6] * num + _inversePlanarCoefficients[7] * num2 + 1.0;
			if (Math.Abs(num3) < 9.99999993922529E-09)
			{
				return false;
			}
			double num4 = (_inversePlanarCoefficients[0] * num + _inversePlanarCoefficients[1] * num2 + _inversePlanarCoefficients[2]) / num3;
			double num5 = (_inversePlanarCoefficients[3] * num + _inversePlanarCoefficients[4] * num2 + _inversePlanarCoefficients[5]) / num3;
			if (double.IsNaN(num4) || double.IsInfinity(num4) || double.IsNaN(num5) || double.IsInfinity(num5))
			{
				return false;
			}
			worldXZ = new Vector2((float)num4, (float)num5);
			return true;
		}

		public bool TryMapUVToWorldXZAtY(Vector2 mapUV, float worldY, out Vector2 worldXZ)
		{
			worldXZ = default;
			if (!IsValid || !UsesElevation || float.IsNaN(worldY) || float.IsInfinity(worldY))
			{
				return false;
			}
			double num = _tiltedCoefficients[0];
			double num2 = _tiltedCoefficients[2];
			double num3 = _tiltedCoefficients[4];
			double num4 = _tiltedCoefficients[6];
			double num5 = num * num4 - num2 * num3;
			if (Math.Abs(num5) < 9.99999993922529E-09)
			{
				return false;
			}
			double num6 = (double)mapUV.x - _tiltedCoefficients[1] * (double)worldY - _tiltedCoefficients[3];
			double num7 = (double)mapUV.y - _tiltedCoefficients[5] * (double)worldY - _tiltedCoefficients[7];
			double num8 = (num6 * num4 - num2 * num7) / num5;
			double num9 = (num * num7 - num6 * num3) / num5;
			if (double.IsNaN(num8) || double.IsInfinity(num8) || double.IsNaN(num9) || double.IsInfinity(num9))
			{
				return false;
			}
			worldXZ = new Vector2((float)num8, (float)num9);
			return true;
		}

		public bool TryCreateTiltedOrthographicRay(Vector2 mapUV, float rayStartY, out Ray ray)
		{
			ray = default;
			if (!UsesElevation || !TryMapUVToWorldXZAtY(mapUV, rayStartY, out var worldXZ))
			{
				return false;
			}
			if (Mathf.Abs(TiltedViewDirection.y) < 1E-08f)
			{
				return false;
			}
			ray = new Ray(new Vector3(worldXZ.x, rayStartY, worldXZ.y), TiltedViewDirection);
			return true;
		}

		private bool TryCalculateTiltedViewDirection(out Vector3 viewDirection)
		{
			Vector3 lhs = new Vector3((float)_tiltedCoefficients[0], (float)_tiltedCoefficients[1], (float)_tiltedCoefficients[2]);
			Vector3 rhs = new Vector3((float)_tiltedCoefficients[4], (float)_tiltedCoefficients[5], (float)_tiltedCoefficients[6]);
			viewDirection = Vector3.Cross(lhs, rhs);
			if (viewDirection.sqrMagnitude < 1E-16f)
			{
				return false;
			}
			viewDirection.Normalize();
			if (viewDirection.y > 0f)
			{
				viewDirection = -viewDirection;
			}
			return Mathf.Abs(viewDirection.y) >= 1E-08f;
		}

		private bool TryResolveTiltedViewDirection(MapDefinitionSO definition, out Vector3 viewDirection)
		{
			viewDirection = definition.TiltedViewDirection;
			if (viewDirection.sqrMagnitude <= 1E-16f)
			{
				return TryCalculateTiltedViewDirection(out viewDirection);
			}
			if (!IsFinite(viewDirection.x) || !IsFinite(viewDirection.y) || !IsFinite(viewDirection.z))
			{
				FailureReason = "The configured tilted view direction contains a non-finite value.";
				return false;
			}
			viewDirection.Normalize();
			if (viewDirection.y > 0f)
			{
				viewDirection = -viewDirection;
			}
			if (Mathf.Abs(viewDirection.y) < 1E-08f)
			{
				FailureReason = "The configured tilted view direction is horizontal and cannot cast toward ground.";
				return false;
			}
			return true;
		}

		public float GetAnchorErrorPixels(MapDefinitionSO definition, MapCalibrationAnchor anchor)
		{
			if (definition == null || anchor == null || !TryWorldToMapUV(anchor.WorldPosition, out var mapUV))
			{
				return 0f;
			}
			return Vector2.Scale(b: new Vector2(Mathf.Max(1f, Mathf.Abs(definition.ReferenceResolution.x)), Mathf.Max(1f, Mathf.Abs(definition.ReferenceResolution.y))), a: mapUV - anchor.MapUV).magnitude;
		}

		private void CalculateErrorMetrics(MapDefinitionSO definition)
		{
			IReadOnlyList<MapCalibrationAnchor> calibrationAnchors = definition.CalibrationAnchors;
			float num = 0f;
			float num2 = 0f;
			int num3 = 0;
			for (int i = 0; i < calibrationAnchors.Count; i++)
			{
				MapCalibrationAnchor mapCalibrationAnchor = calibrationAnchors[i];
				if (mapCalibrationAnchor != null && mapCalibrationAnchor.Enabled)
				{
					float anchorErrorPixels = GetAnchorErrorPixels(definition, mapCalibrationAnchor);
					num += anchorErrorPixels * anchorErrorPixels;
					num2 = Mathf.Max(num2, anchorErrorPixels);
					num3++;
				}
			}
			RootMeanSquareErrorPixels = ((num3 > 0) ? Mathf.Sqrt(num / (float)num3) : 0f);
			MaximumErrorPixels = num2;
		}

		private static void AccumulateNormalEquation(double[,] matrix, double[] target, double[] row, double value)
		{
			for (int i = 0; i < 8; i++)
			{
				target[i] += row[i] * value;
				for (int j = 0; j < 8; j++)
				{
					matrix[i, j] += row[i] * row[j];
				}
			}
		}

		private static bool TrySolve(double[,] coefficients, double[] values, double[] result, int coefficientCount = 8, int resultOffset = 0)
		{
			double[,] array = new double[coefficientCount, coefficientCount + 1];
			for (int i = 0; i < coefficientCount; i++)
			{
				for (int j = 0; j < coefficientCount; j++)
				{
					array[i, j] = coefficients[i, j];
				}
				array[i, coefficientCount] = values[i];
			}
			for (int k = 0; k < coefficientCount; k++)
			{
				int num = k;
				double num2 = Math.Abs(array[k, k]);
				for (int l = k + 1; l < coefficientCount; l++)
				{
					double num3 = Math.Abs(array[l, k]);
					if (num3 > num2)
					{
						num2 = num3;
						num = l;
					}
				}
				if (num2 < 9.99999993922529E-09)
				{
					return false;
				}
				if (num != k)
				{
					for (int m = k; m <= coefficientCount; m++)
					{
						double num4 = array[k, m];
						array[k, m] = array[num, m];
						array[num, m] = num4;
					}
				}
				double num5 = array[k, k];
				for (int n = k; n <= coefficientCount; n++)
				{
					array[k, n] /= num5;
				}
				for (int num6 = 0; num6 < coefficientCount; num6++)
				{
					if (num6 == k)
					{
						continue;
					}
					double num7 = array[num6, k];
					if (!(Math.Abs(num7) < 9.99999993922529E-09))
					{
						for (int num8 = k; num8 <= coefficientCount; num8++)
						{
							array[num6, num8] -= num7 * array[k, num8];
						}
					}
				}
			}
			for (int num9 = 0; num9 < coefficientCount; num9++)
			{
				result[resultOffset + num9] = array[num9, coefficientCount];
			}
			return true;
		}
	}
}
