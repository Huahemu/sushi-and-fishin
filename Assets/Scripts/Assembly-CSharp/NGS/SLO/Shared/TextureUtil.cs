using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NGS.SLO.Shared
{
	public static class TextureUtil
	{
		private enum OperationType
		{
			Uncompress = 0,
			Offset = 1,
			Repeat = 2,
			Resize = 3,
			UncompressRepeatResize = 4
		}

		private struct TextureOperation
		{
			public Texture2D source;

			public OperationType operation;

			public float value1;

			public float value2;

			public TextureOperation(Texture2D source, OperationType operation, float value1, float value2)
			{
				this.source = source;
				this.operation = operation;
				this.value1 = value1;
				this.value2 = value2;
			}

			public override bool Equals(object obj)
			{
				if (obj is TextureOperation other)
				{
					return Equals(other);
				}
				return false;
			}

			public bool Equals(TextureOperation other)
			{
				if (source == other.source && operation == other.operation && Approximately(value1, other.value1))
				{
					return Approximately(value2, other.value2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return (((17 * 31 + ((source != null) ? source.GetInstanceID() : 0)) * 31 + operation.GetHashCode()) * 31 + value1.GetHashCode()) * 31 + value2.GetHashCode();
			}

			private static bool Approximately(float a, float b)
			{
				return Mathf.Abs(a - b) < 0.0001f;
			}
		}

		[BurstCompile]
		internal struct TextureOffsetJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeArray<Color32> sourcePixels;

			public int sourceWidth;

			public int sourceHeight;

			[WriteOnly]
			public NativeArray<Color32> resultPixels;

			public float2 offset;

			public void Execute(int index)
			{
				int num = index % sourceWidth;
				int num2 = index / sourceWidth;
				Color color = GetPixelBilinear(uv: new float2((float)num / (float)sourceWidth + offset.x, (float)num2 / (float)sourceHeight + offset.y), pixels: sourcePixels, width: sourceWidth, height: sourceHeight);
				resultPixels[num2 * sourceWidth + num] = color;
			}

			private static Color GetPixelBilinear(NativeArray<Color32> pixels, int width, int height, float2 uv)
			{
				float2 obj = math.frac(uv);
				float num = obj.x * (float)(width - 1);
				float num2 = obj.y * (float)(height - 1);
				int num3 = (int)math.floor(num);
				int num4 = (int)math.floor(num2);
				int num5 = math.min(num3 + 1, width - 1);
				int num6 = math.min(num4 + 1, height - 1);
				float t = num - (float)num3;
				float t2 = num2 - (float)num4;
				Color a = pixels[num3 + num4 * width];
				Color b = pixels[num5 + num4 * width];
				Color a2 = pixels[num3 + num6 * width];
				Color b2 = pixels[num5 + num6 * width];
				Color a3 = Color.Lerp(a, b, t);
				Color b3 = Color.Lerp(a2, b2, t);
				return Color.Lerp(a3, b3, t2);
			}
		}

		[BurstCompile]
		internal struct TextureRepeatJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeArray<Color32> sourcePixels;

			public int sourceWidth;

			public int sourceHeight;

			[WriteOnly]
			public NativeArray<Color32> resultPixels;

			public int repeatX;

			public int repeatY;

			public void Execute(int index)
			{
				int num = sourceWidth * repeatX;
				int num2 = index / num;
				int num3 = index % num;
				int num4 = num2 % sourceHeight;
				int num5 = num3 % sourceWidth;
				int index2 = num4 * sourceWidth + num5;
				resultPixels[index] = sourcePixels[index2];
			}
		}

		[BurstCompile]
		internal struct TextureResizeJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeArray<Color32> sourcePixels;

			public int sourceWidth;

			public int sourceHeight;

			[WriteOnly]
			public NativeArray<Color32> resultPixels;

			public int resultWidth;

			public int resultHeight;

			public void Execute(int index)
			{
				int num = index % resultWidth;
				int num2 = index / resultWidth;
				float num3 = (float)num / (float)(resultWidth - 1);
				float num4 = (float)num2 / (float)(resultHeight - 1);
				float num5 = num3 * (float)(sourceWidth - 1);
				float num6 = num4 * (float)(sourceHeight - 1);
				int num7 = (int)math.floor(num5);
				int num8 = (int)math.floor(num6);
				int num9 = math.min(num7 + 1, sourceWidth - 1);
				int num10 = math.min(num8 + 1, sourceHeight - 1);
				float num11 = num5 - (float)num7;
				float num12 = num6 - (float)num8;
				float num13 = 1f - num11;
				float num14 = 1f - num12;
				Color32 color = sourcePixels[num8 * sourceWidth + num7];
				Color32 color2 = sourcePixels[num8 * sourceWidth + num9];
				Color32 color3 = sourcePixels[num10 * sourceWidth + num7];
				Color32 color4 = sourcePixels[num10 * sourceWidth + num9];
				float4 obj = new float4((int)color.r, (int)color.g, (int)color.b, (int)color.a);
				float4 float5 = new float4((int)color2.r, (int)color2.g, (int)color2.b, (int)color2.a);
				float4 float6 = new float4((int)color3.r, (int)color3.g, (int)color3.b, (int)color3.a);
				float4 float7 = new float4((int)color4.r, (int)color4.g, (int)color4.b, (int)color4.a);
				float4 obj2 = obj * num13 + float5 * num11;
				float4 float8 = float6 * num13 + float7 * num11;
				float4 obj3 = obj2 * num14 + float8 * num12;
				byte r = (byte)math.clamp(math.round(obj3.x), 0f, 255f);
				byte g = (byte)math.clamp(math.round(obj3.y), 0f, 255f);
				byte b = (byte)math.clamp(math.round(obj3.z), 0f, 255f);
				byte a = (byte)math.clamp(math.round(obj3.w), 0f, 255f);
				resultPixels[index] = new Color32(r, g, b, a);
			}
		}

		[BurstCompile]
		internal struct TextureRepeatAndResizeJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeArray<Color32> sourcePixels;

			public int sourceWidth;

			public int sourceHeight;

			[WriteOnly]
			public NativeArray<Color32> resultPixels;

			public int resultWidth;

			public int resultHeight;

			public int repeatX;

			public int repeatY;

			public void Execute(int index)
			{
				int num = index % resultWidth;
				int num2 = index / resultWidth;
				float num3 = (float)num / (float)(resultWidth - 1);
				float num4 = (float)num2 / (float)(resultHeight - 1);
				float num5 = num3 * (float)(sourceWidth * repeatX - 1);
				float num6 = num4 * (float)(sourceHeight * repeatY - 1);
				int num7 = ((int)num5 % sourceWidth + sourceWidth) % sourceWidth;
				int num8 = ((int)num6 % sourceHeight + sourceHeight) % sourceHeight;
				int num9 = (int)math.floor(num7);
				int num10 = (int)math.floor(num8);
				int num11 = math.min(num9 + 1, sourceWidth - 1);
				int num12 = math.min(num10 + 1, sourceHeight - 1);
				float num13 = num7 - num9;
				float num14 = num8 - num10;
				float num15 = 1f - num13;
				float num16 = 1f - num14;
				Color32 color = sourcePixels[num10 * sourceWidth + num9];
				Color32 color2 = sourcePixels[num10 * sourceWidth + num11];
				Color32 color3 = sourcePixels[num12 * sourceWidth + num9];
				Color32 color4 = sourcePixels[num12 * sourceWidth + num11];
				float4 obj = new float4((int)color.r, (int)color.g, (int)color.b, (int)color.a);
				float4 float5 = new float4((int)color2.r, (int)color2.g, (int)color2.b, (int)color2.a);
				float4 float6 = new float4((int)color3.r, (int)color3.g, (int)color3.b, (int)color3.a);
				float4 float7 = new float4((int)color4.r, (int)color4.g, (int)color4.b, (int)color4.a);
				float4 obj2 = obj * num15 + float5 * num13;
				float4 float8 = float6 * num15 + float7 * num13;
				float4 obj3 = obj2 * num16 + float8 * num14;
				byte r = (byte)math.clamp(math.round(obj3.x), 0f, 255f);
				byte g = (byte)math.clamp(math.round(obj3.y), 0f, 255f);
				byte b = (byte)math.clamp(math.round(obj3.z), 0f, 255f);
				byte a = (byte)math.clamp(math.round(obj3.w), 0f, 255f);
				resultPixels[index] = new Color32(r, g, b, a);
			}
		}

		private const int MAX_CACHE_SIZE_MB = 300;

		private static Dictionary<TextureOperation, Texture2D> _texturesCache;

		private static float _cacheSizeMb;

		public static Texture2D GetTextureUncompressed(Texture2D source)
		{
			if (source == null)
			{
				throw new NullReferenceException("TexturesUtil::GetTextureUncompressed source texture is null");
			}
			TextureOperation operation = new TextureOperation(source, OperationType.Uncompress, 0f, 0f);
			if (TryGetTextureFromCache(operation, out var result))
			{
				return result;
			}
			return SaveTextureInCache(operation, CopyTextureByBlit(source));
		}

		public static Texture2D Repeat(Texture2D source, int repeatX, int repeatY)
		{
			if (source == null)
			{
				throw new NullReferenceException("TexturesUtil::Repeat source texture is null");
			}
			if (!source.isReadable || source.format != TextureFormat.RGBA32)
			{
				source = GetTextureUncompressed(source);
			}
			TextureOperation operation = new TextureOperation(source, OperationType.Repeat, repeatX, repeatY);
			if (TryGetTextureFromCache(operation, out var result))
			{
				return result;
			}
			int num = source.width * repeatX;
			int num2 = source.height * repeatY;
			Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
			NativeArray<Color32> pixelData = source.GetPixelData<Color32>(0);
			NativeArray<Color32> pixelData2 = texture2D.GetPixelData<Color32>(0);
			IJobParallelForExtensions.Schedule(new TextureRepeatJob
			{
				sourcePixels = pixelData,
				resultPixels = pixelData2,
				sourceWidth = source.width,
				sourceHeight = source.height,
				repeatX = repeatX,
				repeatY = repeatY
			}, num * num2, 64).Complete();
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return SaveTextureInCache(operation, texture2D);
		}

		public static Texture2D Resize(Texture2D source, int width, int height)
		{
			if (source == null)
			{
				throw new NullReferenceException("TexturesUtil::Resize source texture is null");
			}
			if (!source.isReadable || source.format != TextureFormat.RGBA32)
			{
				source = GetTextureUncompressed(source);
			}
			TextureOperation operation = new TextureOperation(source, OperationType.Resize, width, height);
			if (TryGetTextureFromCache(operation, out var result))
			{
				return result;
			}
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
			NativeArray<Color32> pixelData = source.GetPixelData<Color32>(0);
			NativeArray<Color32> pixelData2 = texture2D.GetPixelData<Color32>(0);
			IJobParallelForExtensions.Schedule(new TextureResizeJob
			{
				sourcePixels = pixelData,
				resultPixels = pixelData2,
				sourceWidth = source.width,
				sourceHeight = source.height,
				resultWidth = width,
				resultHeight = height
			}, width * height, 64).Complete();
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return SaveTextureInCache(operation, texture2D);
		}

		public static Texture2D UncompressRepeatResize(Texture2D source, int width, int height, int repeatX, int repeatY)
		{
			if (source == null)
			{
				throw new NullReferenceException("TexturesUtil::UncompressRepeatResize source texture is null");
			}
			TextureOperation operation = new TextureOperation(source, OperationType.UncompressRepeatResize, width + repeatX, height + repeatY);
			if (TryGetTextureFromCache(operation, out var result))
			{
				return result;
			}
			if (!source.isReadable || source.format != TextureFormat.RGBA32)
			{
				source = GetTextureUncompressed(source);
			}
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
			NativeArray<Color32> pixelData = source.GetPixelData<Color32>(0);
			NativeArray<Color32> pixelData2 = texture2D.GetPixelData<Color32>(0);
			IJobParallelForExtensions.Schedule(new TextureRepeatAndResizeJob
			{
				sourcePixels = pixelData,
				sourceWidth = source.width,
				sourceHeight = source.height,
				resultPixels = pixelData2,
				resultWidth = width,
				resultHeight = height,
				repeatX = repeatX,
				repeatY = repeatY
			}, width * height, 64).Complete();
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return SaveTextureInCache(operation, texture2D);
		}

		public static void ClearCache()
		{
			if (_texturesCache != null)
			{
				foreach (Texture2D value in _texturesCache.Values)
				{
					if (!(value == null))
					{
						UnityEngine.Object.DestroyImmediate(value);
					}
				}
				_texturesCache.Clear();
			}
			_cacheSizeMb = 0f;
			GC.Collect();
		}

		private static Texture2D CopyTextureByBlit(Texture2D source)
		{
			RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(source, temporary);
			RenderTexture.active = temporary;
			Texture2D texture2D = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(temporary);
			return texture2D;
		}

		private static Texture2D CopyNormalMapByBlit(Texture2D source)
		{
			RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			Graphics.Blit(source, temporary);
			RenderTexture.active = temporary;
			Texture2D texture2D = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false, linear: true);
			texture2D.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
			Color32[] pixels = texture2D.GetPixels32();
			Color32[] array = new Color32[pixels.Length];
			for (int i = 0; i < pixels.Length; i++)
			{
				float num = (float)(int)pixels[i].a / 255f * 2f - 1f;
				float num2 = (float)(int)pixels[i].g / 255f * 2f - 1f;
				float num3 = Mathf.Sqrt(Mathf.Clamp01(1f - num * num - num2 * num2));
				byte r = (byte)((num * 0.5f + 0.5f) * 255f);
				byte g = (byte)((num2 * 0.5f + 0.5f) * 255f);
				byte b = (byte)((num3 * 0.5f + 0.5f) * 255f);
				array[i] = new Color32(r, g, b, byte.MaxValue);
			}
			texture2D.SetPixels32(array);
			texture2D.Apply();
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(temporary);
			return texture2D;
		}

		private static bool TryGetTextureFromCache(TextureOperation operation, out Texture2D result)
		{
			if (_texturesCache == null)
			{
				result = null;
				return false;
			}
			if (_texturesCache.TryGetValue(operation, out result))
			{
				return true;
			}
			return false;
		}

		private static Texture2D SaveTextureInCache(TextureOperation operation, Texture2D texture)
		{
			try
			{
				if (_texturesCache == null)
				{
					_texturesCache = new Dictionary<TextureOperation, Texture2D>();
				}
				if (!_texturesCache.ContainsKey(operation))
				{
					_texturesCache.Add(operation, texture);
					_cacheSizeMb += (float)(texture.width * texture.height * 4) / 1048576f;
				}
				return texture;
			}
			catch (Exception ex)
			{
				Debug.Log("TexturesUtil::SaveTextureInCache error : " + ex.Message + " \n " + ex.StackTrace);
				return texture;
			}
		}
	}
}
