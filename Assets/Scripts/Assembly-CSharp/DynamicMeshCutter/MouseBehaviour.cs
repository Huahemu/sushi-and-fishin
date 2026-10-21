using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
	[RequireComponent(typeof(LineRenderer))]
	public class MouseBehaviour : CutterBehaviour
	{
		private Vector3 _from;

		private Vector3 _to;

		private bool _isDragging;

		public LineRenderer LR => GetComponent<LineRenderer>();

		protected override void Update()
		{
			base.Update();
			if (Input.GetMouseButtonDown(0))
			{
				_isDragging = true;
				Vector3 position = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane + 0.05f);
				_from = Camera.main.ScreenToWorldPoint(position);
			}
			if (_isDragging)
			{
				Vector3 position2 = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane + 0.05f);
				_to = Camera.main.ScreenToWorldPoint(position2);
				VisualizeLine(value: true);
			}
			else
			{
				VisualizeLine(value: false);
			}
			if (Input.GetMouseButtonUp(0) && _isDragging)
			{
				Cut();
				_isDragging = false;
			}
		}

		private void Cut()
		{
			Plane plane = new Plane(_from, _to, Camera.main.transform.position);
			GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			foreach (GameObject gameObject in rootGameObjects)
			{
				if (gameObject.activeInHierarchy)
				{
					MeshTarget[] componentsInChildren = gameObject.GetComponentsInChildren<MeshTarget>();
					foreach (MeshTarget target in componentsInChildren)
					{
						Cut(target, _to, plane.normal, null, OnCreated);
					}
				}
			}
		}

		private void OnCreated(Info info, MeshCreationData cData)
		{
			MeshCreation.TranslateCreatedObjects(info, cData.CreatedObjects, cData.CreatedTargets, Separation);
		}

		private void VisualizeLine(bool value)
		{
			if (!(LR == null))
			{
				LR.enabled = value;
				if (value)
				{
					LR.positionCount = 2;
					LR.SetPosition(0, _from);
					LR.SetPosition(1, _to);
				}
			}
		}
	}
}
