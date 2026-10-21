using System;
using System.Collections.Generic;
using System.Linq;
using ES3Internal;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[IncludeInSettings(true)]
public class ES3AutoSaveMgr : MonoBehaviour
{
	public enum LoadEvent
	{
		None = 0,
		Awake = 1,
		Start = 2
	}

	public enum SaveEvent
	{
		None = 0,
		OnApplicationQuit = 1,
		OnApplicationPause = 2
	}

	public static ES3AutoSaveMgr _current = null;

	public static Dictionary<Scene, ES3AutoSaveMgr> managers = new Dictionary<Scene, ES3AutoSaveMgr>();

	public string key = Guid.NewGuid().ToString();

	public bool immediatelyCommitToFile = true;

	public SaveEvent saveEvent = SaveEvent.OnApplicationQuit;

	public LoadEvent loadEvent = LoadEvent.Start;

	public ES3SerializableSettings settings = new ES3SerializableSettings("SaveFile.es3", ES3.Location.Cache);

	public HashSet<ES3AutoSave> autoSaves = new HashSet<ES3AutoSave>();

	private List<long> destroyedIds = new List<long>();

	public static ES3AutoSaveMgr Current
	{
		get
		{
			if (_current == null)
			{
				GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
				GameObject[] array = rootGameObjects;
				foreach (GameObject gameObject in array)
				{
					if (gameObject.name == "Easy Save 3 Manager")
					{
						return _current = gameObject.GetComponent<ES3AutoSaveMgr>();
					}
				}
				array = rootGameObjects;
				for (int i = 0; i < array.Length; i++)
				{
					if ((_current = array[i].GetComponentInChildren<ES3AutoSaveMgr>()) != null)
					{
						return _current;
					}
				}
			}
			return _current;
		}
	}

	public static ES3AutoSaveMgr Instance => Current;

	public void Save()
	{
		if (autoSaves == null || autoSaves.Count == 0)
		{
			return;
		}
		ManageSlots();
		if (settings.location == ES3.Location.Cache && !ES3.FileExists(settings))
		{
			ES3.CacheFile(settings);
		}
		if (autoSaves == null || autoSaves.Count == 0)
		{
			ES3.DeleteKey(key, settings);
		}
		else
		{
			List<GameObject> list = new List<GameObject>();
			foreach (ES3AutoSave autoSafe in autoSaves)
			{
				if (autoSafe != null && autoSafe.enabled)
				{
					list.Add(autoSafe.gameObject);
				}
			}
			ES3.Save(key, list.OrderBy((GameObject x) => GetDepth(x.transform)).ToArray(), settings);
			if (destroyedIds != null && destroyedIds.Count > 0)
			{
				ES3.Save(key + "_destroyed", destroyedIds, settings);
			}
		}
		if (immediatelyCommitToFile && settings.location == ES3.Location.Cache && ES3.FileExists(settings))
		{
			ES3.StoreCachedFile(settings);
		}
	}

	public void Load()
	{
		ManageSlots();
		try
		{
			if (settings.location == ES3.Location.Cache && !ES3.FileExists(settings))
			{
				ES3.CacheFile(settings);
			}
		}
		catch
		{
		}
		ES3ReferenceMgrBase managerFromScene = ES3ReferenceMgrBase.GetManagerFromScene(base.gameObject.scene, getAnyManagerIfNotInScene: false);
		managerFromScene.Awake();
		ES3.Load(key, new GameObject[0], settings);
		foreach (long item in ES3.Load(key + "_destroyed", new List<long>(), settings))
		{
			UnityEngine.Object obj2 = managerFromScene.Get(item, suppressWarnings: true);
			if (obj2 != null)
			{
				ES3AutoSave component = ((GameObject)obj2).GetComponent<ES3AutoSave>();
				if (component != null)
				{
					DestroyAutoSave(component);
				}
				UnityEngine.Object.Destroy(obj2);
			}
		}
	}

	private void Start()
	{
		if (loadEvent == LoadEvent.Start)
		{
			Load();
		}
	}

	public void Awake()
	{
		managers[base.gameObject.scene] = this;
		GetAutoSaves();
		if (loadEvent == LoadEvent.Awake)
		{
			Load();
		}
	}

	private void OnApplicationQuit()
	{
		if (saveEvent == SaveEvent.OnApplicationQuit)
		{
			Save();
		}
	}

	private void OnApplicationPause(bool paused)
	{
		if ((saveEvent == SaveEvent.OnApplicationPause || (Application.isMobilePlatform && saveEvent == SaveEvent.OnApplicationQuit)) & paused)
		{
			Save();
		}
	}

	public static void AddAutoSave(ES3AutoSave autoSave)
	{
		if (!(autoSave == null) && managers.TryGetValue(autoSave.gameObject.scene, out var value))
		{
			value.autoSaves.Add(autoSave);
		}
	}

	public static void DestroyAutoSave(ES3AutoSave autoSave)
	{
		if (autoSave == null || !managers.TryGetValue(autoSave.gameObject.scene, out var value))
		{
			return;
		}
		value.autoSaves.Remove(autoSave);
		if (!autoSave.saveDestroyed)
		{
			return;
		}
		ES3ReferenceMgrBase managerFromScene = ES3ReferenceMgrBase.GetManagerFromScene(autoSave.gameObject.scene);
		if (managerFromScene != null)
		{
			long num = managerFromScene.Add(autoSave.gameObject);
			if (num != -1)
			{
				value.destroyedIds.Add(num);
			}
		}
	}

	public void GetAutoSaves()
	{
		autoSaves = new HashSet<ES3AutoSave>();
		GameObject[] rootGameObjects = base.gameObject.scene.GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			autoSaves.UnionWith(gameObject.GetComponentsInChildren<ES3AutoSave>(includeInactive: true));
		}
	}

	private static int GetDepth(Transform t)
	{
		int num = 0;
		while (t.parent != null)
		{
			t = t.parent;
			num++;
		}
		return num;
	}

	private void ManageSlots()
	{
		if (ES3SlotManager.selectedSlotPath != null)
		{
			settings.path = ES3SlotManager.selectedSlotPath;
		}
	}
}
