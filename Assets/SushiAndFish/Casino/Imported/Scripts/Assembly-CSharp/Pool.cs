namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[Serializable]
public class Pool
{
	public SlotType slot;

	[TextArea(2, 8)]
	public List<string> items = new List<string>();
}

}
