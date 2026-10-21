using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    [DefaultExecutionOrder(-31000)]
    public sealed class HousingEscapeGuard : MonoBehaviour
    {
        HousingRestSession session;
        void Awake(){session=GetComponent<HousingRestSession>();}
        void Update(){if(session)session.BeforePlayerUpdate();}
    }
}
