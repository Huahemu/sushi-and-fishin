using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingToilet : MonoBehaviour
    {
        public Transform lid;
        public float OpenAmount {get;private set;}
        public void SetOpen(float amount)
        {
            OpenAmount=Mathf.Clamp01(amount);
            if(lid)lid.localRotation=Quaternion.Euler(105*OpenAmount,0,0);
        }
    }
}
