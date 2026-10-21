using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	public sealed class OwnedLoopHandle
	{
		private SoundManager _manager;

		private AudioSource _source;

		private ulong _playbackId;

		public string OwnerName { get; }

		public bool IsActive
		{
			get
			{
				if (_manager != null)
				{
					return _manager.IsPlaybackActive(_source, _playbackId);
				}
				return false;
			}
		}

		public AudioSource Source
		{
			get
			{
				if (!IsActive)
				{
					return null;
				}
				return _source;
			}
		}

		internal SoundManager Manager => _manager;

		internal AudioSource TrackedSource => _source;

		internal ulong PlaybackId => _playbackId;

		internal OwnedLoopHandle(SoundManager manager, AudioSource source, ulong playbackId, string ownerName)
		{
			_manager = manager;
			_source = source;
			_playbackId = playbackId;
			OwnerName = ownerName;
		}

		internal void Invalidate()
		{
			_manager = null;
			_source = null;
			_playbackId = 0uL;
		}
	}
}
