using CasinoLab = global::SushiAndFish.Casino.Original.CasinoLab;
using SushiAndFish.Casino.Original;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

namespace Dissonance.Integrations.MirrorIgnorance
{
	[RequireComponent(typeof(NetworkIdentity))]
	public class MirrorIgnorancePlayer : NetworkBehaviour, IDissonancePlayer
	{
		private static readonly Log Log;

		private DissonanceComms _comms;

		[SyncVar]
		private string _playerId;

		public bool IsTracking { get; private set; }

		public string PlayerId => _playerId;

		public Vector3 Position => base.transform.position;

		public Quaternion Rotation => base.transform.rotation;

		public NetworkPlayerType Type
		{
			get
			{
				if (_comms == null || _playerId == null)
				{
					return NetworkPlayerType.Unknown;
				}
				if (!_comms.LocalPlayerName.Equals(_playerId))
				{
					return NetworkPlayerType.Remote;
				}
				return NetworkPlayerType.Local;
			}
		}

		public string Network_playerId
		{
			get
			{
				return _playerId;
			}
			[param: In]
			set
			{
				GeneratedSyncVarSetter(value, ref _playerId, 1uL, null);
			}
		}

		public void OnDestroy()
		{
			if (_comms != null)
			{
				_comms.LocalPlayerNameChanged -= SetPlayerName;
			}
		}

		public void OnEnable()
		{
			_comms = Object.FindFirstObjectByType<DissonanceComms>();
		}

		public void OnDisable()
		{
			if (IsTracking)
			{
				StopTracking();
			}
		}

		public override void OnStartLocalPlayer()
		{
			base.OnStartLocalPlayer();
			if (CasinoLab.CasinoLabMode.Active) return;
			DissonanceComms dissonanceComms = Object.FindFirstObjectByType<DissonanceComms>();
			if (dissonanceComms == null)
			{
				throw Log.CreateUserErrorException("cannot find DissonanceComms component in scene", "not placing a DissonanceComms component on a game object in the scene", "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-MirrorIgnorance/", "2D90A6C3-5F2B-4859-994C-EBBDDD4A10F4");
			}
			if (dissonanceComms.LocalPlayerName != null)
			{
				SetPlayerName(dissonanceComms.LocalPlayerName);
			}
			dissonanceComms.LocalPlayerNameChanged += SetPlayerName;
		}

		private void SetPlayerName(string playerName)
		{
			if (IsTracking)
			{
				StopTracking();
			}
			Network_playerId = playerName;
			StartTracking();
			if (base.isLocalPlayer)
			{
				CmdSetPlayerName(playerName);
			}
		}

		public override void OnStartClient()
		{
			base.OnStartClient();
			if (!string.IsNullOrEmpty(PlayerId))
			{
				StartTracking();
			}
		}

		[Command]
		private void CmdSetPlayerName(string playerName)
		{
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteString(playerName);
			SendCommandInternal("System.Void Dissonance.Integrations.MirrorIgnorance.MirrorIgnorancePlayer::CmdSetPlayerName(System.String)", 2094266474, writer, 0);
			NetworkWriterPool.Return(writer);
		}

		[ClientRpc]
		private void RpcSetPlayerName(string playerName)
		{
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteString(playerName);
			SendRPCInternal("System.Void Dissonance.Integrations.MirrorIgnorance.MirrorIgnorancePlayer::RpcSetPlayerName(System.String)", -1860767819, writer, 0, includeOwner: true);
			NetworkWriterPool.Return(writer);
		}

		private void StartTracking()
		{
			if (IsTracking)
			{
				throw Log.CreatePossibleBugException("Attempting to start player tracking, but tracking is already started", "31971B1F-52FD-4FCF-89E9-67A17A917921");
			}
			if (_comms != null)
			{
				_comms.TrackPlayerPosition(this);
				IsTracking = true;
			}
		}

		private void StopTracking()
		{
			if (!IsTracking)
			{
				throw Log.CreatePossibleBugException("Attempting to stop player tracking, but tracking is not started", "C7CF0174-0667-4F07-88E3-800ED652142D");
			}
			if (_comms != null)
			{
				_comms.StopTracking(this);
				IsTracking = false;
			}
		}

		static MirrorIgnorancePlayer()
		{
			Log = Logs.Create(LogCategory.Network, "Mirror Player Component");
			RemoteProcedureCalls.RegisterCommand(typeof(MirrorIgnorancePlayer), "System.Void Dissonance.Integrations.MirrorIgnorance.MirrorIgnorancePlayer::CmdSetPlayerName(System.String)", InvokeUserCode_CmdSetPlayerName__String, requiresAuthority: true);
			RemoteProcedureCalls.RegisterRpc(typeof(MirrorIgnorancePlayer), "System.Void Dissonance.Integrations.MirrorIgnorance.MirrorIgnorancePlayer::RpcSetPlayerName(System.String)", InvokeUserCode_RpcSetPlayerName__String);
		}

		public override bool Weaved()
		{
			return true;
		}

		protected void UserCode_CmdSetPlayerName__String(string playerName)
		{
			Network_playerId = playerName;
			RpcSetPlayerName(playerName);
		}

		protected static void InvokeUserCode_CmdSetPlayerName__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
		{
			if (!NetworkServer.active)
			{
				Debug.LogError("Command CmdSetPlayerName called on client.");
			}
			else
			{
				((MirrorIgnorancePlayer)obj).UserCode_CmdSetPlayerName__String(reader.ReadString());
			}
		}

		protected void UserCode_RpcSetPlayerName__String(string playerName)
		{
			if (!base.isLocalPlayer)
			{
				SetPlayerName(playerName);
			}
		}

		protected static void InvokeUserCode_RpcSetPlayerName__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
		{
			if (!NetworkClient.active)
			{
				Debug.LogError("RPC RpcSetPlayerName called on server.");
			}
			else
			{
				((MirrorIgnorancePlayer)obj).UserCode_RpcSetPlayerName__String(reader.ReadString());
			}
		}

		protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
		{
			base.SerializeSyncVars(writer, forceAll);
			if (forceAll)
			{
				writer.WriteString(_playerId);
				return;
			}
			writer.WriteVarULong(syncVarDirtyBits);
			if ((syncVarDirtyBits & 1L) != 0L)
			{
				writer.WriteString(_playerId);
			}
		}

		protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
		{
			base.DeserializeSyncVars(reader, initialState);
			if (initialState)
			{
				GeneratedSyncVarDeserialize(ref _playerId, null, reader.ReadString());
				return;
			}
			long num = (long)reader.ReadVarULong();
			if ((num & 1L) != 0L)
			{
				GeneratedSyncVarDeserialize(ref _playerId, null, reader.ReadString());
			}
		}
	}
}
