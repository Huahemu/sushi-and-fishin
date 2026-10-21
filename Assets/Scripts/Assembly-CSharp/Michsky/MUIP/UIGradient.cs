using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[DisallowMultipleComponent]
	[AddComponentMenu("Modern UI Pack/Effects/UI Gradient")]
	public class UIGradient : BaseMeshEffect
	{
		public enum Type
		{
			Horizontal = 0,
			Vertical = 1,
			Diamond = 2
		}

		public enum Blend
		{
			Override = 0,
			Add = 1,
			Multiply = 2
		}

		[SerializeField]
		private Type _gradientType;

		[SerializeField]
		private Blend _blendMode = Blend.Multiply;

		[SerializeField]
		private bool _modifyVertices = true;

		[SerializeField]
		[Range(-1f, 1f)]
		private float _offset;

		[SerializeField]
		[Range(0.1f, 10f)]
		private float _zoom = 1f;

		[SerializeField]
		private Gradient _effectGradient = new Gradient
		{
			colorKeys = new GradientColorKey[2]
			{
				new GradientColorKey(Color.black, 0f),
				new GradientColorKey(Color.white, 1f)
			}
		};

		public Blend BlendMode
		{
			get
			{
				return _blendMode;
			}
			set
			{
				_blendMode = value;
				base.graphic.SetVerticesDirty();
			}
		}

		public Gradient EffectGradient
		{
			get
			{
				return _effectGradient;
			}
			set
			{
				_effectGradient = value;
				base.graphic.SetVerticesDirty();
			}
		}

		public Type GradientType
		{
			get
			{
				return _gradientType;
			}
			set
			{
				_gradientType = value;
				base.graphic.SetVerticesDirty();
			}
		}

		public bool ModifyVertices
		{
			get
			{
				return _modifyVertices;
			}
			set
			{
				_modifyVertices = value;
				base.graphic.SetVerticesDirty();
			}
		}

		public float Offset
		{
			get
			{
				return _offset;
			}
			set
			{
				_offset = Mathf.Clamp(value, -1f, 1f);
				base.graphic.SetVerticesDirty();
			}
		}

		public float Zoom
		{
			get
			{
				return _zoom;
			}
			set
			{
				_zoom = Mathf.Clamp(value, 0.1f, 10f);
				base.graphic.SetVerticesDirty();
			}
		}

		public override void ModifyMesh(VertexHelper helper)
		{
			if (!IsActive() || helper.currentVertCount == 0)
			{
				return;
			}
			List<UIVertex> list = new List<UIVertex>();
			helper.GetUIVertexStream(list);
			int count = list.Count;
			switch (GradientType)
			{
			case Type.Horizontal:
			case Type.Vertical:
			{
				Rect bounds2 = GetBounds(list);
				float num3 = bounds2.xMin;
				float num4 = bounds2.width;
				Func<UIVertex, float> func = (UIVertex v) => v.position.x;
				if (GradientType == Type.Vertical)
				{
					num3 = bounds2.yMin;
					num4 = bounds2.height;
					func = (UIVertex v) => v.position.y;
				}
				float num5 = ((num4 == 0f) ? 0f : (1f / num4 / Zoom));
				float num6 = (1f - 1f / Zoom) * 0.5f;
				float num7 = Offset * (1f - num6) - num6;
				if (ModifyVertices)
				{
					SplitTrianglesAtGradientStops(list, bounds2, num6, helper);
				}
				UIVertex vertex2 = default;
				for (int num8 = 0; num8 < helper.currentVertCount; num8++)
				{
					helper.PopulateUIVertex(ref vertex2, num8);
					vertex2.color = BlendColor(vertex2.color, EffectGradient.Evaluate((func(vertex2) - num3) * num5 - num7));
					helper.SetUIVertex(vertex2, num8);
				}
				break;
			}
			case Type.Diamond:
			{
				Rect bounds = GetBounds(list);
				float num = ((bounds.height == 0f) ? 0f : (1f / bounds.height / Zoom));
				float num2 = bounds.center.y / 2f;
				Vector3 vector = (Vector3.right + Vector3.up) * num2 + Vector3.forward * list[0].position.z;
				if (ModifyVertices)
				{
					helper.Clear();
					for (int i = 0; i < count; i++)
					{
						helper.AddVert(list[i]);
					}
					helper.AddVert(new UIVertex
					{
						position = vector,
						normal = list[0].normal,
						uv0 = new Vector2(0.5f, 0.5f),
						color = Color.white
					});
					for (int j = 1; j < count; j++)
					{
						helper.AddTriangle(j - 1, j, count);
					}
					helper.AddTriangle(0, count - 1, count);
				}
				UIVertex vertex = default;
				for (int k = 0; k < helper.currentVertCount; k++)
				{
					helper.PopulateUIVertex(ref vertex, k);
					vertex.color = BlendColor(vertex.color, EffectGradient.Evaluate(Vector3.Distance(vertex.position, vector) * num - Offset));
					helper.SetUIVertex(vertex, k);
				}
				break;
			}
			}
		}

		private Rect GetBounds(List<UIVertex> vertices)
		{
			float num = vertices[0].position.x;
			float num2 = num;
			float num3 = vertices[0].position.y;
			float num4 = num3;
			for (int num5 = vertices.Count - 1; num5 >= 1; num5--)
			{
				float x = vertices[num5].position.x;
				float y = vertices[num5].position.y;
				if (x > num2)
				{
					num2 = x;
				}
				else if (x < num)
				{
					num = x;
				}
				if (y > num4)
				{
					num4 = y;
				}
				else if (y < num3)
				{
					num3 = y;
				}
			}
			return new Rect(num, num3, num2 - num, num4 - num3);
		}

		private void SplitTrianglesAtGradientStops(List<UIVertex> _vertexList, Rect bounds, float zoomOffset, VertexHelper helper)
		{
			List<float> list = FindStops(zoomOffset, bounds);
			if (list.Count <= 0)
			{
				return;
			}
			helper.Clear();
			int count = _vertexList.Count;
			for (int i = 0; i < count; i += 3)
			{
				float[] positions = GetPositions(_vertexList, i);
				List<int> list2 = new List<int>(3);
				List<UIVertex> list3 = new List<UIVertex>(3);
				List<UIVertex> list4 = new List<UIVertex>(2);
				for (int j = 0; j < list.Count; j++)
				{
					int currentVertCount = helper.currentVertCount;
					bool flag = list4.Count > 0;
					bool flag2 = false;
					for (int k = 0; k < 3; k++)
					{
						if (!list2.Contains(k) && positions[k] < list[j])
						{
							int num = (k + 1) % 3;
							UIVertex item = _vertexList[k + i];
							if (positions[num] > list[j])
							{
								list2.Insert(0, k);
								list3.Insert(0, item);
								flag2 = true;
							}
							else
							{
								list2.Add(k);
								list3.Add(item);
							}
						}
					}
					if (list2.Count == 0)
					{
						continue;
					}
					if (list2.Count == 3)
					{
						break;
					}
					foreach (UIVertex item3 in list3)
					{
						helper.AddVert(item3);
					}
					list4.Clear();
					foreach (int item4 in list2)
					{
						int num2 = (item4 + 1) % 3;
						if (positions[num2] < list[j])
						{
							num2 = (num2 + 1) % 3;
						}
						list4.Add(CreateSplitVertex(_vertexList[item4 + i], _vertexList[num2 + i], list[j]));
					}
					if (list4.Count == 1)
					{
						int num3 = (list2[0] + 2) % 3;
						list4.Add(CreateSplitVertex(_vertexList[list2[0] + i], _vertexList[num3 + i], list[j]));
					}
					foreach (UIVertex item5 in list4)
					{
						helper.AddVert(item5);
					}
					if (flag)
					{
						helper.AddTriangle(currentVertCount - 2, currentVertCount, currentVertCount + 1);
						helper.AddTriangle(currentVertCount - 2, currentVertCount + 1, currentVertCount - 1);
						if (list3.Count > 0)
						{
							if (flag2)
							{
								helper.AddTriangle(currentVertCount - 2, currentVertCount + 3, currentVertCount);
							}
							else
							{
								helper.AddTriangle(currentVertCount + 1, currentVertCount + 3, currentVertCount - 1);
							}
						}
					}
					else
					{
						int currentVertCount2 = helper.currentVertCount;
						helper.AddTriangle(currentVertCount, currentVertCount2 - 2, currentVertCount2 - 1);
						if (list3.Count > 1)
						{
							helper.AddTriangle(currentVertCount, currentVertCount2 - 1, currentVertCount + 1);
						}
					}
					list3.Clear();
				}
				if (list4.Count > 0)
				{
					if (list3.Count == 0)
					{
						for (int l = 0; l < 3; l++)
						{
							if (!list2.Contains(l) && positions[l] > list[list.Count - 1])
							{
								int num4 = (l + 1) % 3;
								UIVertex item2 = _vertexList[l + i];
								if (positions[num4] > list[list.Count - 1])
								{
									list3.Insert(0, item2);
								}
								else
								{
									list3.Add(item2);
								}
							}
						}
					}
					foreach (UIVertex item6 in list3)
					{
						helper.AddVert(item6);
					}
					int currentVertCount3 = helper.currentVertCount;
					if (list3.Count > 1)
					{
						helper.AddTriangle(currentVertCount3 - 4, currentVertCount3 - 2, currentVertCount3 - 1);
						helper.AddTriangle(currentVertCount3 - 4, currentVertCount3 - 1, currentVertCount3 - 3);
					}
					else if (list3.Count > 0)
					{
						helper.AddTriangle(currentVertCount3 - 3, currentVertCount3 - 1, currentVertCount3 - 2);
					}
				}
				else
				{
					helper.AddVert(_vertexList[i]);
					helper.AddVert(_vertexList[i + 1]);
					helper.AddVert(_vertexList[i + 2]);
					int currentVertCount4 = helper.currentVertCount;
					helper.AddTriangle(currentVertCount4 - 3, currentVertCount4 - 2, currentVertCount4 - 1);
				}
			}
		}

		private float[] GetPositions(List<UIVertex> _vertexList, int index)
		{
			float[] array = new float[3];
			if (GradientType == Type.Horizontal)
			{
				array[0] = _vertexList[index].position.x;
				array[1] = _vertexList[index + 1].position.x;
				array[2] = _vertexList[index + 2].position.x;
			}
			else
			{
				array[0] = _vertexList[index].position.y;
				array[1] = _vertexList[index + 1].position.y;
				array[2] = _vertexList[index + 2].position.y;
			}
			return array;
		}

		private List<float> FindStops(float zoomOffset, Rect bounds)
		{
			List<float> list = new List<float>();
			float num = Offset * (1f - zoomOffset);
			float num2 = zoomOffset - num;
			float num3 = 1f - zoomOffset - num;
			GradientColorKey[] colorKeys = EffectGradient.colorKeys;
			for (int i = 0; i < colorKeys.Length; i++)
			{
				GradientColorKey gradientColorKey = colorKeys[i];
				if (gradientColorKey.time >= num3)
				{
					break;
				}
				if (gradientColorKey.time > num2)
				{
					list.Add((gradientColorKey.time - num2) * Zoom);
				}
			}
			GradientAlphaKey[] alphaKeys = EffectGradient.alphaKeys;
			for (int i = 0; i < alphaKeys.Length; i++)
			{
				GradientAlphaKey gradientAlphaKey = alphaKeys[i];
				if (gradientAlphaKey.time >= num3)
				{
					break;
				}
				if (gradientAlphaKey.time > num2)
				{
					list.Add((gradientAlphaKey.time - num2) * Zoom);
				}
			}
			float num4 = bounds.xMin;
			float num5 = bounds.width;
			if (GradientType == Type.Vertical)
			{
				num4 = bounds.yMin;
				num5 = bounds.height;
			}
			list.Sort();
			for (int j = 0; j < list.Count; j++)
			{
				list[j] = list[j] * num5 + num4;
				if (j > 0 && Math.Abs(list[j] - list[j - 1]) < 2f)
				{
					list.RemoveAt(j);
					j--;
				}
			}
			return list;
		}

		private UIVertex CreateSplitVertex(UIVertex vertex1, UIVertex vertex2, float stop)
		{
			if (GradientType == Type.Horizontal)
			{
				float num = vertex1.position.x - stop;
				float num2 = vertex1.position.x - vertex2.position.x;
				float num3 = vertex1.position.y - vertex2.position.y;
				float num4 = vertex1.uv0.x - vertex2.uv0.x;
				float num5 = vertex1.uv0.y - vertex2.uv0.y;
				float num6 = num / num2;
				float y = vertex1.position.y - num3 * num6;
				return new UIVertex
				{
					position = new Vector3(stop, y, vertex1.position.z),
					normal = vertex1.normal,
					uv0 = new Vector2(vertex1.uv0.x - num4 * num6, vertex1.uv0.y - num5 * num6),
					color = Color.white
				};
			}
			float num7 = vertex1.position.y - stop;
			float num8 = vertex1.position.y - vertex2.position.y;
			float num9 = vertex1.position.x - vertex2.position.x;
			float num10 = vertex1.uv0.x - vertex2.uv0.x;
			float num11 = vertex1.uv0.y - vertex2.uv0.y;
			float num12 = num7 / num8;
			float x = vertex1.position.x - num9 * num12;
			return new UIVertex
			{
				position = new Vector3(x, stop, vertex1.position.z),
				normal = vertex1.normal,
				uv0 = new Vector2(vertex1.uv0.x - num10 * num12, vertex1.uv0.y - num11 * num12),
				color = Color.white
			};
		}

		private Color BlendColor(Color colorA, Color colorB)
		{
			return BlendMode switch
			{
				Blend.Add => colorA + colorB, 
				Blend.Multiply => colorA * colorB, 
				_ => colorB, 
			};
		}
	}
}
