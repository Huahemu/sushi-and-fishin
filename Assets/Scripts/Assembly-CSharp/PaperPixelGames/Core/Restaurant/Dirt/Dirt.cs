using UnityEngine;

namespace PaperPixelGames.Core.Restaurant.Dirt
{
	public class Dirt : MonoBehaviour
	{
		[Header("Optional")]
		[SerializeField]
		private float _autoDestroyAfterSeconds;

		private DirtManager _manager;

		public byte DirtId { get; private set; }

		public void Initialize(byte dirtId, DirtManager manager)
		{
			DirtId = dirtId;
			_manager = manager;
		}

		private void Start()
		{
			if (_autoDestroyAfterSeconds > 0f)
			{
				Invoke("Clean", _autoDestroyAfterSeconds);
			}
		}

		public void Clean()
		{
			if (_manager != null)
			{
				_manager.ServerDirtDestroyedRpc(DirtId);
			}
		}
	}
}
