using UnityEngine;

namespace SushiAndFish.FrontEnd
{
    // The overhead menu uses existing world meshes. Retain a visible authored
    // LOD at this camera height instead of the walking-camera cull threshold.
    public sealed class IslandBackdropPresentation : MonoBehaviour
    {
        private void OnEnable()
        {
            foreach(var group in GetComponentsInChildren<LODGroup>(true))
            {
                var lods=group.GetLODs();
                if(lods.Length>0)group.ForceLOD(Mathf.Min(1,lods.Length-1));
            }
        }
    }
}
