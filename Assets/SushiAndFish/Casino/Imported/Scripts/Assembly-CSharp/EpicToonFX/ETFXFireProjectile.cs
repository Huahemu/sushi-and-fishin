namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.UI;

namespace EpicToonFX
{
	public class ETFXFireProjectile : MonoBehaviour
	{
		public GameObject[] projectiles;

		[Header("GUI Links")]
		public Text missileNameText;

		public Toggle fullAutoButton;

		public Slider speedSlider;

		public bool cleanUpMissileName;

		[Header("Projectile Settings")]
		public Transform spawnPosition;

		[HideInInspector]
		public int currentProjectile;

		public float speed = 1000f;

		public float spawnOffset = 0.3f;

		[Header("Firing Settings")]
		public float fireRate = 0.13f;

		public bool isFullAuto = true;

		[Header("Gun Settings")]
		public GameObject gunPrefab;

		public float gunOffset = 0.5f;

		private bool canShoot = true;

		private GameObject instantiatedGun;

		private void Start()
		{
			if (gunPrefab != null)
			{
				instantiatedGun = Object.Instantiate(gunPrefab, Vector3.zero, Quaternion.identity);
				instantiatedGun.transform.SetParent(base.transform);
				instantiatedGun.transform.localPosition = Vector3.zero;
			}
			if (speedSlider != null)
			{
				speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
				speed = speedSlider.value;
			}
			GameObject gameObject = GameObject.Find("ToggleAuto");
			if (gameObject != null)
			{
				fullAutoButton = gameObject.GetComponent<Toggle>();
			}
			UpdateDisplayName();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
			{
				nextEffect();
			}
			else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
			{
				previousEffect();
			}
			if (fullAutoButton != null)
			{
				isFullAuto = fullAutoButton.isOn;
			}
			if (instantiatedGun != null)
			{
				UpdateGunPositionAndRotation();
			}
			if (isFullAuto)
			{
				if (canShoot && Input.GetKey(KeyCode.Mouse0) && !EventSystem.current.IsPointerOverGameObject())
				{
					StartCoroutine(Shoot());
				}
			}
			else if (Input.GetKeyDown(KeyCode.Mouse0) && !EventSystem.current.IsPointerOverGameObject())
			{
				ShootProjectile();
			}
			if (speedSlider != null)
			{
				speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
				speed = speedSlider.value;
			}
			Debug.DrawRay(Camera.main.ScreenPointToRay(Input.mousePosition).origin, Camera.main.ScreenPointToRay(Input.mousePosition).direction * 100f, Color.yellow);
		}

		private IEnumerator Shoot()
		{
			canShoot = false;
			ShootProjectile();
			yield return new WaitForSeconds(fireRate);
			canShoot = true;
		}

		private void ShootProjectile()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Vector3 vector = ((!Physics.Raycast(ray, out var hitInfo, 100f)) ? ray.direction.normalized : (hitInfo.point - spawnPosition.position).normalized);
			Vector3 position = spawnPosition.position + vector * spawnOffset;
			Quaternion rotation = Quaternion.LookRotation(vector);
			Object.Instantiate(projectiles[currentProjectile], position, rotation).GetComponent<Rigidbody>().AddForce(vector * speed);
		}

		private void UpdateGunPositionAndRotation()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Vector3 vector = ((!Physics.Raycast(ray, out var hitInfo)) ? (ray.origin + ray.direction * 100f) : hitInfo.point);
			Vector3 normalized = (vector - base.transform.position).normalized;
			float y = Mathf.Atan2(normalized.x, normalized.z) * 57.29578f;
			Quaternion b = Quaternion.Euler((0f - Mathf.Asin(normalized.y / normalized.magnitude)) * 57.29578f, y, 0f);
			if (instantiatedGun != null)
			{
				instantiatedGun.transform.rotation = Quaternion.Slerp(instantiatedGun.transform.rotation, b, Time.deltaTime * 10f);
				instantiatedGun.transform.position = spawnPosition.position - instantiatedGun.transform.forward * gunOffset;
			}
		}

		public void nextEffect()
		{
			if (currentProjectile < projectiles.Length - 1)
			{
				currentProjectile++;
			}
			else
			{
				currentProjectile = 0;
			}
			UpdateDisplayName();
		}

		public void previousEffect()
		{
			if (currentProjectile > 0)
			{
				currentProjectile--;
			}
			else
			{
				currentProjectile = projectiles.Length - 1;
			}
			UpdateDisplayName();
		}

		private void UpdateDisplayName()
		{
			Text text = ((missileNameText != null) ? missileNameText : GetComponentInChildren<Text>());
			if (text != null)
			{
				string arg = projectiles[currentProjectile].GetComponent<ETFXProjectileScript>().projectileParticle.name;
				if (cleanUpMissileName)
				{
					arg = CleanUpMissileName(arg);
				}
				text.text = $"{arg} ({currentProjectile + 1}/{projectiles.Length})";
			}
		}

		private string CleanUpMissileName(string name)
		{
			name = name.Replace("Missile", "");
			name = name.Replace("Blue", " Blue");
			name = name.Replace("Red", " Red");
			name = name.Replace("Yellow", " Yellow");
			name = name.Replace("Green", " Green");
			name = name.Replace("Purple", " Purple");
			name = name.Replace("White", " White");
			name = name.Replace("Black", " Black");
			name = name.Replace("Pink", " Pink");
			name = name.Replace("Orange", " Orange");
			return name;
		}

		private void OnSpeedSliderChanged(float value)
		{
			speed = value;
		}
	}
}

}
