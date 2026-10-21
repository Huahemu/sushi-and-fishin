namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CosmeticWardrobe : NetworkBehaviour
{
	[SerializeField]
	private Transform leftWardrobeDoor;

	[SerializeField]
	private Transform rightWardrobeDoor;

	[SerializeField]
	private float openDoorAngle = 120f;

	[SerializeField]
	private bool isWardrobeOpen;

	[Header("Range Settings")]
	[SerializeField]
	private float interactionRange = 3f;

	[SerializeField]
	private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

	[Header("Cosmetic Display")]
	[SerializeField]
	private CosmeticType currentCategory;

	[SerializeField]
	private Transform[] spawnTransforms = new Transform[12];

	[SerializeField]
	private int itemsPerPage = 12;

	[SerializeField]
	private Material lockedCosmeticMaterial;

	[SerializeField]
	private Material unlockedCosmeticMaterial;

	[Header("Clothing tab display")]
	[SerializeField]
	private Vector3 clothingSpawnLocalOffset = new Vector3(0f, 1.1f, 0f);

	[SerializeField]
	private Camera[] mirrorCameras;

	[Header("SFX")]
	[SerializeField]
	private EventReference openDoorsSFX;

	[SerializeField]
	private EventReference closeDoorsSFX;

	[SerializeField]
	private EventReference pickItemSFX;

	[SerializeField]
	private GameObject sFXEmitter;

	private bool playerInRange;

	private int currentPage;

	private List<CosmeticData> currentCategoryCosmetics = new List<CosmeticData>();

	private List<GameObject> spawnedCosmetics = new List<GameObject>();

	private bool openEffectsApplied;

	private BoxCollider rangeTrigger;

	private bool IsClothingTab => currentCategory == CosmeticType.Clothing;

	private void Awake()
	{
		ConfigureRangeTrigger();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		StartCoroutine(CheckInitialRangeOnceReady());
	}

	public void CloseDoors()
	{
		if (!isWardrobeOpen)
		{
			return;
		}
		BeginClose(() =>
		{
			if (NetworkClient.localPlayer != null && !isWardrobeOpen)
			{
				ClearSpawnedCosmetics();
				Camera[] array = mirrorCameras;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].enabled = false;
				}
			}
		}, out var _, out var _);
	}

	public void OpenDoors()
	{
		if (!isWardrobeOpen)
		{
			SFXManager.SFXOneShot3DAttached(openDoorsSFX, sFXEmitter);
			leftWardrobeDoor.DOLocalRotate(new Vector3(0f, openDoorAngle, 0f), 1f).SetEase(Ease.OutBounce);
			rightWardrobeDoor.DOLocalRotate(new Vector3(0f, 0f - openDoorAngle, 0f), 1f).SetEase(Ease.OutBounce);
			isWardrobeOpen = true;
			ApplyOpenEffectsIfReady();
		}
	}

	private void ApplyOpenEffectsIfReady()
	{
		if (isWardrobeOpen && !openEffectsApplied && !(NetworkClient.localPlayer == null))
		{
			Camera[] array = mirrorCameras;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = true;
			}
			LoadAndDisplayCosmetics();
			openEffectsApplied = true;
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (IsLocalPlayerCollider(other) && !playerInRange)
		{
			playerInRange = true;
			OpenDoors();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (IsLocalPlayerCollider(other) && playerInRange)
		{
			playerInRange = false;
			CloseDoors();
		}
	}

	private void BeginClose(Action onLeftComplete, out Tween leftDoorTween, out Tween rightDoorTween)
	{
		SFXManager.SFXOneShot3DAttached(closeDoorsSFX, sFXEmitter);
		leftDoorTween = leftWardrobeDoor.DOLocalRotate(new Vector3(0f, 0f, 0f), 1f).SetEase(Ease.OutBounce).OnComplete(() =>
		{
			onLeftComplete?.Invoke();
		});
		rightDoorTween = rightWardrobeDoor.DOLocalRotate(new Vector3(0f, 0f, 0f), 1f).SetEase(Ease.OutBounce);
		isWardrobeOpen = false;
		openEffectsApplied = false;
	}

	private void ConfigureRangeTrigger()
	{
		if (rangeTrigger == null)
		{
			rangeTrigger = GetComponent<BoxCollider>();
		}
		rangeTrigger.isTrigger = true;
		rangeTrigger.size = new Vector3(interactionRange, interactionRange, interactionRange);
	}

	private IEnumerator CheckInitialRangeOnceReady()
	{
		while (NetworkClient.localPlayer == null)
		{
			yield return null;
		}
		if (Physics.OverlapSphere(base.transform.position, interactionRange).Any((Collider collider) => IsLocalPlayerCollider(collider)) && !playerInRange)
		{
			playerInRange = true;
			OpenDoors();
		}
		ApplyOpenEffectsIfReady();
	}

	private bool IsLocalPlayerCollider(Collider other)
	{
		if (other == null)
		{
			return false;
		}
		NetworkIdentity componentInParent = other.GetComponentInParent<NetworkIdentity>();
		if (componentInParent != null)
		{
			return componentInParent.isLocalPlayer;
		}
		return false;
	}

	public void NextPage()
	{
		if (!(NetworkClient.localPlayer == null) && GetTotalPages() > 1)
		{
			StartCoroutine(ChangePageCoroutine(next: true));
		}
	}

	public void PreviousPage()
	{
		if (!(NetworkClient.localPlayer == null) && GetTotalPages() > 1)
		{
			StartCoroutine(ChangePageCoroutine(next: false));
		}
	}

	public void SetCategoryByIndex(int categoryIndex)
	{
		if (NetworkClient.localPlayer == null)
		{
			return;
		}
		CosmeticType[] array = (CosmeticType[])Enum.GetValues(typeof(CosmeticType));
		if (categoryIndex < 0 || categoryIndex >= array.Length)
		{
			Debug.LogWarning($"[CosmeticWardrobe] Invalid category index: {categoryIndex}. Valid range: 0-{array.Length - 1}");
			return;
		}
		CosmeticType cosmeticType = array[categoryIndex];
		if (currentCategory != cosmeticType)
		{
			StartCoroutine(ChangeCategoryCoroutine(cosmeticType));
		}
	}

	public void RefreshPage()
	{
		if (playerInRange && !(NetworkClient.localPlayer == null))
		{
			StartCoroutine(RefreshPageCoroutine());
		}
	}

	private IEnumerator ChangePageCoroutine(bool next)
	{
		if (!isWardrobeOpen)
		{
			yield break;
		}
		BeginClose(() =>
		{
			if (NetworkClient.localPlayer != null)
			{
				ClearSpawnedCosmetics();
			}
		}, out var leftDoorTween, out var rightDoorTween);
		yield return leftDoorTween.WaitForCompletion();
		yield return rightDoorTween.WaitForCompletion();
		if (next)
		{
			currentPage = (currentPage + 1) % GetTotalPages();
		}
		else
		{
			currentPage = (currentPage - 1 + GetTotalPages()) % GetTotalPages();
		}
		OpenDoors();
	}

	private IEnumerator ChangeCategoryCoroutine(CosmeticType newCategory)
	{
		if (!isWardrobeOpen)
		{
			yield break;
		}
		BeginClose(() =>
		{
			if (NetworkClient.localPlayer != null)
			{
				ClearSpawnedCosmetics();
			}
		}, out var leftDoorTween, out var rightDoorTween);
		yield return leftDoorTween.WaitForCompletion();
		yield return rightDoorTween.WaitForCompletion();
		currentCategory = newCategory;
		currentPage = 0;
		OpenDoors();
	}

	private IEnumerator RefreshPageCoroutine()
	{
		if (!isWardrobeOpen)
		{
			yield break;
		}
		BeginClose(() =>
		{
			if (NetworkClient.localPlayer != null)
			{
				ClearSpawnedCosmetics();
			}
		}, out var leftDoorTween, out var rightDoorTween);
		yield return leftDoorTween.WaitForCompletion();
		yield return rightDoorTween.WaitForCompletion();
		OpenDoors();
	}

	private void LoadAndDisplayCosmetics()
	{
		ClearSpawnedCosmetics();
		LoadCategoryCosmetics();
		for (int i = 1; i < spawnTransforms.Length; i++)
		{
			Transform transform = spawnTransforms[i];
			if (transform != null && transform.parent != null)
			{
				transform.parent.gameObject.SetActive(value: false);
				SetSlotParentName(transform, string.Empty);
				SetLockSpriteVisible(transform, isVisible: false);
			}
		}
		int usableSlotsPerPage = GetUsableSlotsPerPage();
		int num = currentPage * usableSlotsPerPage;
		int num2 = Mathf.Min(num + usableSlotsPerPage, currentCategoryCosmetics.Count);
		for (int j = num; j < num2; j++)
		{
			int num3 = j - num + 1;
			if (num3 >= spawnTransforms.Length || spawnTransforms[num3] == null)
			{
				Debug.LogWarning($"[CosmeticWardrobe] Spawn transform at index {num3} is null or out of bounds");
				continue;
			}
			CosmeticData cosmeticData = currentCategoryCosmetics[j];
			if (cosmeticData == null || cosmeticData.cosmeticModel == null)
			{
				Debug.LogWarning($"[CosmeticWardrobe] Cosmetic {j} has no model");
				continue;
			}
			Transform transform2 = spawnTransforms[num3];
			SpawnCosmetic(cosmeticData, transform2);
			SetSlotParentName(transform2, cosmeticData.cosmeticName);
			if (transform2.parent != null)
			{
				transform2.parent.gameObject.SetActive(value: true);
			}
		}
		bool flag = !IsClothingTab;
		for (int k = 0; k < spawnTransforms.Length; k++)
		{
			Transform transform3 = spawnTransforms[k];
			if (transform3 == null || transform3.parent == null)
			{
				continue;
			}
			MeshRenderer[] components = transform3.parent.GetComponents<MeshRenderer>();
			foreach (MeshRenderer meshRenderer in components)
			{
				if (meshRenderer != null)
				{
					meshRenderer.enabled = flag;
				}
			}
			SkinnedMeshRenderer[] components2 = transform3.parent.GetComponents<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in components2)
			{
				if (skinnedMeshRenderer != null)
				{
					skinnedMeshRenderer.enabled = flag;
				}
			}
		}
		RefreshSlotParentOutlines();
	}

	private void LoadCategoryCosmetics()
	{
		currentCategoryCosmetics.Clear();
		IEnumerable<CosmeticData> allCosmetics = CosmeticDataManager.GetAllCosmetics();
		currentCategoryCosmetics = (from c in allCosmetics
			where c != null && c.cosmeticType == currentCategory
			orderby c.cosmeticId
			select c).ToList();
		Debug.Log($"[CosmeticWardrobe] Loaded {currentCategoryCosmetics.Count} cosmetics for category {currentCategory}");
	}

	private void SpawnCosmetic(CosmeticData cosmetic, Transform spawnTransform)
	{
		if (cosmetic == null || cosmetic.cosmeticModel == null || spawnTransform == null)
		{
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(cosmetic.cosmeticModel, spawnTransform.position, spawnTransform.rotation, spawnTransform);
		if (IsClothingTab)
		{
			gameObject.transform.localPosition += clothingSpawnLocalOffset;
		}
		spawnedCosmetics.Add(gameObject);
		bool flag = MonoSingleton<CosmeticsUnlockManager>.Instance != null && MonoSingleton<CosmeticsUnlockManager>.Instance.IsCosmeticUnlocked(cosmetic.cosmeticId);
		SetLockSpriteVisible(spawnTransform, !flag);
		Material material = (flag ? cosmetic.cosmeticMaterial : lockedCosmeticMaterial);
		if (!(material != null))
		{
			return;
		}
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (renderer != null)
			{
				renderer.material = material;
			}
		}
	}

	private static void SetLockSpriteVisible(Transform spawnTransform, bool isVisible)
	{
		if (!(spawnTransform == null) && !(spawnTransform.parent == null))
		{
			SpriteRenderer spriteRenderer = spawnTransform.parent.GetComponentsInChildren<SpriteRenderer>(includeInactive: true).FirstOrDefault((SpriteRenderer sr) => sr.transform.parent == spawnTransform.parent && sr.transform != spawnTransform);
			if (spriteRenderer != null)
			{
				spriteRenderer.enabled = isVisible;
			}
		}
	}

	private void ClearSpawnedCosmetics()
	{
		foreach (GameObject spawnedCosmetic in spawnedCosmetics)
		{
			if (spawnedCosmetic != null)
			{
				UnityEngine.Object.Destroy(spawnedCosmetic);
			}
		}
		spawnedCosmetics.Clear();
		for (int i = 0; i < spawnTransforms.Length; i++)
		{
			SetLockSpriteVisible(spawnTransforms[i], isVisible: false);
		}
	}

	private void RefreshSlotParentOutlines()
	{
		for (int i = 0; i < spawnTransforms.Length; i++)
		{
			Transform transform = spawnTransforms[i];
			if (!(transform == null) && !(transform.parent == null) && transform.parent.TryGetComponent<Outline>(out var component))
			{
				bool num = component.enabled;
				component.CacheRenderers();
				if (num)
				{
					component.enabled = false;
					component.enabled = true;
				}
			}
		}
	}

	private static void SetSlotParentName(Transform spawnTransform, string tooltip)
	{
		if (!(spawnTransform == null) && !(spawnTransform.parent == null) && spawnTransform.parent.TryGetComponent<InteractableBase>(out var component))
		{
			component.InteractableName = tooltip;
		}
	}

	private int GetTotalPages()
	{
		if (currentCategoryCosmetics.Count == 0)
		{
			LoadCategoryCosmetics();
		}
		int usableSlotsPerPage = GetUsableSlotsPerPage();
		return Mathf.Max(1, Mathf.CeilToInt((float)currentCategoryCosmetics.Count / (float)usableSlotsPerPage));
	}

	private int GetUsableSlotsPerPage()
	{
		return Mathf.Max(1, itemsPerPage - 1);
	}

	public void TryEquipCosmetic(int spawnIndex)
	{
		if (NetworkClient.localPlayer == null)
		{
			return;
		}
		if (spawnIndex < 0 || spawnIndex >= spawnTransforms.Length)
		{
			Debug.LogWarning($"[CosmeticWardrobe] Invalid spawn index: {spawnIndex}");
			return;
		}
		if (spawnIndex == 0)
		{
			PlayerCustomization component = NetworkClient.localPlayer.GetComponent<PlayerCustomization>();
			if (component == null)
			{
				Debug.LogWarning("[CosmeticWardrobe] PlayerCustomization component not found on local player");
			}
			else
			{
				component.ClearCategory(currentCategory);
			}
			return;
		}
		int usableSlotsPerPage = GetUsableSlotsPerPage();
		int num = currentPage * usableSlotsPerPage + (spawnIndex - 1);
		if (num < 0 || num >= currentCategoryCosmetics.Count)
		{
			Debug.LogWarning($"[CosmeticWardrobe] Cosmetic index {num} is out of bounds");
			return;
		}
		CosmeticData cosmeticData = currentCategoryCosmetics[num];
		if (cosmeticData == null)
		{
			Debug.LogWarning($"[CosmeticWardrobe] Cosmetic at index {num} is null");
			return;
		}
		int cosmeticId = cosmeticData.cosmeticId;
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			Debug.LogError("[CosmeticWardrobe] CosmeticsUnlockManager.Instance is null!");
			return;
		}
		if (!MonoSingleton<CosmeticsUnlockManager>.Instance.IsCosmeticUnlocked(cosmeticId))
		{
			Debug.LogWarning($"[CosmeticWardrobe] Cosmetic {cosmeticId} ({cosmeticData.cosmeticName}) is not unlocked!");
			return;
		}
		PlayerCustomization component2 = NetworkClient.localPlayer.GetComponent<PlayerCustomization>();
		if (component2 == null)
		{
			Debug.LogWarning("[CosmeticWardrobe] PlayerCustomization component not found on local player");
			return;
		}
		component2.CmdChangeCustomization(cosmeticId, shouldSave: true);
		Debug.Log($"[CosmeticWardrobe] Equipped cosmetic: {cosmeticData.cosmeticName} (ID: {cosmeticId})");
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = gizmoColor;
		Gizmos.DrawWireSphere(base.transform.position, interactionRange);
		Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
		Gizmos.DrawWireSphere(base.transform.position, interactionRange);
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
		Gizmos.DrawSphere(base.transform.position, interactionRange);
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(base.transform.position, interactionRange);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
