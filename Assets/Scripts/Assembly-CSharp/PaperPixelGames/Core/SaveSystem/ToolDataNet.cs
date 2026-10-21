using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public struct ToolDataNet : INetworkSerializable, IEquatable<ToolDataNet>
	{
		public ushort SyncRevision;

		public byte DivingEquipmentLevel;

		public byte HarpoonLevel;

		public byte FishingRodLevel;

		public byte MopLevel;

		public byte ScooterLevel;

		public byte CompassLevel;

		public bool DivingEquipmentUnlocked;

		public bool HarpoonUnlocked;

		public bool FishingRodUnlocked;

		public bool MopUnlocked;

		public bool ScooterUnlocked;

		public bool CompassUnlocked;

		public bool PoseidonTridentUnlocked;

		public ToolDataNet(ToolData data)
		{
			SyncRevision = 1;
			DivingEquipmentLevel = data.DivingEquipmentLevel;
			HarpoonLevel = data.HarpoonLevel;
			FishingRodLevel = data.FishingRodLevel;
			MopLevel = data.MopLevel;
			ScooterLevel = data.ScooterLevel;
			CompassLevel = data.CompassLevel;
			DivingEquipmentUnlocked = data.DivingEquipmentUnlocked;
			HarpoonUnlocked = data.HarpoonUnlocked;
			FishingRodUnlocked = data.FishingRodUnlocked;
			MopUnlocked = data.MopUnlocked;
			ScooterUnlocked = data.ScooterUnlocked;
			CompassUnlocked = data.CompassUnlocked;
			PoseidonTridentUnlocked = data.PoseidonTridentUnlocked;
		}

		public ToolData ToToolData()
		{
			return new ToolData
			{
				DivingEquipmentLevel = DivingEquipmentLevel,
				HarpoonLevel = HarpoonLevel,
				FishingRodLevel = FishingRodLevel,
				MopLevel = MopLevel,
				ScooterLevel = ScooterLevel,
				CompassLevel = CompassLevel,
				DivingEquipmentUnlocked = DivingEquipmentUnlocked,
				HarpoonUnlocked = HarpoonUnlocked,
				FishingRodUnlocked = FishingRodUnlocked,
				MopUnlocked = MopUnlocked,
				ScooterUnlocked = ScooterUnlocked,
				CompassUnlocked = CompassUnlocked,
				PoseidonTridentUnlocked = PoseidonTridentUnlocked
			};
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref SyncRevision, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref DivingEquipmentLevel);
			serializer.SerializeValue(ref HarpoonLevel);
			serializer.SerializeValue(ref FishingRodLevel);
			serializer.SerializeValue(ref MopLevel);
			serializer.SerializeValue(ref ScooterLevel);
			serializer.SerializeValue(ref CompassLevel);
			serializer.SerializeValue(ref DivingEquipmentUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref HarpoonUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref FishingRodUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref MopUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref ScooterUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CompassUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref PoseidonTridentUnlocked, default(FastBufferWriter.ForPrimitives));
		}

		public bool Equals(ToolDataNet other)
		{
			if (SyncRevision == other.SyncRevision && DivingEquipmentLevel == other.DivingEquipmentLevel && HarpoonLevel == other.HarpoonLevel && FishingRodLevel == other.FishingRodLevel && MopLevel == other.MopLevel && ScooterLevel == other.ScooterLevel && CompassLevel == other.CompassLevel && DivingEquipmentUnlocked == other.DivingEquipmentUnlocked && HarpoonUnlocked == other.HarpoonUnlocked && FishingRodUnlocked == other.FishingRodUnlocked && MopUnlocked == other.MopUnlocked && ScooterUnlocked == other.ScooterUnlocked && CompassUnlocked == other.CompassUnlocked)
			{
				return PoseidonTridentUnlocked == other.PoseidonTridentUnlocked;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ToolDataNet other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (((((((((((((17 * 31 + SyncRevision.GetHashCode()) * 31 + DivingEquipmentLevel.GetHashCode()) * 31 + HarpoonLevel.GetHashCode()) * 31 + FishingRodLevel.GetHashCode()) * 31 + MopLevel.GetHashCode()) * 31 + ScooterLevel.GetHashCode()) * 31 + CompassLevel.GetHashCode()) * 31 + DivingEquipmentUnlocked.GetHashCode()) * 31 + HarpoonUnlocked.GetHashCode()) * 31 + FishingRodUnlocked.GetHashCode()) * 31 + MopUnlocked.GetHashCode()) * 31 + ScooterUnlocked.GetHashCode()) * 31 + CompassUnlocked.GetHashCode()) * 31 + PoseidonTridentUnlocked.GetHashCode();
		}

		public static bool operator ==(ToolDataNet left, ToolDataNet right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ToolDataNet left, ToolDataNet right)
		{
			return !left.Equals(right);
		}
	}
}
