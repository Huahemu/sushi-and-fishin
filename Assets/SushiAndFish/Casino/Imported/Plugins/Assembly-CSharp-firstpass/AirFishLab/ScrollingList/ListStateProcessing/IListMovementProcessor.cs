namespace SushiAndFish.Casino.Original
{
namespace AirFishLab.ScrollingList.ListStateProcessing
{
	public interface IListMovementProcessor
	{
		void Initialize(ListSetupData setupData);

		void SetMovement(InputInfo inputInfo);

		void SetUnitMovement(int unit);

		void SetSelectionMovement(int units);

		float GetMovement(float detailTime);

		bool IsMovementEnded();

		bool NeedToAlign();

		void EndMovement(bool toAlign);
	}
}

}
