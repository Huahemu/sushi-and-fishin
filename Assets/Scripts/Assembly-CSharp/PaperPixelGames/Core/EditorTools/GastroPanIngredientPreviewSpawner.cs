using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.EditorTools
{
	public sealed class GastroPanIngredientPreviewSpawner : MonoBehaviour
	{
		private const string EditorPreviewVisualRootName = "__EditorPreviewIngredientVisual";

		[SerializeField]
		private Transform _spawnRoot;

		[SerializeField]
		private GameObject _gastroPanPrefab;

		[SerializeField]
		private List<string> _searchFolders = new List<string> { "Assets/Data/Kitchen/ProcessedIngredient" };

		[SerializeField]
		private List<ProcessedIngredientSO> _manualIngredients = new List<ProcessedIngredientSO>();

		[SerializeField]
		[Min(1f)]
		private int _columns = 8;

		[SerializeField]
		private Vector3 _spacing = new Vector3(0.65f, 0f, 0.65f);

		[SerializeField]
		private bool _spawnNameLabels = true;

		[SerializeField]
		[Min(1f)]
		private byte _previewCount = 1;

		[SerializeField]
		private bool _useMaxPanCapacity = true;

		[SerializeField]
		private bool _addRuntimeInitializer = true;

		[SerializeField]
		private bool _forceEnableAllPreviewChildren = true;

		[SerializeField]
		private bool _forceEnableAllPanChildrenForPreview;

		[SerializeField]
		private bool _clearBeforeSpawnAll = true;

		[SerializeField]
		private bool _clearBeforeSpawnNext;

		[SerializeField]
		private bool _sortByName = true;

		[SerializeField]
		private int _nextSpawnIndex;
	}
}
