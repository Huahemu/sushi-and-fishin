namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

namespace EpicToonFX
{
	[Serializable]
	public class TargetEffects
	{
		public GameObject hitParticle;

		public GameObject respawnParticle;

		public List<GameObject> deathParticles = new List<GameObject>();

		public AudioClip destroySound;

		public AudioClip respawnSound;
	}
}

}
