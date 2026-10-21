using System;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace SushiAndFish.CityExpansion
{
    public sealed class CityAutomaticDoor : MonoBehaviour
    {
        public Transform leftLeaf,rightLeaf,doorway;
        public Vector3 leftClosed,rightClosed,leftOpen,rightOpen;
        public float duration=.7f;
        float openness,nextSense,holdUntil;
        public bool IsOpen => openness>.98f;
        public float Openness => openness;
        void Update()
        {
            if(!leftLeaf||!rightLeaf||!doorway)return;
            if(Time.unscaledTime>=nextSense)
            {
                nextSense=Time.unscaledTime+.12f;
                var player=Singleton<PlayersManager>.Instance?.LocalPlayer;
                bool occupied=player&&Near(player.transform.position);
                var residents=Singleton<ResidentManager>.Instance?.Residents;
                if(!occupied&&residents!=null)
                    foreach(var npc in residents)if(npc&&npc.gameObject.activeInHierarchy&&Near(npc.transform.position)){occupied=true;break;}
                if(occupied)holdUntil=Time.unscaledTime+.9f;
            }
            openness=Mathf.MoveTowards(openness,Time.unscaledTime<holdUntil?1:0,Time.unscaledDeltaTime/Mathf.Max(.1f,duration));
            float smooth=openness*openness*(3-2*openness);
            leftLeaf.localPosition=Vector3.LerpUnclamped(leftClosed,leftOpen,smooth);
            rightLeaf.localPosition=Vector3.LerpUnclamped(rightClosed,rightOpen,smooth);
        }
        bool Near(Vector3 point)
        {
            Vector3 p=doorway.InverseTransformPoint(point);
            return Mathf.Abs(p.x)<1.95f&&Mathf.Abs(p.z)<2.35f&&p.y>-.5f&&p.y<3.1f;
        }
    }
}
