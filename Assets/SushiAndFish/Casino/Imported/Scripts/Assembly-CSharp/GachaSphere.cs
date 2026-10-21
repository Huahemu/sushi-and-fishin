namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::UnityEngine;

public class GachaSphere : ConsumableItem
{
	[Header("Cosmetic Data")]
	[SerializeField]
	[SyncVar(hook = "OnCosmeticIdChanged")]
	private int cosmeticId = -1;

	[SerializeField]
	private Transform cosmeticModelTransform;

	[SerializeField]
	private bool centerUsingBounds = true;

	[Header("Clothing Overrides")]
	[Tooltip("Only for clothing cosmetics: disables this transform's MeshRenderer/SkinnedMeshRenderer.")]
	[SerializeField]
	private Transform clothingRendererToDisable;

	[SerializeField]
	private GameEvent onCosmeticUnlocked;

	private CosmeticData _currentCosmeticData;

	private GameObject _spawnedCosmeticModel;

	[SerializeField]
	private bool randomCosmeticIdOnStart;

	public Action<int, int> _Mirror_SyncVarHookDelegate_cosmeticId;

	public int CosmeticId => cosmeticId;

	public override bool ShouldShowHoverDescription => false;

	public int NetworkcosmeticId
	{
		get
		{
			return cosmeticId;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref cosmeticId, 2uL, _Mirror_SyncVarHookDelegate_cosmeticId);
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		if (randomCosmeticIdOnStart)
		{
			int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
			global::System.Random random = new global::System.Random(GetDeterministicCosmeticHash(base.transform.position, NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed, successfulQuota));
			int[] validCosmeticIdsSorted = CosmeticDataManager.GetValidCosmeticIdsSorted();
			if (validCosmeticIdsSorted.Length == 0)
			{
				Debug.LogWarning("[GachaSphere] No cosmetic data loaded; cannot assign random cosmetic.");
				NetworkcosmeticId = -1;
			}
			else
			{
				int num = random.Next(0, validCosmeticIdsSorted.Length);
				NetworkcosmeticId = validCosmeticIdsSorted[num];
			}
		}
	}

	private int GetDeterministicCosmeticHash(Vector3 position, int seed, int quotaIndex)
	{
		int num = seed * 31 + quotaIndex;
		int num2 = Mathf.RoundToInt(position.x * 100f);
		int num3 = Mathf.RoundToInt(position.y * 100f);
		int num4 = Mathf.RoundToInt(position.z * 100f);
		return ((num * 31 + num2) * 31 + num3) * 31 + num4;
	}

	[Server]
	public void SetCosmeticData(CosmeticData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GachaSphere::SetCosmeticData(CosmeticData)' called when server was not active");
		}
		else if (data != null)
		{
			NetworkcosmeticId = data.cosmeticId;
		}
	}

	[Server]
	public void SetCosmeticId(int id)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GachaSphere::SetCosmeticId(System.Int32)' called when server was not active");
		}
		else if (id > 0)
		{
			NetworkcosmeticId = id;
		}
	}

	private void OnCosmeticIdChanged(int oldValue, int newValue)
	{
		NetworkcosmeticId = newValue;
		InteractableName = CosmeticDataManager.GetCosmeticById(cosmeticId).cosmeticName;
		LoadAndSpawnCosmetic();
	}

	private void LoadAndSpawnCosmetic()
	{
		ClearCosmeticModel();
		if (cosmeticId > 0)
		{
			_currentCosmeticData = CosmeticDataManager.GetCosmeticById(cosmeticId);
			if (_currentCosmeticData == null)
			{
				Debug.LogError($"[GachaSphere] Failed to load CosmeticData with ID {cosmeticId}. Make sure CosmeticDataManager is initialized.");
			}
			else if (_currentCosmeticData.cosmeticModel == null)
			{
				Debug.LogError($"[GachaSphere] CosmeticData with ID {cosmeticId} ({_currentCosmeticData.cosmeticName}) has no cosmeticModel assigned!");
			}
			else
			{
				SpawnCosmeticModel();
			}
		}
	}

	private void SpawnCosmeticModel()
	{
		if (!(_currentCosmeticData == null) && !(_currentCosmeticData.cosmeticModel == null))
		{
			bool num = _currentCosmeticData.cosmeticType == CosmeticType.Clothing;
			_spawnedCosmeticModel = UnityEngine.Object.Instantiate(_currentCosmeticData.cosmeticModel, cosmeticModelTransform.position, cosmeticModelTransform.rotation, cosmeticModelTransform);
			RefreshCosmeticParentOutline();
			Renderer component = _spawnedCosmeticModel.GetComponent<Renderer>();
			if (component != null)
			{
				component.material = _currentCosmeticData.cosmeticMaterial;
			}
			if (num)
			{
				SetRendererEnabled(clothingRendererToDisable, enabled: false);
				_spawnedCosmeticModel.transform.localPosition = new Vector3(0f, 1.1f, 0f);
			}
			else
			{
				SetRendererEnabled(clothingRendererToDisable, enabled: true);
				_spawnedCosmeticModel.transform.localPosition = Vector3.zero;
			}
			if (centerUsingBounds)
			{
				CenterCosmeticUsingBounds();
			}
		}
	}

	private void RefreshCosmeticParentOutline()
	{
		if (cosmeticModelTransform == null)
		{
			return;
		}
		Outline componentInParent = cosmeticModelTransform.GetComponentInParent<Outline>();
		if (!(componentInParent == null))
		{
			bool flag = componentInParent.enabled;
			componentInParent.CacheRenderers();
			if (flag)
			{
				componentInParent.enabled = false;
				componentInParent.enabled = true;
			}
		}
	}

	private static void SetRendererEnabled(Transform transform, bool enabled)
	{
		if (!(transform == null))
		{
			MeshRenderer component = transform.GetComponent<MeshRenderer>();
			if (component != null)
			{
				component.enabled = enabled;
			}
			SkinnedMeshRenderer component2 = transform.GetComponent<SkinnedMeshRenderer>();
			if (component2 != null)
			{
				component2.enabled = enabled;
			}
		}
	}

	private void CenterCosmeticUsingBounds()
	{
		if (_spawnedCosmeticModel == null)
		{
			return;
		}
		Renderer[] componentsInChildren = _spawnedCosmeticModel.GetComponentsInChildren<Renderer>();
		if (componentsInChildren.Length != 0)
		{
			Bounds bounds = componentsInChildren[0].bounds;
			for (int i = 1; i < componentsInChildren.Length; i++)
			{
				bounds.Encapsulate(componentsInChildren[i].bounds);
			}
			Vector3 vector = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
			Vector3 vector2 = cosmeticModelTransform.position - vector;
			_spawnedCosmeticModel.transform.position += vector2;
		}
	}

	private void ClearCosmeticModel()
	{
		if (_spawnedCosmeticModel != null)
		{
			UnityEngine.Object.Destroy(_spawnedCosmeticModel);
			_spawnedCosmeticModel = null;
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClearCosmeticModel();
	}

	public GachaSphere()
	{
		_Mirror_SyncVarHookDelegate_cosmeticId = OnCosmeticIdChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(cosmeticId);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarInt(cosmeticId);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref cosmeticId, _Mirror_SyncVarHookDelegate_cosmeticId, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref cosmeticId, _Mirror_SyncVarHookDelegate_cosmeticId, reader.ReadVarInt());
		}
	}
}

}
