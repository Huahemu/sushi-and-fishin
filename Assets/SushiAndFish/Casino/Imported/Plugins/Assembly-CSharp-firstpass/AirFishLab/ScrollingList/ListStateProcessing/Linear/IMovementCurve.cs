namespace SushiAndFish.Casino.Original
{
namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	internal interface IMovementCurve
	{
		void SetMovement(float factor);

		bool IsMovementEnded();

		void EndMovement();

		float GetDistance(float deltaTime);
	}
}

}
