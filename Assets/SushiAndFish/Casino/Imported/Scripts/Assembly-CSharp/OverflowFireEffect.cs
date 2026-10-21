namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class OverflowFireEffect : MonoBehaviour
{
	[SerializeField]
	private float frameDuration = 0.08f;

	private RawImage _rawImage;

	private Material _instancedMat;

	private float _timer;

	private int _index;

	private void Awake()
	{
		_rawImage = GetComponent<RawImage>();
	}

	private void OnEnable()
	{
		if (_rawImage.material != null && _instancedMat == null)
		{
			_instancedMat = new Material(_rawImage.material);
		}
		if (_instancedMat != null)
		{
			_rawImage.material = _instancedMat;
			_instancedMat.mainTextureScale = new Vector2(1f / 3f, 1f / 3f);
		}
	}

	private void OnDisable()
	{
		if (_instancedMat != null)
		{
			_rawImage.material = null;
		}
	}

	private void OnDestroy()
	{
		if (_instancedMat != null)
		{
			Object.Destroy(_instancedMat);
		}
	}

	private void Update()
	{
		if (!(_instancedMat == null))
		{
			_timer += Time.deltaTime;
			if (!(_timer < frameDuration))
			{
				_timer = 0f;
				_index = (_index + 1) % 9;
				int num = _index / 3;
				int num2 = _index % 3;
				_instancedMat.mainTextureOffset = new Vector2((float)num2 / 3f, 1f - (float)(num + 1) / 3f);
			}
		}
	}
}

}
