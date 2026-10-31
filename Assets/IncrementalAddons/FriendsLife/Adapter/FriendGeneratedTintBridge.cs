using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;
namespace SushiAndFish.FriendsLife
{
    // The generated surface uses one explicit base-tint channel per independent
    // Body/Head/Hair/Cloth part, retaining the original creator's colour controls.
    public sealed class FriendGeneratedTintBridge:MonoBehaviour
    {
        OutfitSystem system;MaterialPropertyBlock block;
        readonly HashSet<Outfit> routed=new HashSet<Outfit>();
        void Awake(){system=GetComponent<OutfitSystem>();block=new MaterialPropertyBlock();if(system)system.OnOutfitChanged+=RouteGeneratedColors;}
        void OnDestroy()
        {if(system)system.OnOutfitChanged-=RouteGeneratedColors;foreach(var outfit in routed)if(outfit)outfit.OnColorChanged-=GeneratedColorChanged;routed.Clear();}
        void RouteGeneratedColors(Outfit outfit)
        {
            if(!outfit||outfit.mergeTexture||!outfit.GetComponent<FriendGeneratedOutfitIdentity>())return;
            // Native AttachOutfit subscribes even custom, unbaked outfits to its
            // atlas rebake. Such a bake is unrelated to this UV surface and asks
            // the photo shader for stock ID-map data. Keep native SetColor and
            // serialization, but route only these generated parts to their MPB.
            if(outfit.OnColorChanged!=null)foreach(var callback in outfit.OnColorChanged.GetInvocationList())
                if(callback.Target==system&&callback.Method.DeclaringType==typeof(OutfitSystem)&&callback.Method.Name=="UpdateTexture")
                    outfit.OnColorChanged-=(UnityEngine.Events.UnityAction<Outfit>)callback;
            outfit.OnColorChanged-=GeneratedColorChanged;outfit.OnColorChanged+=GeneratedColorChanged;
            routed.RemoveWhere(item=>!item);routed.Add(outfit);
        }
        void GeneratedColorChanged(Outfit outfit)=>Apply();
        void LateUpdate()=>Apply();
        public void Apply()
        {
            if(!system)system=GetComponent<OutfitSystem>();if(!system)return;if(block==null)block=new MaterialPropertyBlock();var combined=system.GetCharacterBody();var combinedMaterials=combined?combined.sharedMaterials:null;
            Color hair=Tint("HairFront","HairBack"),top=Tint("Top"),bottom=Tint("Bottom"),skin=Tint("Body"),face=Tint("Head"),shoes=Tint("Feet","Shoes");
            foreach(var outfit in system.GetOutfits())
            {
            if(!outfit||!outfit.TryGetComponent<FriendGeneratedOutfitIdentity>(out var identity)||outfit.colors==null||outfit.colors.Length==0)continue;var tint=outfit.colors[0];tint.a=1;
                if(!routed.Contains(outfit))RouteGeneratedColors(outfit);
                // Custom unbaked materials do not receive BMAC's texture-atlas
                // controlMaterial binding. Their real destination is a material
                // slot of the merged ProxyMesh, not the hidden source outfit.
                bool bound=false;if(combinedMaterials!=null)for(int index=0;index<combinedMaterials.Length;index++)
                {var material=combinedMaterials[index];if(!material)continue;if(material!=outfit.material&&material.GetTag("FriendsPhotoPart",false,"")!=identity.resourcePath&&!material.name.StartsWith(identity.resourcePath,System.StringComparison.Ordinal))continue;combined.GetPropertyBlock(block,index);block.SetColor("_Color_1",tint);RegionColors(hair,top,bottom,skin,face,shoes);combined.SetPropertyBlock(block,index);if(!bound){outfit.SetControlMaterial(combined,index);bound=true;}}
                foreach(var renderer in outfit.GetComponentsInChildren<Renderer>(true)){renderer.GetPropertyBlock(block);block.SetColor("_Color_1",tint);RegionColors(hair,top,bottom,skin,face,shoes);renderer.SetPropertyBlock(block);}
            }
        }
        Color Tint(string type,string alternative=null)
        {var outfit=system.GetOutfit(type);if(!outfit&&alternative!=null)outfit=system.GetOutfit(alternative);if(!outfit||outfit.colors==null||outfit.colors.Length==0)return Color.white;var tint=outfit.colors[0];tint.a=1;return tint;}
        void RegionColors(Color hair,Color top,Color bottom,Color skin,Color face,Color shoes)
        {block.SetColor("_RegionHair",hair);block.SetColor("_RegionTop",top);block.SetColor("_RegionBottom",bottom);block.SetColor("_RegionSkin",skin);block.SetColor("_RegionFace",face);block.SetColor("_RegionShoes",shoes);}
    }
}
