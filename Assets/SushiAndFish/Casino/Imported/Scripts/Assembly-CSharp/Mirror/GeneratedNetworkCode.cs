using SushiAndFish.Casino.Original;
using global::System;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::Dissonance.Integrations.MirrorIgnorance;
using global::Mirror.Discovery;
using SushiAndFish.Casino.Original.Smooth;
using global::UnityEngine;

namespace Mirror
{
	[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
	public static class GeneratedNetworkCode
	{
		public static TimeSnapshotMessage _Read_Mirror_002ETimeSnapshotMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002ETimeSnapshotMessage(NetworkWriter writer, TimeSnapshotMessage value)
		{
		}

		public static ReadyMessage _Read_Mirror_002EReadyMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002EReadyMessage(NetworkWriter writer, ReadyMessage value)
		{
		}

		public static NotReadyMessage _Read_Mirror_002ENotReadyMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002ENotReadyMessage(NetworkWriter writer, NotReadyMessage value)
		{
		}

		public static AddPlayerMessage _Read_Mirror_002EAddPlayerMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002EAddPlayerMessage(NetworkWriter writer, AddPlayerMessage value)
		{
		}

		public static SceneMessage _Read_Mirror_002ESceneMessage(NetworkReader reader)
		{
			return new SceneMessage
			{
				sceneName = reader.ReadString(),
				sceneOperation = _Read_Mirror_002ESceneOperation(reader),
				customHandling = reader.ReadBool()
			};
		}

		public static SceneOperation _Read_Mirror_002ESceneOperation(NetworkReader reader)
		{
			return (SceneOperation)NetworkReaderExtensions.ReadByte(reader);
		}

		public static void _Write_Mirror_002ESceneMessage(NetworkWriter writer, SceneMessage value)
		{
			writer.WriteString(value.sceneName);
			_Write_Mirror_002ESceneOperation(writer, value.sceneOperation);
			writer.WriteBool(value.customHandling);
		}

		public static void _Write_Mirror_002ESceneOperation(NetworkWriter writer, SceneOperation value)
		{
			NetworkWriterExtensions.WriteByte(writer, (byte)value);
		}

		public static CommandMessage _Read_Mirror_002ECommandMessage(NetworkReader reader)
		{
			return new CommandMessage
			{
				netId = reader.ReadVarUInt(),
				componentIndex = NetworkReaderExtensions.ReadByte(reader),
				functionHash = reader.ReadUShort(),
				payload = reader.ReadArraySegmentAndSize()
			};
		}

		public static void _Write_Mirror_002ECommandMessage(NetworkWriter writer, CommandMessage value)
		{
			writer.WriteVarUInt(value.netId);
			NetworkWriterExtensions.WriteByte(writer, value.componentIndex);
			writer.WriteUShort(value.functionHash);
			writer.WriteArraySegmentAndSize(value.payload);
		}

		public static RpcMessage _Read_Mirror_002ERpcMessage(NetworkReader reader)
		{
			return new RpcMessage
			{
				netId = reader.ReadVarUInt(),
				componentIndex = NetworkReaderExtensions.ReadByte(reader),
				functionHash = reader.ReadUShort(),
				payload = reader.ReadArraySegmentAndSize()
			};
		}

		public static void _Write_Mirror_002ERpcMessage(NetworkWriter writer, RpcMessage value)
		{
			writer.WriteVarUInt(value.netId);
			NetworkWriterExtensions.WriteByte(writer, value.componentIndex);
			writer.WriteUShort(value.functionHash);
			writer.WriteArraySegmentAndSize(value.payload);
		}

		public static SpawnMessage _Read_Mirror_002ESpawnMessage(NetworkReader reader)
		{
			return new SpawnMessage
			{
				netId = reader.ReadVarUInt(),
				spawnFlags = _Read_Mirror_002ESpawnFlags(reader),
				sceneId = reader.ReadVarULong(),
				assetId = reader.ReadVarUInt(),
				position = reader.ReadVector3(),
				rotation = reader.ReadQuaternion(),
				scale = reader.ReadVector3(),
				payload = reader.ReadArraySegmentAndSize()
			};
		}

		public static SpawnFlags _Read_Mirror_002ESpawnFlags(NetworkReader reader)
		{
			return (SpawnFlags)NetworkReaderExtensions.ReadByte(reader);
		}

		public static void _Write_Mirror_002ESpawnMessage(NetworkWriter writer, SpawnMessage value)
		{
			writer.WriteVarUInt(value.netId);
			_Write_Mirror_002ESpawnFlags(writer, value.spawnFlags);
			writer.WriteVarULong(value.sceneId);
			writer.WriteVarUInt(value.assetId);
			writer.WriteVector3(value.position);
			writer.WriteQuaternion(value.rotation);
			writer.WriteVector3(value.scale);
			writer.WriteArraySegmentAndSize(value.payload);
		}

		public static void _Write_Mirror_002ESpawnFlags(NetworkWriter writer, SpawnFlags value)
		{
			NetworkWriterExtensions.WriteByte(writer, (byte)value);
		}

		public static ChangeOwnerMessage _Read_Mirror_002EChangeOwnerMessage(NetworkReader reader)
		{
			return new ChangeOwnerMessage
			{
				netId = reader.ReadVarUInt(),
				spawnFlags = _Read_Mirror_002ESpawnFlags(reader)
			};
		}

		public static void _Write_Mirror_002EChangeOwnerMessage(NetworkWriter writer, ChangeOwnerMessage value)
		{
			writer.WriteVarUInt(value.netId);
			_Write_Mirror_002ESpawnFlags(writer, value.spawnFlags);
		}

		public static ObjectSpawnStartedMessage _Read_Mirror_002EObjectSpawnStartedMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002EObjectSpawnStartedMessage(NetworkWriter writer, ObjectSpawnStartedMessage value)
		{
		}

		public static ObjectSpawnFinishedMessage _Read_Mirror_002EObjectSpawnFinishedMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002EObjectSpawnFinishedMessage(NetworkWriter writer, ObjectSpawnFinishedMessage value)
		{
		}

		public static ObjectDestroyMessage _Read_Mirror_002EObjectDestroyMessage(NetworkReader reader)
		{
			return new ObjectDestroyMessage
			{
				netId = reader.ReadVarUInt()
			};
		}

		public static void _Write_Mirror_002EObjectDestroyMessage(NetworkWriter writer, ObjectDestroyMessage value)
		{
			writer.WriteVarUInt(value.netId);
		}

		public static ObjectHideMessage _Read_Mirror_002EObjectHideMessage(NetworkReader reader)
		{
			return new ObjectHideMessage
			{
				netId = reader.ReadVarUInt()
			};
		}

		public static void _Write_Mirror_002EObjectHideMessage(NetworkWriter writer, ObjectHideMessage value)
		{
			writer.WriteVarUInt(value.netId);
		}

		public static EntityStateMessage _Read_Mirror_002EEntityStateMessage(NetworkReader reader)
		{
			return new EntityStateMessage
			{
				netId = reader.ReadVarUInt(),
				payload = reader.ReadArraySegmentAndSize()
			};
		}

		public static void _Write_Mirror_002EEntityStateMessage(NetworkWriter writer, EntityStateMessage value)
		{
			writer.WriteVarUInt(value.netId);
			writer.WriteArraySegmentAndSize(value.payload);
		}

		public static NetworkPingMessage _Read_Mirror_002ENetworkPingMessage(NetworkReader reader)
		{
			return new NetworkPingMessage
			{
				localTime = reader.ReadDouble(),
				predictedTimeAdjusted = reader.ReadDouble()
			};
		}

		public static void _Write_Mirror_002ENetworkPingMessage(NetworkWriter writer, NetworkPingMessage value)
		{
			writer.WriteDouble(value.localTime);
			writer.WriteDouble(value.predictedTimeAdjusted);
		}

		public static NetworkPongMessage _Read_Mirror_002ENetworkPongMessage(NetworkReader reader)
		{
			return new NetworkPongMessage
			{
				localTime = reader.ReadDouble(),
				predictionErrorUnadjusted = reader.ReadDouble(),
				predictionErrorAdjusted = reader.ReadDouble()
			};
		}

		public static void _Write_Mirror_002ENetworkPongMessage(NetworkWriter writer, NetworkPongMessage value)
		{
			writer.WriteDouble(value.localTime);
			writer.WriteDouble(value.predictionErrorUnadjusted);
			writer.WriteDouble(value.predictionErrorAdjusted);
		}

		public static ServerRequest _Read_Mirror_002EDiscovery_002EServerRequest(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_Mirror_002EDiscovery_002EServerRequest(NetworkWriter writer, ServerRequest value)
		{
		}

		public static ServerResponse _Read_Mirror_002EDiscovery_002EServerResponse(NetworkReader reader)
		{
			return new ServerResponse
			{
				uri = reader.ReadUri(),
				serverId = reader.ReadVarLong()
			};
		}

		public static void _Write_Mirror_002EDiscovery_002EServerResponse(NetworkWriter writer, ServerResponse value)
		{
			writer.WriteUri(value.uri);
			writer.WriteVarLong(value.serverId);
		}

		public static WebSocketRelayMessage _Read_WebSocketRelayMessage(NetworkReader reader)
		{
			return new WebSocketRelayMessage
			{
				rawData = reader.ReadString(),
				messageType = reader.ReadString()
			};
		}

		public static void _Write_WebSocketRelayMessage(NetworkWriter writer, WebSocketRelayMessage value)
		{
			writer.WriteString(value.rawData);
			writer.WriteString(value.messageType);
		}

		public static SceneReadyMessage _Read_SceneReadyMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_SceneReadyMessage(NetworkWriter writer, SceneReadyMessage value)
		{
		}

		public static JoinGameMessage _Read_JoinGameMessage(NetworkReader reader)
		{
			return default;
		}

		public static void _Write_JoinGameMessage(NetworkWriter writer, JoinGameMessage value)
		{
		}

		public static ClientScenePlayReadyMessage _Read_ClientScenePlayReadyMessage(NetworkReader reader)
		{
			return new ClientScenePlayReadyMessage
			{
				epoch = reader.ReadVarInt()
			};
		}

		public static void _Write_ClientScenePlayReadyMessage(NetworkWriter writer, ClientScenePlayReadyMessage value)
		{
			writer.WriteVarInt(value.epoch);
		}

		public static CardData _Read_CardData(NetworkReader reader)
		{
			return new CardData
			{
				Suit = _Read_Suit(reader),
				Rank = _Read_Rank(reader)
			};
		}

		public static Suit _Read_Suit(NetworkReader reader)
		{
			return (Suit)reader.ReadVarInt();
		}

		public static Rank _Read_Rank(NetworkReader reader)
		{
			return (Rank)reader.ReadVarInt();
		}

		public static void _Write_CardData(NetworkWriter writer, CardData value)
		{
			_Write_Suit(writer, value.Suit);
			_Write_Rank(writer, value.Rank);
		}

		public static void _Write_Suit(NetworkWriter writer, Suit value)
		{
			writer.WriteVarInt((int)value);
		}

		public static void _Write_Rank(NetworkWriter writer, Rank value)
		{
			writer.WriteVarInt((int)value);
		}

		public static void _Write_Baccarat_002FCardAreaType(NetworkWriter writer, Baccarat.CardAreaType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static Baccarat.CardAreaType _Read_Baccarat_002FCardAreaType(NetworkReader reader)
		{
			return (Baccarat.CardAreaType)reader.ReadVarInt();
		}

		public static void _Write_BaccaratBetType(NetworkWriter writer, BaccaratBetType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static BaccaratBetType _Read_BaccaratBetType(NetworkReader reader)
		{
			return (BaccaratBetType)reader.ReadVarInt();
		}

		public static void _Write_Blackjack_002FCardAreaType(NetworkWriter writer, Blackjack.CardAreaType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static Blackjack.CardAreaType _Read_Blackjack_002FCardAreaType(NetworkReader reader)
		{
			return (Blackjack.CardAreaType)reader.ReadVarInt();
		}

		public static void _Write_UnityEngine_002EGradient(NetworkWriter writer, Gradient value)
		{
			if (value == null)
			{
				writer.WriteBool(value: false);
			}
			else
			{
				writer.WriteBool(value: true);
			}
		}

		public static Gradient _Read_UnityEngine_002EGradient(NetworkReader reader)
		{
			if (!reader.ReadBool())
			{
				return null;
			}
			return new Gradient();
		}

		public static void _Write_DragonTowerButton_002FButtonState(NetworkWriter writer, DragonTowerButton.ButtonState value)
		{
			writer.WriteVarInt((int)value);
		}

		public static DragonTowerButton.ButtonState _Read_DragonTowerButton_002FButtonState(NetworkReader reader)
		{
			return (DragonTowerButton.ButtonState)reader.ReadVarInt();
		}

		public static void _Write_BankMode(NetworkWriter writer, BankMode value)
		{
			writer.WriteVarInt((int)value);
		}

		public static BankMode _Read_BankMode(NetworkReader reader)
		{
			return (BankMode)reader.ReadVarInt();
		}

		public static void _Write_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E(NetworkWriter writer, List<int> value)
		{
			writer.WriteList(value);
		}

		public static List<int> _Read_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E(NetworkReader reader)
		{
			return reader.ReadList<int>();
		}

		public static void _Write_GameState(NetworkWriter writer, GameState value)
		{
			writer.WriteVarInt((int)value);
		}

		public static GameState _Read_GameState(NetworkReader reader)
		{
			return (GameState)reader.ReadVarInt();
		}

		public static void _Write_SFXParams_005B_005D(NetworkWriter writer, SFXParams[] value)
		{
			writer.WriteArray(value);
		}

		public static void _Write_SFXParams(NetworkWriter writer, SFXParams value)
		{
			writer.WriteString(value.name);
			writer.WriteFloat(value.value);
		}

		public static SFXParams[] _Read_SFXParams_005B_005D(NetworkReader reader)
		{
			return reader.ReadArray<SFXParams>();
		}

		public static SFXParams _Read_SFXParams(NetworkReader reader)
		{
			return new SFXParams
			{
				name = reader.ReadString(),
				value = reader.ReadFloat()
			};
		}

		public static void _Write_ChallengeSyncData_005B_005D(NetworkWriter writer, ChallengeSyncData[] value)
		{
			writer.WriteArray(value);
		}

		public static void _Write_ChallengeSyncData(NetworkWriter writer, ChallengeSyncData value)
		{
			writer.WriteVarInt(value.challengeID);
			writer.WriteFloat(value.progress);
			writer.WriteBool(value.isCompleted);
			writer.WriteBool(value.isClaimed);
			writer.WriteVarInt(value.completionCount);
			writer.WriteVarLong(value.lastBet);
			writer.WriteVarLong(value.lastPayout);
			_Write_CasinoGameType(writer, value.lastGameType);
			_Write_ConditionStateSyncData_005B_005D(writer, value.conditionStates);
		}

		public static void _Write_CasinoGameType(NetworkWriter writer, CasinoGameType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static void _Write_ConditionStateSyncData_005B_005D(NetworkWriter writer, ConditionStateSyncData[] value)
		{
			writer.WriteArray(value);
		}

		public static void _Write_ConditionStateSyncData(NetworkWriter writer, ConditionStateSyncData value)
		{
			writer.WriteVarInt(value.currentWinCount);
			writer.WriteVarInt(value.consecutiveWinCount);
			writer.WriteVarInt(value.currentLossCount);
			writer.WriteVarInt(value.consecutiveLossCount);
			writer.WriteVarLong(value.totalBetAmount);
			writer.WriteVarLong(value.totalPayoutAmount);
			writer.WriteVarLong(value.totalProfit);
			writer.WriteFloat(value.elapsedSinceStart);
			writer.WriteFloat(value.elapsedSinceLastGame);
		}

		public static ChallengeSyncData[] _Read_ChallengeSyncData_005B_005D(NetworkReader reader)
		{
			return reader.ReadArray<ChallengeSyncData>();
		}

		public static ChallengeSyncData _Read_ChallengeSyncData(NetworkReader reader)
		{
			return new ChallengeSyncData
			{
				challengeID = reader.ReadVarInt(),
				progress = reader.ReadFloat(),
				isCompleted = reader.ReadBool(),
				isClaimed = reader.ReadBool(),
				completionCount = reader.ReadVarInt(),
				lastBet = reader.ReadVarLong(),
				lastPayout = reader.ReadVarLong(),
				lastGameType = _Read_CasinoGameType(reader),
				conditionStates = _Read_ConditionStateSyncData_005B_005D(reader)
			};
		}

		public static CasinoGameType _Read_CasinoGameType(NetworkReader reader)
		{
			return (CasinoGameType)reader.ReadVarInt();
		}

		public static ConditionStateSyncData[] _Read_ConditionStateSyncData_005B_005D(NetworkReader reader)
		{
			return reader.ReadArray<ConditionStateSyncData>();
		}

		public static ConditionStateSyncData _Read_ConditionStateSyncData(NetworkReader reader)
		{
			return new ConditionStateSyncData
			{
				currentWinCount = reader.ReadVarInt(),
				consecutiveWinCount = reader.ReadVarInt(),
				currentLossCount = reader.ReadVarInt(),
				consecutiveLossCount = reader.ReadVarInt(),
				totalBetAmount = reader.ReadVarLong(),
				totalPayoutAmount = reader.ReadVarLong(),
				totalProfit = reader.ReadVarLong(),
				elapsedSinceStart = reader.ReadFloat(),
				elapsedSinceLastGame = reader.ReadFloat()
			};
		}

		public static void _Write_PlayerCreditsSnapshot_005B_005D(NetworkWriter writer, PlayerCreditsSnapshot[] value)
		{
			writer.WriteArray(value);
		}

		public static PlayerCreditsSnapshot[] _Read_PlayerCreditsSnapshot_005B_005D(NetworkReader reader)
		{
			return reader.ReadArray<PlayerCreditsSnapshot>();
		}

		public static void _Write_ChangeType(NetworkWriter writer, ChangeType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static ChangeType _Read_ChangeType(NetworkReader reader)
		{
			return (ChangeType)reader.ReadVarInt();
		}

		public static PayoutRecord _Read_PayoutRecord(NetworkReader reader)
		{
			if (!reader.ReadBool())
			{
				return null;
			}
			PayoutRecord payoutRecord = new PayoutRecord();
			payoutRecord.timestamp = reader.ReadFloat();
			payoutRecord.playerName = reader.ReadString();
			payoutRecord.playerProfile = reader.ReadNetworkBehaviour<PlayerProfile>();
			payoutRecord.bet = reader.ReadVarLong();
			payoutRecord.payout = reader.ReadVarLong();
			payoutRecord.profit = reader.ReadVarLong();
			payoutRecord.isWin = reader.ReadBool();
			payoutRecord.isLoss = reader.ReadBool();
			payoutRecord.gameType = _Read_CasinoGameType(reader);
			payoutRecord.gamePosition = reader.ReadVector3();
			return payoutRecord;
		}

		public static void _Write_PayoutRecord(NetworkWriter writer, PayoutRecord value)
		{
			if (value == null)
			{
				writer.WriteBool(value: false);
				return;
			}
			writer.WriteBool(value: true);
			writer.WriteFloat(value.timestamp);
			writer.WriteString(value.playerName);
			writer.WriteNetworkBehaviour(value.playerProfile);
			writer.WriteVarLong(value.bet);
			writer.WriteVarLong(value.payout);
			writer.WriteVarLong(value.profit);
			writer.WriteBool(value.isWin);
			writer.WriteBool(value.isLoss);
			_Write_CasinoGameType(writer, value.gameType);
			writer.WriteVector3(value.gamePosition);
		}

		public static void _Write_PlayerUpgradeType(NetworkWriter writer, PlayerUpgradeType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static PlayerUpgradeType _Read_PlayerUpgradeType(NetworkReader reader)
		{
			return (PlayerUpgradeType)reader.ReadVarInt();
		}

		public static void _Write_NPC_002FNPCState(NetworkWriter writer, NPC.NPCState value)
		{
			writer.WriteVarInt((int)value);
		}

		public static NPC.NPCState _Read_NPC_002FNPCState(NetworkReader reader)
		{
			return (NPC.NPCState)reader.ReadVarInt();
		}

		public static PlayerBuffType _Read_PlayerBuffType(NetworkReader reader)
		{
			return (PlayerBuffType)reader.ReadVarInt();
		}

		public static void _Write_PlayerBuffType(NetworkWriter writer, PlayerBuffType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static void _Write_PlayerController_002FPlayerState(NetworkWriter writer, PlayerController.PlayerState value)
		{
			writer.WriteVarInt((int)value);
		}

		public static PlayerController.PlayerState _Read_PlayerController_002FPlayerState(NetworkReader reader)
		{
			return (PlayerController.PlayerState)reader.ReadVarInt();
		}

		public static void _Write_CosmeticType(NetworkWriter writer, CosmeticType value)
		{
			writer.WriteVarInt((int)value);
		}

		public static CosmeticType _Read_CosmeticType(NetworkReader reader)
		{
			return (CosmeticType)reader.ReadVarInt();
		}

		public static void _Write_VoipManipulationManager_002FVoipFX(NetworkWriter writer, VoipManipulationManager.VoipFX value)
		{
			writer.WriteVarInt((int)value);
		}

		public static VoipManipulationManager.VoipFX _Read_VoipManipulationManager_002FVoipFX(NetworkReader reader)
		{
			return (VoipManipulationManager.VoipFX)reader.ReadVarInt();
		}

		public static void _Write_UnityEngine_002EKeyCode(NetworkWriter writer, KeyCode value)
		{
			writer.WriteVarInt((int)value);
		}

		public static KeyCode _Read_UnityEngine_002EKeyCode(NetworkReader reader)
		{
			return (KeyCode)reader.ReadVarInt();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void InitReadWriters()
		{
			Writer<byte>.write = NetworkWriterExtensions.WriteByte;
			Writer<byte?>.write = NetworkWriterExtensions.WriteByteNullable;
			Writer<sbyte>.write = NetworkWriterExtensions.WriteSByte;
			Writer<sbyte?>.write = NetworkWriterExtensions.WriteSByteNullable;
			Writer<char>.write = NetworkWriterExtensions.WriteChar;
			Writer<char?>.write = NetworkWriterExtensions.WriteCharNullable;
			Writer<bool>.write = NetworkWriterExtensions.WriteBool;
			Writer<bool?>.write = NetworkWriterExtensions.WriteBoolNullable;
			Writer<short>.write = NetworkWriterExtensions.WriteShort;
			Writer<short?>.write = NetworkWriterExtensions.WriteShortNullable;
			Writer<ushort>.write = NetworkWriterExtensions.WriteUShort;
			Writer<ushort?>.write = NetworkWriterExtensions.WriteUShortNullable;
			Writer<int>.write = NetworkWriterExtensions.WriteVarInt;
			Writer<int?>.write = NetworkWriterExtensions.WriteIntNullable;
			Writer<uint>.write = NetworkWriterExtensions.WriteVarUInt;
			Writer<uint?>.write = NetworkWriterExtensions.WriteUIntNullable;
			Writer<long>.write = NetworkWriterExtensions.WriteVarLong;
			Writer<long?>.write = NetworkWriterExtensions.WriteLongNullable;
			Writer<ulong>.write = NetworkWriterExtensions.WriteVarULong;
			Writer<ulong?>.write = NetworkWriterExtensions.WriteULongNullable;
			Writer<float>.write = NetworkWriterExtensions.WriteFloat;
			Writer<float?>.write = NetworkWriterExtensions.WriteFloatNullable;
			Writer<double>.write = NetworkWriterExtensions.WriteDouble;
			Writer<double?>.write = NetworkWriterExtensions.WriteDoubleNullable;
			Writer<decimal>.write = NetworkWriterExtensions.WriteDecimal;
			Writer<decimal?>.write = NetworkWriterExtensions.WriteDecimalNullable;
			Writer<global::System.Half>.write = NetworkWriterExtensions.WriteHalf;
			Writer<string>.write = NetworkWriterExtensions.WriteString;
			Writer<byte[]>.write = NetworkWriterExtensions.WriteBytesAndSize;
			Writer<ArraySegment<byte>>.write = NetworkWriterExtensions.WriteArraySegmentAndSize;
			Writer<Vector2>.write = NetworkWriterExtensions.WriteVector2;
			Writer<Vector2?>.write = NetworkWriterExtensions.WriteVector2Nullable;
			Writer<Vector3>.write = NetworkWriterExtensions.WriteVector3;
			Writer<Vector3?>.write = NetworkWriterExtensions.WriteVector3Nullable;
			Writer<Vector4>.write = NetworkWriterExtensions.WriteVector4;
			Writer<Vector4?>.write = NetworkWriterExtensions.WriteVector4Nullable;
			Writer<Vector2Int>.write = NetworkWriterExtensions.WriteVector2Int;
			Writer<Vector2Int?>.write = NetworkWriterExtensions.WriteVector2IntNullable;
			Writer<Vector3Int>.write = NetworkWriterExtensions.WriteVector3Int;
			Writer<Vector3Int?>.write = NetworkWriterExtensions.WriteVector3IntNullable;
			Writer<Color>.write = NetworkWriterExtensions.WriteColor;
			Writer<Color?>.write = NetworkWriterExtensions.WriteColorNullable;
			Writer<Color32>.write = NetworkWriterExtensions.WriteColor32;
			Writer<Color32?>.write = NetworkWriterExtensions.WriteColor32Nullable;
			Writer<Quaternion>.write = NetworkWriterExtensions.WriteQuaternion;
			Writer<Quaternion?>.write = NetworkWriterExtensions.WriteQuaternionNullable;
			Writer<Rect>.write = NetworkWriterExtensions.WriteRect;
			Writer<Rect?>.write = NetworkWriterExtensions.WriteRectNullable;
			Writer<Plane>.write = NetworkWriterExtensions.WritePlane;
			Writer<Plane?>.write = NetworkWriterExtensions.WritePlaneNullable;
			Writer<Ray>.write = NetworkWriterExtensions.WriteRay;
			Writer<Ray?>.write = NetworkWriterExtensions.WriteRayNullable;
			Writer<LayerMask>.write = NetworkWriterExtensions.WriteLayerMask;
			Writer<LayerMask?>.write = NetworkWriterExtensions.WriteLayerMaskNullable;
			Writer<Matrix4x4>.write = NetworkWriterExtensions.WriteMatrix4x4;
			Writer<Matrix4x4?>.write = NetworkWriterExtensions.WriteMatrix4x4Nullable;
			Writer<Guid>.write = NetworkWriterExtensions.WriteGuid;
			Writer<Guid?>.write = NetworkWriterExtensions.WriteGuidNullable;
			Writer<NetworkIdentity>.write = NetworkWriterExtensions.WriteNetworkIdentity;
			Writer<NetworkBehaviour>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<Transform>.write = NetworkWriterExtensions.WriteTransform;
			Writer<GameObject>.write = NetworkWriterExtensions.WriteGameObject;
			Writer<Uri>.write = NetworkWriterExtensions.WriteUri;
			Writer<Texture2D>.write = NetworkWriterExtensions.WriteTexture2D;
			Writer<Sprite>.write = NetworkWriterExtensions.WriteSprite;
			Writer<DateTime>.write = NetworkWriterExtensions.WriteDateTime;
			Writer<DateTime?>.write = NetworkWriterExtensions.WriteDateTimeNullable;
			Writer<TimeSnapshotMessage>.write = _Write_Mirror_002ETimeSnapshotMessage;
			Writer<ReadyMessage>.write = _Write_Mirror_002EReadyMessage;
			Writer<NotReadyMessage>.write = _Write_Mirror_002ENotReadyMessage;
			Writer<AddPlayerMessage>.write = _Write_Mirror_002EAddPlayerMessage;
			Writer<SceneMessage>.write = _Write_Mirror_002ESceneMessage;
			Writer<SceneOperation>.write = _Write_Mirror_002ESceneOperation;
			Writer<CommandMessage>.write = _Write_Mirror_002ECommandMessage;
			Writer<RpcMessage>.write = _Write_Mirror_002ERpcMessage;
			Writer<SpawnMessage>.write = _Write_Mirror_002ESpawnMessage;
			Writer<SpawnFlags>.write = _Write_Mirror_002ESpawnFlags;
			Writer<ChangeOwnerMessage>.write = _Write_Mirror_002EChangeOwnerMessage;
			Writer<ObjectSpawnStartedMessage>.write = _Write_Mirror_002EObjectSpawnStartedMessage;
			Writer<ObjectSpawnFinishedMessage>.write = _Write_Mirror_002EObjectSpawnFinishedMessage;
			Writer<ObjectDestroyMessage>.write = _Write_Mirror_002EObjectDestroyMessage;
			Writer<ObjectHideMessage>.write = _Write_Mirror_002EObjectHideMessage;
			Writer<EntityStateMessage>.write = _Write_Mirror_002EEntityStateMessage;
			Writer<NetworkPingMessage>.write = _Write_Mirror_002ENetworkPingMessage;
			Writer<NetworkPongMessage>.write = _Write_Mirror_002ENetworkPongMessage;
			Writer<SyncData>.write = SyncDataReaderWriter.WriteSyncData;
			Writer<PredictedSyncData>.write = PredictedSyncDataReadWrite.WritePredictedSyncData;
			Writer<ServerRequest>.write = _Write_Mirror_002EDiscovery_002EServerRequest;
			Writer<ServerResponse>.write = _Write_Mirror_002EDiscovery_002EServerResponse;
			Writer<PlayerCreditsSnapshot>.write = PlayerCreditsSnapshotSerialization.WritePlayerCreditsSnapshot;
			Writer<NetworkStateMirror>.write = SyncProjectilesMessageFunctions.Serialize;
			Writer<DissonanceNetworkMessage>.write = DissonanceNetworkMessageExtensions.Serialize;
			Writer<WebSocketRelayMessage>.write = _Write_WebSocketRelayMessage;
			Writer<SceneReadyMessage>.write = _Write_SceneReadyMessage;
			Writer<JoinGameMessage>.write = _Write_JoinGameMessage;
			Writer<ClientScenePlayReadyMessage>.write = _Write_ClientScenePlayReadyMessage;
			Writer<CardData>.write = _Write_CardData;
			Writer<Suit>.write = _Write_Suit;
			Writer<Rank>.write = _Write_Rank;
			Writer<Baccarat.CardAreaType>.write = _Write_Baccarat_002FCardAreaType;
			Writer<BaccaratBetType>.write = _Write_BaccaratBetType;
			Writer<Blackjack.CardAreaType>.write = _Write_Blackjack_002FCardAreaType;
			Writer<Gradient>.write = _Write_UnityEngine_002EGradient;
			Writer<PlayerInteract>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<DragonTowerButton.ButtonState>.write = _Write_DragonTowerButton_002FButtonState;
			Writer<BankMode>.write = _Write_BankMode;
			Writer<List<int>>.write = _Write_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E;
			Writer<SlotReel>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<GameState>.write = _Write_GameState;
			Writer<PlayerInventory>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<PlayerController>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<NPC>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<Item>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<SFXParams[]>.write = _Write_SFXParams_005B_005D;
			Writer<SFXParams>.write = _Write_SFXParams;
			Writer<GameBase>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<ChallengeSyncData[]>.write = _Write_ChallengeSyncData_005B_005D;
			Writer<ChallengeSyncData>.write = _Write_ChallengeSyncData;
			Writer<CasinoGameType>.write = _Write_CasinoGameType;
			Writer<ConditionStateSyncData[]>.write = _Write_ConditionStateSyncData_005B_005D;
			Writer<ConditionStateSyncData>.write = _Write_ConditionStateSyncData;
			Writer<PlayerCreditsSnapshot[]>.write = _Write_PlayerCreditsSnapshot_005B_005D;
			Writer<PlayerProfile>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<ChangeType>.write = _Write_ChangeType;
			Writer<PayoutRecord>.write = _Write_PayoutRecord;
			Writer<PlayerUpgradeType>.write = _Write_PlayerUpgradeType;
			Writer<NPC.NPCState>.write = _Write_NPC_002FNPCState;
			Writer<PlayerBuffType>.write = _Write_PlayerBuffType;
			Writer<PlayerCarry>.write = NetworkWriterExtensions.WriteNetworkBehaviour;
			Writer<PlayerController.PlayerState>.write = _Write_PlayerController_002FPlayerState;
			Writer<CosmeticType>.write = _Write_CosmeticType;
			Writer<VoipManipulationManager.VoipFX>.write = _Write_VoipManipulationManager_002FVoipFX;
			Writer<KeyCode>.write = _Write_UnityEngine_002EKeyCode;
			Reader<byte>.read = NetworkReaderExtensions.ReadByte;
			Reader<byte?>.read = NetworkReaderExtensions.ReadByteNullable;
			Reader<sbyte>.read = NetworkReaderExtensions.ReadSByte;
			Reader<sbyte?>.read = NetworkReaderExtensions.ReadSByteNullable;
			Reader<char>.read = NetworkReaderExtensions.ReadChar;
			Reader<char?>.read = NetworkReaderExtensions.ReadCharNullable;
			Reader<bool>.read = NetworkReaderExtensions.ReadBool;
			Reader<bool?>.read = NetworkReaderExtensions.ReadBoolNullable;
			Reader<short>.read = NetworkReaderExtensions.ReadShort;
			Reader<short?>.read = NetworkReaderExtensions.ReadShortNullable;
			Reader<ushort>.read = NetworkReaderExtensions.ReadUShort;
			Reader<ushort?>.read = NetworkReaderExtensions.ReadUShortNullable;
			Reader<int>.read = NetworkReaderExtensions.ReadVarInt;
			Reader<int?>.read = NetworkReaderExtensions.ReadIntNullable;
			Reader<uint>.read = NetworkReaderExtensions.ReadVarUInt;
			Reader<uint?>.read = NetworkReaderExtensions.ReadUIntNullable;
			Reader<long>.read = NetworkReaderExtensions.ReadVarLong;
			Reader<long?>.read = NetworkReaderExtensions.ReadLongNullable;
			Reader<ulong>.read = NetworkReaderExtensions.ReadVarULong;
			Reader<ulong?>.read = NetworkReaderExtensions.ReadULongNullable;
			Reader<float>.read = NetworkReaderExtensions.ReadFloat;
			Reader<float?>.read = NetworkReaderExtensions.ReadFloatNullable;
			Reader<double>.read = NetworkReaderExtensions.ReadDouble;
			Reader<double?>.read = NetworkReaderExtensions.ReadDoubleNullable;
			Reader<decimal>.read = NetworkReaderExtensions.ReadDecimal;
			Reader<decimal?>.read = NetworkReaderExtensions.ReadDecimalNullable;
			Reader<global::System.Half>.read = NetworkReaderExtensions.ReadHalf;
			Reader<string>.read = NetworkReaderExtensions.ReadString;
			Reader<byte[]>.read = NetworkReaderExtensions.ReadBytesAndSize;
			Reader<ArraySegment<byte>>.read = NetworkReaderExtensions.ReadArraySegmentAndSize;
			Reader<Vector2>.read = NetworkReaderExtensions.ReadVector2;
			Reader<Vector2?>.read = NetworkReaderExtensions.ReadVector2Nullable;
			Reader<Vector3>.read = NetworkReaderExtensions.ReadVector3;
			Reader<Vector3?>.read = NetworkReaderExtensions.ReadVector3Nullable;
			Reader<Vector4>.read = NetworkReaderExtensions.ReadVector4;
			Reader<Vector4?>.read = NetworkReaderExtensions.ReadVector4Nullable;
			Reader<Vector2Int>.read = NetworkReaderExtensions.ReadVector2Int;
			Reader<Vector2Int?>.read = NetworkReaderExtensions.ReadVector2IntNullable;
			Reader<Vector3Int>.read = NetworkReaderExtensions.ReadVector3Int;
			Reader<Vector3Int?>.read = NetworkReaderExtensions.ReadVector3IntNullable;
			Reader<Color>.read = NetworkReaderExtensions.ReadColor;
			Reader<Color?>.read = NetworkReaderExtensions.ReadColorNullable;
			Reader<Color32>.read = NetworkReaderExtensions.ReadColor32;
			Reader<Color32?>.read = NetworkReaderExtensions.ReadColor32Nullable;
			Reader<Quaternion>.read = NetworkReaderExtensions.ReadQuaternion;
			Reader<Quaternion?>.read = NetworkReaderExtensions.ReadQuaternionNullable;
			Reader<Rect>.read = NetworkReaderExtensions.ReadRect;
			Reader<Rect?>.read = NetworkReaderExtensions.ReadRectNullable;
			Reader<Plane>.read = NetworkReaderExtensions.ReadPlane;
			Reader<Plane?>.read = NetworkReaderExtensions.ReadPlaneNullable;
			Reader<Ray>.read = NetworkReaderExtensions.ReadRay;
			Reader<Ray?>.read = NetworkReaderExtensions.ReadRayNullable;
			Reader<LayerMask>.read = NetworkReaderExtensions.ReadLayerMask;
			Reader<LayerMask?>.read = NetworkReaderExtensions.ReadLayerMaskNullable;
			Reader<Matrix4x4>.read = NetworkReaderExtensions.ReadMatrix4x4;
			Reader<Matrix4x4?>.read = NetworkReaderExtensions.ReadMatrix4x4Nullable;
			Reader<Guid>.read = NetworkReaderExtensions.ReadGuid;
			Reader<Guid?>.read = NetworkReaderExtensions.ReadGuidNullable;
			Reader<NetworkIdentity>.read = NetworkReaderExtensions.ReadNetworkIdentity;
			Reader<NetworkBehaviour>.read = NetworkReaderExtensions.ReadNetworkBehaviour;
			Reader<NetworkBehaviourSyncVar>.read = NetworkReaderExtensions.ReadNetworkBehaviourSyncVar;
			Reader<Transform>.read = NetworkReaderExtensions.ReadTransform;
			Reader<GameObject>.read = NetworkReaderExtensions.ReadGameObject;
			Reader<Uri>.read = NetworkReaderExtensions.ReadUri;
			Reader<Texture2D>.read = NetworkReaderExtensions.ReadTexture2D;
			Reader<Sprite>.read = NetworkReaderExtensions.ReadSprite;
			Reader<DateTime>.read = NetworkReaderExtensions.ReadDateTime;
			Reader<DateTime?>.read = NetworkReaderExtensions.ReadDateTimeNullable;
			Reader<TimeSnapshotMessage>.read = _Read_Mirror_002ETimeSnapshotMessage;
			Reader<ReadyMessage>.read = _Read_Mirror_002EReadyMessage;
			Reader<NotReadyMessage>.read = _Read_Mirror_002ENotReadyMessage;
			Reader<AddPlayerMessage>.read = _Read_Mirror_002EAddPlayerMessage;
			Reader<SceneMessage>.read = _Read_Mirror_002ESceneMessage;
			Reader<SceneOperation>.read = _Read_Mirror_002ESceneOperation;
			Reader<CommandMessage>.read = _Read_Mirror_002ECommandMessage;
			Reader<RpcMessage>.read = _Read_Mirror_002ERpcMessage;
			Reader<SpawnMessage>.read = _Read_Mirror_002ESpawnMessage;
			Reader<SpawnFlags>.read = _Read_Mirror_002ESpawnFlags;
			Reader<ChangeOwnerMessage>.read = _Read_Mirror_002EChangeOwnerMessage;
			Reader<ObjectSpawnStartedMessage>.read = _Read_Mirror_002EObjectSpawnStartedMessage;
			Reader<ObjectSpawnFinishedMessage>.read = _Read_Mirror_002EObjectSpawnFinishedMessage;
			Reader<ObjectDestroyMessage>.read = _Read_Mirror_002EObjectDestroyMessage;
			Reader<ObjectHideMessage>.read = _Read_Mirror_002EObjectHideMessage;
			Reader<EntityStateMessage>.read = _Read_Mirror_002EEntityStateMessage;
			Reader<NetworkPingMessage>.read = _Read_Mirror_002ENetworkPingMessage;
			Reader<NetworkPongMessage>.read = _Read_Mirror_002ENetworkPongMessage;
			Reader<SyncData>.read = SyncDataReaderWriter.ReadSyncData;
			Reader<PredictedSyncData>.read = PredictedSyncDataReadWrite.ReadPredictedSyncData;
			Reader<ServerRequest>.read = _Read_Mirror_002EDiscovery_002EServerRequest;
			Reader<ServerResponse>.read = _Read_Mirror_002EDiscovery_002EServerResponse;
			Reader<PlayerCreditsSnapshot>.read = PlayerCreditsSnapshotSerialization.ReadPlayerCreditsSnapshot;
			Reader<NetworkStateMirror>.read = SyncProjectilesMessageFunctions.Deserialize;
			Reader<DissonanceNetworkMessage>.read = DissonanceNetworkMessageExtensions.Deserialize;
			Reader<WebSocketRelayMessage>.read = _Read_WebSocketRelayMessage;
			Reader<SceneReadyMessage>.read = _Read_SceneReadyMessage;
			Reader<JoinGameMessage>.read = _Read_JoinGameMessage;
			Reader<ClientScenePlayReadyMessage>.read = _Read_ClientScenePlayReadyMessage;
			Reader<CardData>.read = _Read_CardData;
			Reader<Suit>.read = _Read_Suit;
			Reader<Rank>.read = _Read_Rank;
			Reader<Baccarat.CardAreaType>.read = _Read_Baccarat_002FCardAreaType;
			Reader<BaccaratBetType>.read = _Read_BaccaratBetType;
			Reader<Blackjack.CardAreaType>.read = _Read_Blackjack_002FCardAreaType;
			Reader<Gradient>.read = _Read_UnityEngine_002EGradient;
			Reader<PlayerInteract>.read = NetworkReaderExtensions.ReadNetworkBehaviour<PlayerInteract>;
			Reader<DragonTowerButton.ButtonState>.read = _Read_DragonTowerButton_002FButtonState;
			Reader<BankMode>.read = _Read_BankMode;
			Reader<List<int>>.read = _Read_System_002ECollections_002EGeneric_002EList_00601_003CSystem_002EInt32_003E;
			Reader<SlotReel>.read = NetworkReaderExtensions.ReadNetworkBehaviour<SlotReel>;
			Reader<GameState>.read = _Read_GameState;
			Reader<PlayerInventory>.read = NetworkReaderExtensions.ReadNetworkBehaviour<PlayerInventory>;
			Reader<PlayerController>.read = NetworkReaderExtensions.ReadNetworkBehaviour<PlayerController>;
			Reader<NPC>.read = NetworkReaderExtensions.ReadNetworkBehaviour<NPC>;
			Reader<Item>.read = NetworkReaderExtensions.ReadNetworkBehaviour<Item>;
			Reader<SFXParams[]>.read = _Read_SFXParams_005B_005D;
			Reader<SFXParams>.read = _Read_SFXParams;
			Reader<GameBase>.read = NetworkReaderExtensions.ReadNetworkBehaviour<GameBase>;
			Reader<ChallengeSyncData[]>.read = _Read_ChallengeSyncData_005B_005D;
			Reader<ChallengeSyncData>.read = _Read_ChallengeSyncData;
			Reader<CasinoGameType>.read = _Read_CasinoGameType;
			Reader<ConditionStateSyncData[]>.read = _Read_ConditionStateSyncData_005B_005D;
			Reader<ConditionStateSyncData>.read = _Read_ConditionStateSyncData;
			Reader<PlayerCreditsSnapshot[]>.read = _Read_PlayerCreditsSnapshot_005B_005D;
			Reader<PlayerProfile>.read = NetworkReaderExtensions.ReadNetworkBehaviour<PlayerProfile>;
			Reader<ChangeType>.read = _Read_ChangeType;
			Reader<PayoutRecord>.read = _Read_PayoutRecord;
			Reader<PlayerUpgradeType>.read = _Read_PlayerUpgradeType;
			Reader<NPC.NPCState>.read = _Read_NPC_002FNPCState;
			Reader<PlayerBuffType>.read = _Read_PlayerBuffType;
			Reader<PlayerCarry>.read = NetworkReaderExtensions.ReadNetworkBehaviour<PlayerCarry>;
			Reader<PlayerController.PlayerState>.read = _Read_PlayerController_002FPlayerState;
			Reader<CosmeticType>.read = _Read_CosmeticType;
			Reader<VoipManipulationManager.VoipFX>.read = _Read_VoipManipulationManager_002FVoipFX;
			Reader<KeyCode>.read = _Read_UnityEngine_002EKeyCode;
		}
	}
}
