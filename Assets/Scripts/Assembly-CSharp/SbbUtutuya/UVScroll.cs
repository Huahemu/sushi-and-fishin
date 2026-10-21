using UnityEngine;

namespace SbbUtutuya
{
	public class UVScroll : MonoBehaviour
	{
		[SerializeField]
		private Vector2 m_uvScrollSpeed = new Vector2(0f, -0.24f);

		private Renderer m_renderer;

		private void Start()
		{
			m_renderer = GetComponent<Renderer>();
		}

		private void FixedUpdate()
		{
			if ((bool)m_renderer && (bool)m_renderer.material)
			{
				m_renderer.material.mainTextureOffset += m_uvScrollSpeed * Time.deltaTime;
			}
		}
	}
}
