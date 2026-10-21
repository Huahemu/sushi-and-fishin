using System;
using System.Collections.Generic;
using UnityEngine;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingHouse : MonoBehaviour
    {
        public HousingType definition;
        public string plot;
        public HousingSeatAnchor[] chairs;
        public Transform entry, exit;
        [NonSerialized] public readonly List<GameObject> spawnedSeats=new List<GameObject>();
        void OnDestroy(){foreach(var seat in spawnedSeats)if(seat)Destroy(seat);}
    }
}
