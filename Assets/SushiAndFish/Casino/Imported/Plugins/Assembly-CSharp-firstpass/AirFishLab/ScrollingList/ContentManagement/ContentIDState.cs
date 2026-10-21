namespace SushiAndFish.Casino.Original
{
using global::System;

namespace AirFishLab.ScrollingList.ContentManagement
{
	[Flags]
	public enum ContentIDState
	{
		NoContent = 0,
		Valid = 1,
		Underflow = 2,
		Overflow = 4,
		First = 8,
		Last = 0x10
	}
}

}
