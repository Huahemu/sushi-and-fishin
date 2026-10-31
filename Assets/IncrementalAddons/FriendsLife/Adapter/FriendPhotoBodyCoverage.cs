using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Covered inferred body is retained for wardrobe changes. Only that prior
    // is occluded while its measured photo garment is equipped. Observed scan
    // triangles and the explicitly inferred missing-arm connection stay intact.
    public sealed class FriendPhotoBodyCoverage:MonoBehaviour,IOutfitExtension
    {
        [Serializable] public sealed class Section
        {public SkinnedMeshRenderer renderer;public int[] triangles,coverage;}
        public string packageId;public Section[] sections;
        OutfitSystem system;readonly List<Mesh> owned=new List<Mesh>();
        public string GetID()=>"FriendPhotoBodyCoverage";
        public object GetValue()=>null;
        public Type GetValueType()=>null;
        public void Execute(OutfitSystem outfitSystem,Outfit outfit){}
        public void Initalize(OutfitSystem outfitSystem,Outfit outfit)
        {
            if(system)return;system=outfitSystem;
            foreach(var section in sections??Array.Empty<Section>())if(section.renderer&&section.renderer.sharedMesh)
            {var mesh=UnityEngine.Object.Instantiate(section.renderer.sharedMesh);mesh.name+="_BodyCoverage";section.renderer.sharedMesh=mesh;owned.Add(mesh);}
            system.OnTagsChanged+=Apply;Apply(null);
        }
        bool Wearing(string type)
        {var outfit=system.GetOutfit(type);var identity=outfit?outfit.GetComponent<FriendGeneratedOutfitIdentity>():null;return identity&&identity.resourcePath==type+"/FriendsGenerated/"+packageId+"/"+type;}
        void Apply(List<string> unused)
        {
            if(!system)return;bool top=Wearing("Top"),bottom=Wearing("Bottom"),feet=Wearing("Feet");
            foreach(var section in sections??Array.Empty<Section>())
            {
                if(!section.renderer||!section.renderer.sharedMesh)continue;var triangles=new List<int>(section.triangles.Length);
                for(int face=0;face<section.coverage.Length;face++)
                {int cover=section.coverage[face];if((cover==1&&top)||(cover==2&&bottom)||(cover==3&&feet))continue;triangles.Add(section.triangles[face*3]);triangles.Add(section.triangles[face*3+1]);triangles.Add(section.triangles[face*3+2]);}
                section.renderer.sharedMesh.triangles=triangles.ToArray();
            }
        }
        void OnDestroy()
        {if(system)system.OnTagsChanged-=Apply;foreach(var mesh in owned)if(mesh)UnityEngine.Object.Destroy(mesh);owned.Clear();}
    }
}
