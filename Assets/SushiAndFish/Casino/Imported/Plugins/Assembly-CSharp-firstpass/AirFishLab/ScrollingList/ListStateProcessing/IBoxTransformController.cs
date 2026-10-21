namespace SushiAndFish.Casino.Original
{
namespace AirFishLab.ScrollingList.ListStateProcessing
{
	public interface IBoxTransformController
	{
		void SetInitialLocalTransform(IListBox box, int boxID);

		BoxPositionState UpdateLocalTransform(IListBox box, float deltaPos);
	}
}

}
