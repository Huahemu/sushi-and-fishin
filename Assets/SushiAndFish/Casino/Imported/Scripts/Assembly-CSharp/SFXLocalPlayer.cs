namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::UnityEngine;

public class SFXLocalPlayer : MonoBehaviour
{
	[SerializeField]
	private EventReference eventReference;

	public SFXParams[] fmodParams;

	public void PlayOneShotWith3DPos()
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot(eventReference, base.gameObject.transform.position);
		}
	}

	public void PlayOneShotWithCustom3DPos(Vector3 pos)
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot(eventReference, pos);
		}
	}

	public void PlayOneShotOverrideParams()
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShotWithParameters(eventReference, fmodParams, base.gameObject.transform.position);
		}
	}

	public void PlayOneShotWithPitchMod(float pitch = 1f)
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShotWithParameters(eventReference, null, base.transform.position, pitch);
		}
	}
}

}
