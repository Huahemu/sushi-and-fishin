using System;
using System.Collections.Generic;
using Dissonance.Audio.Playback;
using Dissonance.Extensions;
using Dissonance.Networking;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance.Integrations.Offline
{
	public class OfflineCommsNetwork : MonoBehaviour, ICommsNetwork
	{
		private bool _loopbackActive;

		private bool _sentStartedSpeakingEvent;

		private uint _loopbackSequenceNumber;

		private readonly List<RemoteChannel> _loopbackChannels = new List<RemoteChannel>();

		private readonly Queue<byte[]> _bufferPool = new Queue<byte[]>();

		private readonly Queue<VoicePacket> _loopbackQueue = new Queue<VoicePacket>(128);

		private bool _playerJoined;

		private CodecSettings? _codecSettings;

		public int LoopbackPacketCount { get; private set; }

		public ConnectionStatus Status => ConnectionStatus.Connected;

		public NetworkMode Mode => NetworkMode.Client;

		public event Action<string, CodecSettings> PlayerJoined;

		public event Action<VoicePacket> VoicePacketReceived;

		public event Action<string> PlayerStartedSpeaking;

		public event Action<string> PlayerStoppedSpeaking;

		public event Action<NetworkMode> ModeChanged;

		public event Action<string> PlayerLeft;

		public event Action<TextMessage> TextPacketReceived;

		public event Action<RoomEvent> PlayerEnteredRoom;

		public event Action<RoomEvent> PlayerExitedRoom;

		public void Initialize(string playerName, Rooms rooms, PlayerChannels playerChannels, RoomChannels roomChannels, CodecSettings codecSettings)
		{
			_codecSettings = codecSettings;
			_loopbackChannels.Add(new RemoteChannel("Loopback", ChannelType.Room, new PlaybackOptions(isPositional: false, 1f, ChannelPriority.Default)));
			roomChannels.OpenedChannel += BeginLoopback;
			roomChannels.ClosedChannel += EndLoopback;
		}

		private void BeginLoopback(RoomName channel, ChannelProperties props)
		{
			_loopbackActive = true;
		}

		private void EndLoopback(RoomName channel, ChannelProperties props)
		{
			if (_sentStartedSpeakingEvent)
			{
				PlayerStoppedSpeaking?.Invoke("Loopback");
			}
			_loopbackQueue.Clear();
			_sentStartedSpeakingEvent = false;
			_loopbackActive = false;
			_loopbackSequenceNumber = 0u;
		}

		public void SendVoice(ArraySegment<byte> data)
		{
			if (_loopbackActive)
			{
				ArraySegment<byte> encodedAudioFrame = data.CopyToSegment((_bufferPool.Count > 0) ? _bufferPool.Dequeue() : new byte[1024]);
				LoopbackPacketCount++;
				_loopbackQueue.Enqueue(new VoicePacket("Loopback", ChannelPriority.Default, 1f, positional: false, encodedAudioFrame, _loopbackSequenceNumber++, _loopbackChannels));
			}
		}

		public void SendText([CanBeNull] string data, ChannelType recipientType, string recipientId)
		{
		}

		private void Update()
		{
			JoinFakePlayer();
			if (_playerJoined)
			{
				PumpLoopback();
			}
		}

		private void JoinFakePlayer()
		{
			if (!_playerJoined && _codecSettings.HasValue)
			{
				PlayerJoined?.Invoke("Loopback", _codecSettings.Value);
				_playerJoined = true;
			}
		}

		private void PumpLoopback()
		{
			if (_loopbackActive && (_sentStartedSpeakingEvent || _loopbackQueue.Count >= 5))
			{
				if (!_sentStartedSpeakingEvent)
				{
					PlayerStartedSpeaking?.Invoke("Loopback");
					_sentStartedSpeakingEvent = true;
				}
				while (_loopbackQueue.Count > 0)
				{
					VoicePacket obj = _loopbackQueue.Dequeue();
					VoicePacketReceived?.Invoke(obj);
					_bufferPool.Enqueue(obj.EncodedAudioFrame.Array);
				}
			}
		}
	}
}
