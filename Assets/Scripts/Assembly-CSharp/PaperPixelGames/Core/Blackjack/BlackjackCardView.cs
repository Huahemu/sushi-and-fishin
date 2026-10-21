using UnityEngine;

namespace PaperPixelGames.Core.Blackjack
{
	public class BlackjackCardView : MonoBehaviour
	{
		[Header("Card Renderers")]
		[SerializeField]
		private Renderer _frontRenderer;

		[SerializeField]
		private Renderer _backRenderer;

		[Header("Shader Texture Properties")]
		[SerializeField]
		private string _frontTextureProperty = "_BaseMap";

		[SerializeField]
		private string _backTextureProperty = "_BaseMap";

		private MaterialPropertyBlock _frontBlock;

		private MaterialPropertyBlock _backBlock;

		public void Setup(Texture frontTexture, Texture backTexture)
		{
			if (_frontRenderer != null && frontTexture != null)
			{
				if (_frontBlock == null)
				{
					_frontBlock = new MaterialPropertyBlock();
				}
				_frontRenderer.GetPropertyBlock(_frontBlock);
				_frontBlock.SetTexture(_frontTextureProperty, frontTexture);
				_frontRenderer.SetPropertyBlock(_frontBlock);
			}
			if (_backRenderer != null && backTexture != null)
			{
				if (_backBlock == null)
				{
					_backBlock = new MaterialPropertyBlock();
				}
				_backRenderer.GetPropertyBlock(_backBlock);
				_backBlock.SetTexture(_backTextureProperty, backTexture);
				_backRenderer.SetPropertyBlock(_backBlock);
			}
		}
	}
}
