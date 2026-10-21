namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public struct SFXParams
{
	public string name;

	public float value;

	public SFXParams(string n, float v)
	{
		name = n;
		value = v;
	}
}

}
