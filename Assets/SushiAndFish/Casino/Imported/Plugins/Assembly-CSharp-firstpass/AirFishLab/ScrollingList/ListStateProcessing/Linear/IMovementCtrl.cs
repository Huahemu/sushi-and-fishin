namespace SushiAndFish.Casino.Original
{
namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public interface IMovementCtrl
	{
		void SetMovement(float baseValue, bool flag);

		bool IsMovementEnded();

		float GetDistance(float deltaTime);

		void EndMovement();
	}
}

}
