namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;

public class Upgrade : ConsumableItem
{
	[SerializeField]
	private Animator anim;

	[SerializeField]
	private TextMeshPro typeText;

	[SerializeField]
	private TextMeshPro valueText;

	[SerializeField]
	private PlayerUpgradeType upgradeType;

	[SerializeField]
	private float value;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent upgradeSfx;

	private bool _hasBeenUsed;

	private PlayerProfile _holderProfile;

	public void Start()
	{
		typeText.text = upgradeType.ToString();
		valueText.text = (value * 100f).ToString("0.#") + "%";
	}

	protected override void OnUseItem(bool isPressed)
	{
		if (!_hasBeenUsed)
		{
			_hasBeenUsed = true;
			_holderProfile = base.NetworkHolder.GetComponent<PlayerProfile>();
			anim.SetTrigger("Use");
			if (base.isServer)
			{
				StartCoroutine(UseRoutine());
			}
		}
	}

	private IEnumerator UseRoutine()
	{
		yield return new WaitForSecondsRealtime(0.5f);
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
		{
			NetworkSingleton<UpgradeManager>.Instance.ChangeUpgradeData(_holderProfile.steamId, upgradeType, value);
		}
		upgradeSfx.RpcPlayOneShotWith3DPos();
		DestroyItem();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
