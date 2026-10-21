using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PilotoStudio
{
	public class ParticleShowcase : MonoBehaviour
	{
		[SerializeField]
		private List<GameObject> particles = new List<GameObject>();

		[SerializeField]
		private int currentlyActive;

		[SerializeField]
		private Text displayName;

		private void Start()
		{
			foreach (Transform item in base.transform)
			{
				particles.Add(item.gameObject);
			}
			PostUpdateLogic();
			particles[currentlyActive].SetActive(value: true);
		}

		private void PostUpdateLogic()
		{
			if (displayName != null)
			{
				displayName.text = particles[currentlyActive].name;
			}
			if (particles[currentlyActive].TryGetComponent<ParticleHandler>(out var component))
			{
				component.Cast();
			}
		}

		public void ActivateNext()
		{
			if (currentlyActive + 1 >= particles.Count)
			{
				particles[currentlyActive].SetActive(value: false);
				currentlyActive = 0;
				particles[currentlyActive].SetActive(value: true);
			}
			else
			{
				particles[currentlyActive].SetActive(value: false);
				currentlyActive++;
				particles[currentlyActive].SetActive(value: true);
			}
			PostUpdateLogic();
		}

		public void ActivatePrevious()
		{
			if (currentlyActive - 1 < 0)
			{
				particles[currentlyActive].SetActive(value: false);
				currentlyActive = particles.Count - 1;
				particles[currentlyActive].SetActive(value: true);
			}
			else
			{
				particles[currentlyActive].SetActive(value: false);
				currentlyActive--;
				particles[currentlyActive].SetActive(value: true);
			}
			PostUpdateLogic();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Q))
			{
				ActivatePrevious();
			}
			if (Input.GetKeyDown(KeyCode.W))
			{
				ActivateNext();
			}
			if (Input.GetKeyDown(KeyCode.Space))
			{
				if (particles[currentlyActive].TryGetComponent<ParticleSystem>(out var component))
				{
					component.Play();
				}
				PostUpdateLogic();
			}
		}
	}
}
