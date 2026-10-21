namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class LightbakerManager : MonoSingleton<LightbakerManager>
{
	public List<LightVolume> lightVolumes;

	public LightVolume targetLightVolume;

	public void UpdateActiveLightVolume()
	{
		SetShaderVars();
	}

	public void SetLightVolume(int index)
	{
		targetLightVolume = lightVolumes[index];
		SetShaderVars();
	}

	private void SetShaderVars()
	{
		Texture3D value = SushiAndFish.Casino.Original.CasinoResources.Load<Texture3D>(targetLightVolume.fileName);
		Shader.SetGlobalTexture("_LightMap", value);
		Shader.SetGlobalFloat("brightness", targetLightVolume.brightness);
		Shader.SetGlobalFloat("ambienceStrength", targetLightVolume.ambienceStrength);
		Shader.SetGlobalFloat("ambienceMin", targetLightVolume.ambienceMin);
		Shader.SetGlobalVector("gridRes", (Vector3)targetLightVolume.gridRes);
		Shader.SetGlobalFloat("raySpacing", targetLightVolume.raySpacing);
		Shader.SetGlobalVector("gridOffset", targetLightVolume.gridOffset);
	}
}

}
