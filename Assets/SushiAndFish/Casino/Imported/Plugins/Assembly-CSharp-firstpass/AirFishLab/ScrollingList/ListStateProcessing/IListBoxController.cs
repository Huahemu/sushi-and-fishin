namespace SushiAndFish.Casino.Original
{
namespace AirFishLab.ScrollingList.ListStateProcessing
{
	public interface IListBoxController
	{
		void Initialize(ListSetupData setupData);

		void UpdateBoxes(float movementValue);

		void RefreshBoxes(int focusingContentID = -1);

		IListBox GetFocusingBox();
	}
}

}
