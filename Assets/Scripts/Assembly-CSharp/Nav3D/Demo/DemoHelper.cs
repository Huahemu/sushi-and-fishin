using UnityEngine;

namespace Nav3D.Demo
{
	public static class DemoHelper
	{
		private const string PREFAB_CONE_PATH = "AgentBodyCone";

		private const float RADIUS_TO_SCALE_FACTOR = 20f;

		public static GameObject InstantiateAgentConeBody(string _Name, float _Radius, out Material _Material, Material _SourceMaterial = null, Color? _BodyColor = null)
		{
			GameObject original = Resources.Load<GameObject>("AgentBodyCone");
			GameObject gameObject = new GameObject(_Name);
			GameObject gameObject2 = Object.Instantiate(original, Vector3.zero, Quaternion.identity);
			gameObject2.transform.SetParent(gameObject.transform);
			if (_SourceMaterial != null)
			{
				_Material = _SourceMaterial;
			}
			else
			{
				_Material = new Material(Shader.Find("Legacy Shaders/Diffuse"));
				_Material.color = _BodyColor ?? Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
			}
			gameObject2.transform.Find("agentBodyMesh").GetComponent<Renderer>().material = _Material;
			float num = _Radius * 20f;
			gameObject2.transform.localScale = new Vector3(num, num, num);
			return gameObject;
		}

		public static void ScaleAgentConeBody(GameObject _ConeAgent, float _Radius)
		{
			float num = _Radius * 20f;
			(_ConeAgent.transform.Find("AgentBodyCone") ?? _ConeAgent.transform.Find("AgentBodyCone(Clone)")).localScale = new Vector3(num, num, num);
		}
	}
}
