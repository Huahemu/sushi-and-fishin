using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    [DefaultExecutionOrder(32700)]
    public sealed class HousingRestCamera : MonoBehaviour
    {
        HousingRestSession session;
        void Awake(){session=GetComponent<HousingRestSession>();}
        void LateUpdate(){if(session&&session.Active)session.ApplyThirdPersonCamera();}
    }
}
