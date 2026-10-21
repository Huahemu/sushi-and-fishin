using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan
{
	[DisallowMultipleComponent]
	public sealed class GastroPanPreviewRuntimeInitializer : NetworkBehaviour
	{
		[SerializeField]
		private ProcessedIngredientSO _processedIngredient;

		[SerializeField]
		[Min(1f)]
		private byte _count = 1;

		[SerializeField]
		private bool _destroyEditorPreviewVisualsOnPlay = true;

		[SerializeField]
		private string _editorPreviewVisualRootName = "__EditorPreviewIngredientVisual";

		private GastroPan _gastroPan;

		private bool _initialized;

		private void Awake()
		{
			_gastroPan = GetComponent<GastroPan>();
			if (_gastroPan == null)
			{
				_gastroPan = GetComponentInChildren<GastroPan>(includeInactive: true);
			}
			if (_destroyEditorPreviewVisualsOnPlay)
			{
				DestroyEditorPreviewVisuals();
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				InitializeOnServer();
			}
		}

		public void EditorSetData(ProcessedIngredientSO processedIngredient, byte count, string editorPreviewVisualRootName)
		{
			_processedIngredient = processedIngredient;
			_count = count;
			if (!string.IsNullOrWhiteSpace(editorPreviewVisualRootName))
			{
				_editorPreviewVisualRootName = editorPreviewVisualRootName;
			}
		}

		private void InitializeOnServer()
		{
			if (!_initialized)
			{
				if (_gastroPan == null)
				{
					UtilityMethods.Log("[GastroPanPreviewRuntimeInitializer] Initialize failed. GastroPan is null.");
					return;
				}
				if (_processedIngredient == null)
				{
					UtilityMethods.Log("[GastroPanPreviewRuntimeInitializer] Initialize failed. ProcessedIngredientSO is null.");
					return;
				}
				if (string.IsNullOrEmpty(_processedIngredient.Id))
				{
					UtilityMethods.Log("[GastroPanPreviewRuntimeInitializer] Initialize failed. ProcessedIngredientSO has empty Id: " + _processedIngredient.name);
					return;
				}
				byte safeCount = GetSafeCount();
				_gastroPan.ServerNonChefDeskInitialize(new FixedString64Bytes(_processedIngredient.Id), safeCount);
				_initialized = true;
				UtilityMethods.Log($"[GastroPanPreviewRuntimeInitializer] Initialized. Ingredient: {_processedIngredient.name} | Id: {_processedIngredient.Id} | Count: {safeCount}");
			}
		}

		private byte GetSafeCount()
		{
			int max = Mathf.Max(1, _processedIngredient.MaxPanCapacity);
			return (byte)Mathf.Clamp(_count, 1, max);
		}

		private void DestroyEditorPreviewVisuals()
		{
			if (!string.IsNullOrEmpty(_editorPreviewVisualRootName))
			{
				for (int num = base.transform.childCount - 1; num >= 0; num--)
				{
					DestroyEditorPreviewVisualsRecursive(base.transform.GetChild(num));
				}
			}
		}

		private void DestroyEditorPreviewVisualsRecursive(Transform current)
		{
			if (current == null)
			{
				return;
			}
			if (current.name == _editorPreviewVisualRootName)
			{
				Object.Destroy(current.gameObject);
				return;
			}
			for (int num = current.childCount - 1; num >= 0; num--)
			{
				DestroyEditorPreviewVisualsRecursive(current.GetChild(num));
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "GastroPanPreviewRuntimeInitializer";
		}
	}
}
