using System;
using System.Collections.Generic;
using NGS.SLO.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public class TexturePacker : ITexturePacker
	{
		[BurstCompile]
		internal struct FillPixelsBlackColorJob : IJobParallelFor
		{
			[WriteOnly]
			public NativeArray<Color32> atlasPixels;

			public void Execute(int index)
			{
				atlasPixels[index] = new Color32(0, 0, 0, 0);
			}
		}

		[BurstCompile]
		internal struct ApplyTextureToAtlasJob : IJobParallelFor
		{
			[WriteOnly]
			[NativeDisableContainerSafetyRestriction]
			public NativeArray<Color32> atlasPixels;

			public int atlasWidth;

			[ReadOnly]
			public NativeArray<Color32> texturePixels;

			public int textureWidth;

			public int textureHeight;

			public Rect innerRect;

			public void Execute(int index)
			{
				int num = (int)innerRect.width;
				int num2 = index % num;
				int num3 = index / num;
				float u = (float)num2 / (float)(num - 1);
				float v = (float)num3 / (float)((int)innerRect.height - 1);
				Color32 pixelBilinear = GetPixelBilinear(texturePixels, textureWidth, textureHeight, u, v);
				int num4 = (int)innerRect.x + num2;
				int num5 = (int)innerRect.y + num3;
				atlasPixels[num5 * atlasWidth + num4] = pixelBilinear;
			}

			private Color32 GetPixelBilinear(NativeArray<Color32> pixels, int width, int height, float u, float v)
			{
				u = Mathf.Clamp01(u);
				v = Mathf.Clamp01(v);
				float num = u * (float)(width - 1);
				float num2 = v * (float)(height - 1);
				int num3 = Mathf.FloorToInt(num);
				int num4 = Mathf.FloorToInt(num2);
				int num5 = Mathf.Min(num3 + 1, width - 1);
				int num6 = Mathf.Min(num4 + 1, height - 1);
				float t = num - (float)num3;
				float t2 = num2 - (float)num4;
				Color32 a = pixels[num4 * width + num3];
				Color32 b = pixels[num4 * width + num5];
				Color32 a2 = pixels[num6 * width + num3];
				Color32 b2 = pixels[num6 * width + num5];
				Color32 a3 = Color32.Lerp(a, b, t);
				Color32 b3 = Color32.Lerp(a2, b2, t);
				return Color32.Lerp(a3, b3, t2);
			}
		}

		[BurstCompile]
		internal struct FillPaddingJob : IJob
		{
			[NativeDisableContainerSafetyRestriction]
			public NativeArray<Color32> atlasPixels;

			public int atlasWidth;

			public int padding;

			public Rect innerRect;

			public void Execute()
			{
				for (int i = (int)innerRect.x; (float)i < innerRect.x + innerRect.width; i++)
				{
					Color pixel = GetPixel(i, (int)innerRect.y);
					Color pixel2 = GetPixel(i, (int)(innerRect.y + innerRect.height - 1f));
					int num = (int)innerRect.y;
					int num2 = (int)(innerRect.y + innerRect.height - 1f);
					for (int j = 1; j <= padding; j++)
					{
						SetPixel(i, num - j, pixel);
						SetPixel(i, num2 + j, pixel2);
					}
				}
				for (int k = (int)innerRect.y; (float)k < innerRect.y + innerRect.height; k++)
				{
					Color pixel3 = GetPixel((int)innerRect.x, k);
					Color pixel4 = GetPixel((int)(innerRect.x + innerRect.width - 1f), k);
					for (int l = 1; l <= padding; l++)
					{
						SetPixel((int)innerRect.x - l, k, pixel3);
						SetPixel((int)(innerRect.x + innerRect.width - 1f + (float)l), k, pixel4);
					}
				}
				Color pixel5 = GetPixel((int)innerRect.x, (int)innerRect.y);
				Color pixel6 = GetPixel((int)(innerRect.x + innerRect.width - 1f), (int)innerRect.y);
				Color pixel7 = GetPixel((int)innerRect.x, (int)(innerRect.y + innerRect.height - 1f));
				Color pixel8 = GetPixel((int)(innerRect.x + innerRect.width - 1f), (int)(innerRect.y + innerRect.height - 1f));
				for (int m = 1; m <= padding; m++)
				{
					for (int n = 1; n <= padding; n++)
					{
						SetPixel((int)innerRect.x - m, (int)innerRect.y - n, pixel5);
						SetPixel((int)(innerRect.x + innerRect.width - 1f + (float)m), (int)innerRect.y - n, pixel6);
						SetPixel((int)innerRect.x - m, (int)(innerRect.y + innerRect.height - 1f + (float)n), pixel7);
						SetPixel((int)(innerRect.x + innerRect.width - 1f + (float)m), (int)(innerRect.y + innerRect.height - 1f + (float)n), pixel8);
					}
				}
			}

			private Color GetPixel(int x, int y)
			{
				return atlasPixels[y * atlasWidth + x];
			}

			private void SetPixel(int x, int y, Color pixel)
			{
				atlasPixels[y * atlasWidth + x] = pixel;
			}
		}

		public Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects)
		{
			if (textures == null || textures.Length == 0)
			{
				throw new NullReferenceException("TexturePacker::'textures' is empty");
			}
			Texture2D[] array = new Texture2D[textures.Length];
			int num = int.MinValue;
			for (int i = 0; i < textures.Length; i++)
			{
				array[i] = LoadUncompressedTexture(textures[i]);
				num = Mathf.Max(num, textures[i].width, textures[i].height);
			}
			num = Mathf.NextPowerOfTwo(num);
			Vector2 atlasSize = new Vector2(Mathf.Min(num, maxAtlasSize), Mathf.Min(num, maxAtlasSize));
			Rect[] placements = null;
			while (atlasSize.x <= (float)maxAtlasSize || atlasSize.y <= (float)maxAtlasSize)
			{
				if (TryPlaceTextures(textures, atlasSize, out placements))
				{
					Texture2D texture2D = CreateAtlasTexture(atlasSize);
					PlaceTexturesToAtlas(texture2D, array, placements, padding);
					ConvertPlacementsToUV(ref placements, atlasSize, padding);
					rects = placements;
					return texture2D;
				}
				if (atlasSize.x <= atlasSize.y)
				{
					atlasSize.x *= 2f;
				}
				else
				{
					atlasSize.y *= 2f;
				}
			}
			throw new ArgumentOutOfRangeException("TexturePacker::It is unable to fit all textures into an atlas of a given size.");
		}

		private Texture2D LoadUncompressedTexture(Texture2D source)
		{
			if (source.isReadable && source.format == TextureFormat.RGBA32)
			{
				return source;
			}
			return TextureUtil.GetTextureUncompressed(source);
		}

		private Texture2D CreateAtlasTexture(Vector2 atlasSize)
		{
			return new Texture2D((int)atlasSize.x, (int)atlasSize.y, TextureFormat.RGBA32, mipChain: true)
			{
				wrapMode = TextureWrapMode.Clamp
			};
		}

		private bool TryPlaceTextures(Texture2D[] textures, Vector2 atlasSize, out Rect[] placements)
		{
			TexturePlacer texturePlacer = new TexturePlacer((int)atlasSize.x, (int)atlasSize.y);
			List<int> list = new List<int>();
			for (int i = 0; i < textures.Length; i++)
			{
				list.Add(i);
			}
			list.Sort((int a, int b) => (textures[b].width * textures[b].height).CompareTo(textures[a].width * textures[a].height));
			placements = new Rect[textures.Length];
			foreach (int item in list)
			{
				if (!texturePlacer.TryInsert(textures[item].width, textures[item].height, out var resultRect))
				{
					return false;
				}
				placements[item] = resultRect;
			}
			return true;
		}

		private void PlaceTexturesToAtlas(Texture2D atlas, Texture2D[] textures, Rect[] placements, int padding)
		{
			NativeArray<Color32> pixelData = atlas.GetPixelData<Color32>(0);
			IJobParallelForExtensions.Schedule(new FillPixelsBlackColorJob
			{
				atlasPixels = pixelData
			}, pixelData.Length, 64).Complete();
			JobHandle job = default;
			for (int i = 0; i < textures.Length; i++)
			{
				JobHandle job2 = PlaceTextureToAtlas(atlas, pixelData, textures[i], placements[i], padding);
				job = JobHandle.CombineDependencies(job, job2);
			}
			job.Complete();
			atlas.Apply(updateMipmaps: true, makeNoLongerReadable: false);
		}

		private JobHandle PlaceTextureToAtlas(Texture2D atlas, NativeArray<Color32> atlasPixels, Texture2D texture, Rect placement, int padding)
		{
			Rect innerRect = GetInnerRect(placement, padding);
			NativeArray<Color32> pixelData = texture.GetPixelData<Color32>(0);
			JobHandle jobHandle = IJobParallelForExtensions.Schedule(new ApplyTextureToAtlasJob
			{
				atlasPixels = atlasPixels,
				atlasWidth = atlas.width,
				texturePixels = pixelData,
				textureWidth = texture.width,
				textureHeight = texture.height,
				innerRect = innerRect
			}, (int)(innerRect.width * innerRect.height), 64);
			if (innerRect.width == placement.width)
			{
				return jobHandle;
			}
			JobHandle job = new FillPaddingJob
			{
				atlasPixels = atlasPixels,
				atlasWidth = atlas.width,
				padding = padding,
				innerRect = innerRect
			}.Schedule(jobHandle);
			return JobHandle.CombineDependencies(jobHandle, job);
		}

		private void ConvertPlacementsToUV(ref Rect[] placements, Vector2 atlasSize, int padding)
		{
			for (int i = 0; i < placements.Length; i++)
			{
				Rect placement = placements[i];
				Rect innerRect = GetInnerRect(placement, padding);
				placements[i] = new Rect(innerRect.x / atlasSize.x, innerRect.y / atlasSize.y, innerRect.width / atlasSize.x, innerRect.height / atlasSize.y);
			}
		}

		private Rect GetInnerRect(Rect placement, int padding)
		{
			if (placement.width <= (float)(padding * 2) || placement.height <= (float)(padding * 2))
			{
				return placement;
			}
			return new Rect(placement.x + (float)padding, placement.y + (float)padding, placement.width - (float)(padding * 2), placement.height - (float)(padding * 2));
		}

		private void FillEmptyPixels(Texture2D atlas, Texture2D[] textures, Rect[] placements)
		{
			for (int i = 0; i < atlas.width; i++)
			{
				for (int j = 0; j < atlas.height; j++)
				{
					float num = float.MaxValue;
					Color color = Color.clear;
					for (int k = 0; k < placements.Length; k++)
					{
						Rect r = placements[k];
						if (r.Contains(new Vector2(i, j)))
						{
							num = -1f;
							break;
						}
						int num2 = (int)DistanceToRect(r, i, j);
						if ((float)num2 < num)
						{
							num = num2;
							float u = ((float)i - r.x) / r.width;
							float v = ((float)j - r.y) / r.height;
							color = textures[k].GetPixelBilinear(u, v);
						}
					}
					if (num > -1f)
					{
						atlas.SetPixel(i, j, color);
					}
				}
			}
		}

		private float DistanceToRect(Rect r, int x, int y)
		{
			float num = Mathf.Max(Mathf.Abs((float)x - r.center.x) - r.width / 2f, 0f);
			float num2 = Mathf.Max(Mathf.Abs((float)y - r.center.y) - r.height / 2f, 0f);
			return num * num + num2 * num2;
		}
	}
}
