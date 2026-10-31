using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingToilet : MonoBehaviour
    {
        public Transform lid;
        // 105 deg swung the lid through the cistern; stop just short of upright.
        [Range(0,105)] public float openAngle=82;
        public float OpenAmount {get;private set;}
        public void SetOpen(float amount)
        {
            OpenAmount=Mathf.Clamp01(amount);
            if(lid)lid.localRotation=Quaternion.Euler(openAngle*OpenAmount,0,0);
        }
    }
}
