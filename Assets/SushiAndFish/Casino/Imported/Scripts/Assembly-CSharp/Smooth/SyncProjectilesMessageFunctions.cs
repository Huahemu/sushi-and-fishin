namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Mirror;
using global::UnityEngine;

namespace Smooth
{
	public static class SyncProjectilesMessageFunctions
	{
		private const byte positionMask = 1;

		private const byte rotationMask = 2;

		private const byte scaleMask = 4;

		private const byte velocityMask = 8;

		private const byte angularVelocityMask = 16;

		private const byte atPositionalRestMask = 64;

		private const byte atRotationalRestMask = 128;

		public static void Serialize(this NetworkWriter writer, NetworkStateMirror msg)
		{
			SmoothSyncMirror smoothSync = msg.smoothSync;
			StateMirror state = msg.state;
			bool flag;
			bool flag2;
			bool flag3;
			bool flag4;
			bool flag5;
			bool atPositionalRest;
			bool atRotationalRest;
			if (NetworkServer.active && !smoothSync.hasControl)
			{
				flag = state.serverShouldRelayPosition;
				flag2 = state.serverShouldRelayRotation;
				flag3 = state.serverShouldRelayScale;
				flag4 = state.serverShouldRelayVelocity;
				flag5 = state.serverShouldRelayAngularVelocity;
				atPositionalRest = state.atPositionalRest;
				atRotationalRest = state.atRotationalRest;
			}
			else
			{
				flag = smoothSync.sendPosition;
				flag2 = smoothSync.sendRotation;
				flag3 = smoothSync.sendScale;
				flag4 = smoothSync.sendVelocity;
				flag5 = smoothSync.sendAngularVelocity;
				atPositionalRest = smoothSync.sendAtPositionalRestMessage;
				atRotationalRest = smoothSync.sendAtRotationalRestMessage;
			}
			if (!NetworkServer.active)
			{
				if (flag)
				{
					smoothSync.lastPositionWhenStateWasSent = state.position;
				}
				if (flag2)
				{
					smoothSync.lastRotationWhenStateWasSent = state.rotation;
				}
				if (flag3)
				{
					smoothSync.lastScaleWhenStateWasSent = state.scale;
				}
				if (flag4)
				{
					smoothSync.lastVelocityWhenStateWasSent = state.velocity;
				}
				if (flag5)
				{
					smoothSync.lastAngularVelocityWhenStateWasSent = state.angularVelocity;
				}
			}
			byte b = 0;
			b++;
			b++;
			b += 4;
			b += 4;
			b += 4;
			if (flag)
			{
				byte b2 = 4;
				if (smoothSync.isPositionCompressed)
				{
					b2 = 2;
				}
				if (smoothSync.isSyncingXPosition)
				{
					b += b2;
				}
				if (smoothSync.isSyncingYPosition)
				{
					b += b2;
				}
				if (smoothSync.isSyncingZPosition)
				{
					b += b2;
				}
			}
			if (flag2)
			{
				byte b3 = 4;
				if (smoothSync.isRotationCompressed)
				{
					b3 = 2;
				}
				if (smoothSync.isSyncingXRotation)
				{
					b += b3;
				}
				if (smoothSync.isSyncingYRotation)
				{
					b += b3;
				}
				if (smoothSync.isSyncingZRotation)
				{
					b += b3;
				}
			}
			if (flag3)
			{
				byte b4 = 4;
				if (smoothSync.isScaleCompressed)
				{
					b4 = 2;
				}
				if (smoothSync.isSyncingXScale)
				{
					b += b4;
				}
				if (smoothSync.isSyncingYScale)
				{
					b += b4;
				}
				if (smoothSync.isSyncingZScale)
				{
					b += b4;
				}
			}
			if (flag4)
			{
				byte b5 = 4;
				if (smoothSync.isVelocityCompressed)
				{
					b5 = 2;
				}
				if (smoothSync.isSyncingXVelocity)
				{
					b += b5;
				}
				if (smoothSync.isSyncingYVelocity)
				{
					b += b5;
				}
				if (smoothSync.isSyncingZVelocity)
				{
					b += b5;
				}
			}
			if (flag5)
			{
				byte b6 = 4;
				if (smoothSync.isAngularVelocityCompressed)
				{
					b6 = 2;
				}
				if (smoothSync.isSyncingXAngularVelocity)
				{
					b += b6;
				}
				if (smoothSync.isSyncingYAngularVelocity)
				{
					b += b6;
				}
				if (smoothSync.isSyncingZAngularVelocity)
				{
					b += b6;
				}
			}
			if (smoothSync.isSmoothingAuthorityChanges && NetworkServer.active)
			{
				b++;
			}
			if (smoothSync.automaticallyResetTime)
			{
				b++;
			}
			writer.WriteByte(b);
			writer.WriteByte(encodeSyncInformation(flag, flag2, flag3, flag4, flag5, atPositionalRest, atRotationalRest));
			writer.WriteNetworkIdentity(smoothSync.netID);
			writer.WriteUInt((uint)smoothSync.syncIndex);
			writer.WriteFloat(state.ownerTimestamp);
			if (flag)
			{
				if (smoothSync.isPositionCompressed)
				{
					if (smoothSync.isSyncingXPosition)
					{
						writer.WriteUShort(HalfHelper.Compress(state.position.x));
					}
					if (smoothSync.isSyncingYPosition)
					{
						writer.WriteUShort(HalfHelper.Compress(state.position.y));
					}
					if (smoothSync.isSyncingZPosition)
					{
						writer.WriteUShort(HalfHelper.Compress(state.position.z));
					}
				}
				else
				{
					if (smoothSync.isSyncingXPosition)
					{
						writer.WriteFloat(state.position.x);
					}
					if (smoothSync.isSyncingYPosition)
					{
						writer.WriteFloat(state.position.y);
					}
					if (smoothSync.isSyncingZPosition)
					{
						writer.WriteFloat(state.position.z);
					}
				}
			}
			if (flag2)
			{
				Vector3 eulerAngles = state.rotation.eulerAngles;
				if (smoothSync.isRotationCompressed)
				{
					if (smoothSync.isSyncingXRotation)
					{
						writer.WriteUShort(HalfHelper.Compress(eulerAngles.x * (MathF.PI / 180f)));
					}
					if (smoothSync.isSyncingYRotation)
					{
						writer.WriteUShort(HalfHelper.Compress(eulerAngles.y * (MathF.PI / 180f)));
					}
					if (smoothSync.isSyncingZRotation)
					{
						writer.WriteUShort(HalfHelper.Compress(eulerAngles.z * (MathF.PI / 180f)));
					}
				}
				else
				{
					if (smoothSync.isSyncingXRotation)
					{
						writer.WriteFloat(eulerAngles.x);
					}
					if (smoothSync.isSyncingYRotation)
					{
						writer.WriteFloat(eulerAngles.y);
					}
					if (smoothSync.isSyncingZRotation)
					{
						writer.WriteFloat(eulerAngles.z);
					}
				}
			}
			if (flag3)
			{
				if (smoothSync.isScaleCompressed)
				{
					if (smoothSync.isSyncingXScale)
					{
						writer.WriteUShort(HalfHelper.Compress(state.scale.x));
					}
					if (smoothSync.isSyncingYScale)
					{
						writer.WriteUShort(HalfHelper.Compress(state.scale.y));
					}
					if (smoothSync.isSyncingZScale)
					{
						writer.WriteUShort(HalfHelper.Compress(state.scale.z));
					}
				}
				else
				{
					if (smoothSync.isSyncingXScale)
					{
						writer.WriteFloat(state.scale.x);
					}
					if (smoothSync.isSyncingYScale)
					{
						writer.WriteFloat(state.scale.y);
					}
					if (smoothSync.isSyncingZScale)
					{
						writer.WriteFloat(state.scale.z);
					}
				}
			}
			if (flag4)
			{
				if (smoothSync.isVelocityCompressed)
				{
					if (smoothSync.isSyncingXVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.velocity.x));
					}
					if (smoothSync.isSyncingYVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.velocity.y));
					}
					if (smoothSync.isSyncingZVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.velocity.z));
					}
				}
				else
				{
					if (smoothSync.isSyncingXVelocity)
					{
						writer.WriteFloat(state.velocity.x);
					}
					if (smoothSync.isSyncingYVelocity)
					{
						writer.WriteFloat(state.velocity.y);
					}
					if (smoothSync.isSyncingZVelocity)
					{
						writer.WriteFloat(state.velocity.z);
					}
				}
			}
			if (flag5)
			{
				if (smoothSync.isAngularVelocityCompressed)
				{
					if (smoothSync.isSyncingXAngularVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.angularVelocity.x * (MathF.PI / 180f)));
					}
					if (smoothSync.isSyncingYAngularVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.angularVelocity.y * (MathF.PI / 180f)));
					}
					if (smoothSync.isSyncingZAngularVelocity)
					{
						writer.WriteUShort(HalfHelper.Compress(state.angularVelocity.z * (MathF.PI / 180f)));
					}
				}
				else
				{
					if (smoothSync.isSyncingXAngularVelocity)
					{
						writer.WriteFloat(state.angularVelocity.x);
					}
					if (smoothSync.isSyncingYAngularVelocity)
					{
						writer.WriteFloat(state.angularVelocity.y);
					}
					if (smoothSync.isSyncingZAngularVelocity)
					{
						writer.WriteFloat(state.angularVelocity.z);
					}
				}
			}
			if (smoothSync.isSmoothingAuthorityChanges && NetworkServer.active)
			{
				writer.WriteByte((byte)smoothSync.ownerChangeIndicator);
			}
			if (smoothSync.automaticallyResetTime)
			{
				writer.WriteByte((byte)state.localTimeResetIndicator);
			}
		}

		public static NetworkStateMirror Deserialize(this NetworkReader reader)
		{
			NetworkStateMirror result = new NetworkStateMirror
			{
				state = new StateMirror()
			};
			StateMirror state = result.state;
			byte b = 0;
			byte b2 = reader.ReadByte();
			b++;
			byte syncInformation = reader.ReadByte();
			b++;
			bool flag = shouldSyncPosition(syncInformation);
			bool flag2 = shouldSyncRotation(syncInformation);
			bool flag3 = shouldSyncScale(syncInformation);
			bool flag4 = shouldSyncVelocity(syncInformation);
			bool flag5 = shouldSyncAngularVelocity(syncInformation);
			state.atPositionalRest = shouldBeAtPositionalRest(syncInformation);
			state.atRotationalRest = shouldBeAtRotationalRest(syncInformation);
			NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
			b += 4;
			if (networkIdentity == null)
			{
				reader.ReadBytes(b2 - b);
				return result;
			}
			GameObject gameObject = networkIdentity.gameObject;
			if (!gameObject)
			{
				reader.ReadBytes(b2 - b);
				return result;
			}
			result.smoothSync = gameObject.GetComponent<SmoothSyncMirror>();
			if (!result.smoothSync)
			{
				reader.ReadBytes(b2 - b);
				return result;
			}
			int num = (int)reader.ReadUInt();
			for (int i = 0; i < result.smoothSync.childObjectSmoothSyncs.Length; i++)
			{
				if (result.smoothSync.childObjectSmoothSyncs[i].syncIndex == num)
				{
					result.smoothSync = result.smoothSync.childObjectSmoothSyncs[i];
					break;
				}
			}
			state.ownerTimestamp = reader.ReadFloat();
			SmoothSyncMirror smoothSync = result.smoothSync;
			state.receivedTimestamp = smoothSync.localTime;
			if (NetworkServer.active && !smoothSync.hasControl)
			{
				state.serverShouldRelayPosition = flag;
				state.serverShouldRelayRotation = flag2;
				state.serverShouldRelayScale = flag3;
				state.serverShouldRelayVelocity = flag4;
				state.serverShouldRelayAngularVelocity = flag5;
			}
			if ((float)smoothSync.receivedStatesCounter < smoothSync.sendRate)
			{
				smoothSync.receivedStatesCounter++;
			}
			if (flag)
			{
				if (smoothSync.isPositionCompressed)
				{
					if (smoothSync.isSyncingXPosition)
					{
						state.position.x = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingYPosition)
					{
						state.position.y = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingZPosition)
					{
						state.position.z = HalfHelper.Decompress(reader.ReadUShort());
					}
				}
				else
				{
					if (smoothSync.isSyncingXPosition)
					{
						state.position.x = reader.ReadFloat();
					}
					if (smoothSync.isSyncingYPosition)
					{
						state.position.y = reader.ReadFloat();
					}
					if (smoothSync.isSyncingZPosition)
					{
						state.position.z = reader.ReadFloat();
					}
				}
			}
			else if (smoothSync.stateCount > 0)
			{
				state.position = smoothSync.stateBuffer[0].position;
			}
			else
			{
				state.position = smoothSync.getPosition();
			}
			if (flag2)
			{
				state.reusableRotationVector = Vector3.zero;
				if (smoothSync.isRotationCompressed)
				{
					if (smoothSync.isSyncingXRotation)
					{
						state.reusableRotationVector.x = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.x *= 57.29578f;
					}
					if (smoothSync.isSyncingYRotation)
					{
						state.reusableRotationVector.y = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.y *= 57.29578f;
					}
					if (smoothSync.isSyncingZRotation)
					{
						state.reusableRotationVector.z = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.z *= 57.29578f;
					}
					state.rotation = Quaternion.Euler(state.reusableRotationVector);
				}
				else
				{
					if (smoothSync.isSyncingXRotation)
					{
						state.reusableRotationVector.x = reader.ReadFloat();
					}
					if (smoothSync.isSyncingYRotation)
					{
						state.reusableRotationVector.y = reader.ReadFloat();
					}
					if (smoothSync.isSyncingZRotation)
					{
						state.reusableRotationVector.z = reader.ReadFloat();
					}
					state.rotation = Quaternion.Euler(state.reusableRotationVector);
				}
			}
			else if (smoothSync.stateCount > 0)
			{
				state.rotation = smoothSync.stateBuffer[0].rotation;
			}
			else
			{
				state.rotation = smoothSync.getRotation();
			}
			if (flag3)
			{
				if (smoothSync.isScaleCompressed)
				{
					if (smoothSync.isSyncingXScale)
					{
						state.scale.x = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingYScale)
					{
						state.scale.y = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingZScale)
					{
						state.scale.z = HalfHelper.Decompress(reader.ReadUShort());
					}
				}
				else
				{
					if (smoothSync.isSyncingXScale)
					{
						state.scale.x = reader.ReadFloat();
					}
					if (smoothSync.isSyncingYScale)
					{
						state.scale.y = reader.ReadFloat();
					}
					if (smoothSync.isSyncingZScale)
					{
						state.scale.z = reader.ReadFloat();
					}
				}
			}
			else if (smoothSync.stateCount > 0)
			{
				state.scale = smoothSync.stateBuffer[0].scale;
			}
			else
			{
				state.scale = smoothSync.getScale();
			}
			if (flag4)
			{
				if (smoothSync.isVelocityCompressed)
				{
					if (smoothSync.isSyncingXVelocity)
					{
						state.velocity.x = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingYVelocity)
					{
						state.velocity.y = HalfHelper.Decompress(reader.ReadUShort());
					}
					if (smoothSync.isSyncingZVelocity)
					{
						state.velocity.z = HalfHelper.Decompress(reader.ReadUShort());
					}
				}
				else
				{
					if (smoothSync.isSyncingXVelocity)
					{
						state.velocity.x = reader.ReadFloat();
					}
					if (smoothSync.isSyncingYVelocity)
					{
						state.velocity.y = reader.ReadFloat();
					}
					if (smoothSync.isSyncingZVelocity)
					{
						state.velocity.z = reader.ReadFloat();
					}
				}
				smoothSync.latestReceivedVelocity = state.velocity;
			}
			else
			{
				state.velocity = smoothSync.latestReceivedVelocity;
			}
			if (flag5)
			{
				if (smoothSync.isAngularVelocityCompressed)
				{
					state.reusableRotationVector = Vector3.zero;
					if (smoothSync.isSyncingXAngularVelocity)
					{
						state.reusableRotationVector.x = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.x *= 57.29578f;
					}
					if (smoothSync.isSyncingYAngularVelocity)
					{
						state.reusableRotationVector.y = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.y *= 57.29578f;
					}
					if (smoothSync.isSyncingZAngularVelocity)
					{
						state.reusableRotationVector.z = HalfHelper.Decompress(reader.ReadUShort());
						state.reusableRotationVector.z *= 57.29578f;
					}
					state.angularVelocity = state.reusableRotationVector;
				}
				else
				{
					if (smoothSync.isSyncingXAngularVelocity)
					{
						state.angularVelocity.x = reader.ReadFloat();
					}
					if (smoothSync.isSyncingYAngularVelocity)
					{
						state.angularVelocity.y = reader.ReadFloat();
					}
					if (smoothSync.isSyncingZAngularVelocity)
					{
						state.angularVelocity.z = reader.ReadFloat();
					}
				}
				smoothSync.latestReceivedAngularVelocity = state.angularVelocity;
			}
			else
			{
				state.angularVelocity = smoothSync.latestReceivedAngularVelocity;
			}
			if (smoothSync.isSmoothingAuthorityChanges && !NetworkServer.active)
			{
				smoothSync.ownerChangeIndicator = reader.ReadByte();
			}
			if (smoothSync.automaticallyResetTime)
			{
				state.localTimeResetIndicator = reader.ReadByte();
			}
			return result;
		}

		private static byte encodeSyncInformation(bool sendPosition, bool sendRotation, bool sendScale, bool sendVelocity, bool sendAngularVelocity, bool atPositionalRest, bool atRotationalRest)
		{
			byte b = 0;
			if (sendPosition)
			{
				b |= 1;
			}
			if (sendRotation)
			{
				b |= 2;
			}
			if (sendScale)
			{
				b |= 4;
			}
			if (sendVelocity)
			{
				b |= 8;
			}
			if (sendAngularVelocity)
			{
				b |= 0x10;
			}
			if (atPositionalRest)
			{
				b |= 0x40;
			}
			if (atRotationalRest)
			{
				b |= 0x80;
			}
			return b;
		}

		private static bool shouldSyncPosition(byte syncInformation)
		{
			if ((syncInformation & 1) == 1)
			{
				return true;
			}
			return false;
		}

		private static bool shouldSyncRotation(byte syncInformation)
		{
			if ((syncInformation & 2) == 2)
			{
				return true;
			}
			return false;
		}

		private static bool shouldSyncScale(byte syncInformation)
		{
			if ((syncInformation & 4) == 4)
			{
				return true;
			}
			return false;
		}

		private static bool shouldSyncVelocity(byte syncInformation)
		{
			if ((syncInformation & 8) == 8)
			{
				return true;
			}
			return false;
		}

		private static bool shouldSyncAngularVelocity(byte syncInformation)
		{
			if ((syncInformation & 0x10) == 16)
			{
				return true;
			}
			return false;
		}

		private static bool shouldBeAtPositionalRest(byte syncInformation)
		{
			if ((syncInformation & 0x40) == 64)
			{
				return true;
			}
			return false;
		}

		private static bool shouldBeAtRotationalRest(byte syncInformation)
		{
			if ((syncInformation & 0x80) == 128)
			{
				return true;
			}
			return false;
		}
	}
}

}
