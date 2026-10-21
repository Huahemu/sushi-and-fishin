using UnityEngine;

namespace PixPlays.ElementalVFX
{
	[RequireComponent(typeof(ParticleSystem))]
	public class ParticleSystemStartStopLifetime : MonoBehaviour
	{
		[SerializeField]
		private float _StartLifetime;

		[SerializeField]
		private float _StopLifetime;

		private ParticleSystem _particleSystem;

		private bool _playedFlag;

		private void Awake()
		{
			_particleSystem = GetComponent<ParticleSystem>();
		}

		private void Update()
		{
			if (_particleSystem.isEmitting && !_playedFlag)
			{
				_playedFlag = true;
			}
			if (!_particleSystem.isEmitting && _playedFlag)
			{
				ParticleSystem.Particle[] array = new ParticleSystem.Particle[_particleSystem.particleCount];
				_particleSystem.GetParticles(array);
				for (int i = 0; i < array.Length; i++)
				{
					float num = array[i].remainingLifetime / array[i].startLifetime;
					array[i].startLifetime = _StopLifetime;
					array[i].remainingLifetime = _StopLifetime * num;
				}
				_particleSystem.SetParticles(array);
				_playedFlag = false;
			}
		}
	}
}
