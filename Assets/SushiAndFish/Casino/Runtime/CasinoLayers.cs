using System;
using System.Collections.Generic;
using UnityEngine;

namespace SushiAndFish.Casino
{
    public static class CasinoLayers
    {
        public static readonly int[] Map = {0,1,2,3,4,5,6,28,29,30,24,17,31,10,14,15,26,25,0,0,20,21,22,23,24,25,0,27,28,29,21,0};
        public static readonly string[] SourceNames = {"Default","TransparentFX","Ignore Raycast","Ground","Water","UI","Player","PlinkoPuck","DiceZone","Dice","DisableOutline","Graph","PlinkoWalls","SelfMeshPlayer","BlindnessVolume","LensVolume","Mirror","SlotScreen","Coin","CoinFlipZone","","","","","","","Bat","","","","BoundCollider","Glass"};
        public static int NameToLayer(string name)
        {
            int source=Array.IndexOf(SourceNames,name);
            return source>=0?Map[source]:LayerMask.NameToLayer(name);
        }
        public static int GetMask(params string[] names)
        {
            int mask=0;foreach(string name in names){int layer=NameToLayer(name);if(layer>=0)mask|=1<<layer;}return mask;
        }
        public static int Mask(int mask)
        {
            uint result=0;uint bits=unchecked((uint)mask);
            for(int i=0;i<32;i++)if((bits&(1u<<i))!=0)result|=1u<<Map[i];
            return unchecked((int)result);
        }
        // Only formerly unused host layers receive new collision rules. Rules
        // between every pair of pre-existing host layers remain untouched.
        public static void ConfigurePhysics()
        {
            var sourceRows=new Dictionary<int,uint>
            {
                {7,0xffe3fe7fu},{8,0xffe3ee08u},{9,0xffe3efbfu},{12,0xffe3e080u}
            };
            int[] originalForHost=new int[32];for(int i=0;i<32;i++)originalForHost[i]=i;
            originalForHost[0]=0;originalForHost[3]=3;originalForHost[6]=6;
            originalForHost[28]=7;originalForHost[29]=8;originalForHost[30]=9;originalForHost[31]=12;
            originalForHost[10]=13;originalForHost[14]=14;originalForHost[15]=15;originalForHost[17]=11;originalForHost[21]=30;originalForHost[24]=10;originalForHost[25]=17;originalForHost[26]=16;
            foreach(var row in sourceRows)
                for(int host=0;host<32;host++)Physics.IgnoreLayerCollision(Map[row.Key],host,(row.Value&(1u<<originalForHost[host]))==0);
        }
    }
}
