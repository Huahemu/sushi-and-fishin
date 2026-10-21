namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class CameraDoRender : MonoBehaviour
{
	private void Awake()
	{
		TryGetComponent<Camera>(out var component);
		if(CasinoRuntimeContext.Active) SushiAndFish.Casino.CasinoHostRepair.RepairOffscreenCamera(component);
		component.Render();
	}
}

}
