using System;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	[Serializable]
	public struct SurfWave : INetworkSerializable, IEquatable<SurfWave>
	{
		public Vector3 Origin;

		public Vector3 Direction;

		public double StartTime;

		public float Duration;

		public float Speed;

		public float Height;

		public float Width;

		public float Length;

		public uint Id;

		public float Envelope(double time)
		{
			float num = (float)(time - StartTime);
			if (Duration <= 0f || num <= 0f || num >= Duration)
			{
				return 0f;
			}
			return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num / 3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Duration - num) / 4f));
		}

		public Vector3 Center(double time)
		{
			return Origin + Direction * (Speed * Mathf.Max(0f, (float)(time - StartTime)));
		}

		public bool TryGetArrival(Vector3 position, double time, out float seconds)
		{
			seconds = 0f;
			if (Id == 0 || Speed <= 0f || time >= StartTime + (double)Duration)
			{
				return false;
			}
			Vector3 lhs = position - Center(time);
			float num = Vector3.Dot(lhs, Direction);
			float num2 = Mathf.Abs(Vector3.Dot(lhs, Vector3.Cross(Vector3.up, Direction)));
			if (num <= 0f || num2 > Width * 0.4f)
			{
				return false;
			}
			seconds = Mathf.Max(0f, (float)(StartTime - time)) + num / Speed;
			return time + (double)seconds < StartTime + (double)Duration - 2.0;
		}

		public float Sample(Vector3 position, double time)
		{
			if (Width <= 0f || Length <= 0f)
			{
				return 0f;
			}
			Vector3 lhs = position - Center(time);
			float num = Vector3.Dot(lhs, Direction) / Length;
			float num2 = Vector3.Dot(lhs, Vector3.Cross(Vector3.up, Direction)) / Width;
			if (Mathf.Abs(num) >= 0.5f || Mathf.Abs(num2) >= 0.5f)
			{
				return 0f;
			}
			float num3 = 0.5f + 0.5f * Mathf.Cos(num * MathF.PI * 2f);
			float num4 = 0.5f + 0.5f * Mathf.Cos(num2 * MathF.PI * 2f);
			return Height * Envelope(time) * num3 * num4;
		}

		public float CatchStrength(Vector3 position, Vector3 forward, double time)
		{
			float num = Vector3.Dot(position - Center(time), Direction) / Mathf.Max(0.1f, Length);
			if (num < 0f || num > 0.5f)
			{
				return 0f;
			}
			return Sample(position, time) / Mathf.Max(Height, 0.1f) * Mathf.InverseLerp(0.15f, 0.85f, Vector3.Dot(forward, Direction));
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Origin);
			serializer.SerializeValue(ref Direction);
			serializer.SerializeValue(ref StartTime, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Duration, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Speed, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Height, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Width, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Length, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Id, default(FastBufferWriter.ForPrimitives));
		}

		public bool Equals(SurfWave other)
		{
			if (Id == other.Id && StartTime.Equals(other.StartTime) && Origin == other.Origin && Direction == other.Direction && Duration == other.Duration && Speed == other.Speed && Height == other.Height && Width == other.Width)
			{
				return Length == other.Length;
			}
			return false;
		}
	}
}
