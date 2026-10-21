namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class DragonTowerButton : InteractableBase
{
	public enum ButtonState
	{
		Inactive = 0,
		Clickable = 1,
		Red = 2,
		Green = 3,
		RevealEgg = 4
	}

	[SerializeField]
	private DragonTower dragonTower;

	[SerializeField]
	private int floorIndex;

	[SerializeField]
	private int buttonIndex;

	[SerializeField]
	private MeshRenderer meshRenderer;

	[SerializeField]
	private Transform eggTransform;

	[SerializeField]
	private ParticleSystem explosionVfx;

	[SerializeField]
	private MMF_Player pressFb;

	[SerializeField]
	private List<Material> materials = new List<Material>();

	public ButtonState buttonState;

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		dragonTower.OnPressButton(floorIndex, buttonIndex, this);
	}

	public override void RpcOnInteract(PlayerInteract playerInteract)
	{
		base.RpcOnInteract(playerInteract);
		pressFb.PlayFeedbacks();
	}

	[Server]
	public void ServerSetButtonState(ButtonState state)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void DragonTowerButton::ServerSetButtonState(DragonTowerButton/ButtonState)' called when server was not active");
			return;
		}
		SetButtonState(state);
		RpcSetButtonState(state);
	}

	[ClientRpc]
	private void RpcSetButtonState(ButtonState state)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_DragonTowerButton_002FButtonState(writer, state);
		SendRPCInternal("System.Void DragonTowerButton::RpcSetButtonState(DragonTowerButton/ButtonState)", -1637390773, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetButtonState(ButtonState state)
	{
		buttonState = state;
		switch (state)
		{
		case ButtonState.Inactive:
			IsInteractable = false;
			meshRenderer.material = materials[0];
			eggTransform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
			{
				eggTransform.gameObject.SetActive(value: false);
			});
			break;
		case ButtonState.Clickable:
			IsInteractable = true;
			meshRenderer.material = materials[1];
			break;
		case ButtonState.Red:
			IsInteractable = false;
			meshRenderer.material = materials[2];
			eggTransform.gameObject.SetActive(value: true);
			eggTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
			explosionVfx.Play();
			break;
		case ButtonState.Green:
			IsInteractable = false;
			meshRenderer.material = materials[3];
			break;
		case ButtonState.RevealEgg:
			eggTransform.gameObject.SetActive(value: true);
			eggTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
			break;
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetButtonState__ButtonState(ButtonState state)
	{
		if (!base.isServer)
		{
			SetButtonState(state);
		}
	}

	protected static void InvokeUserCode_RpcSetButtonState__ButtonState(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetButtonState called on server.");
		}
		else
		{
			((DragonTowerButton)obj).UserCode_RpcSetButtonState__ButtonState(GeneratedNetworkCode._Read_DragonTowerButton_002FButtonState(reader));
		}
	}

	static DragonTowerButton()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DragonTowerButton), "System.Void DragonTowerButton::RpcSetButtonState(DragonTowerButton/ButtonState)", InvokeUserCode_RpcSetButtonState__ButtonState);
	}
}

}
