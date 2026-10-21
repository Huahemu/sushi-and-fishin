namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ListStateProcessing.Linear;

namespace AirFishLab.ScrollingList.ListStateProcessing
{
	public static class ListStateProcessorManager
	{
		public static void GetProcessors(ListSetupData setupData, out IListMovementProcessor movementProcessor, out IListBoxController boxController)
		{
			ListMovementProcessor listMovementProcessor = new ListMovementProcessor();
			listMovementProcessor.Initialize(setupData);
			ListBoxController listBoxController = new ListBoxController();
			listBoxController.Initialize(setupData);
			listMovementProcessor.SetListBoxController(listBoxController);
			movementProcessor = listMovementProcessor;
			boxController = listBoxController;
		}
	}
}

}
