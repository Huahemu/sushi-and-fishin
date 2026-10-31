using UnityEngine;
namespace PaperPixelGames.Core.Ai.NPCs
{
    public sealed class ResidentActivityResourceLifetime:MonoBehaviour
    {
        internal int actorInstance;
        void OnDisable(){ResidentActivityResourceReservations.ReleaseActor(actorInstance);}
        void OnDestroy(){ResidentActivityResourceReservations.ReleaseActor(actorInstance);}
    }
}
