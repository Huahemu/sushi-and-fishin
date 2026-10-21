using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
namespace SushiAndFish.ResidentialIsland
{
    public sealed class HousingTextMaterial : MonoBehaviour
    {
        TMP_Text text;
        readonly Dictionary<int,Material> replacements=new Dictionary<int,Material>();
        readonly HashSet<int> ownedIds=new HashSet<int>();
        Material Own(Material source)
        {
            if(!source||ownedIds.Contains(source.GetInstanceID()))return source;
            int id=source.GetInstanceID();if(replacements.TryGetValue(id,out var cached))return cached;
            var copy=new Material(HousingIsland.Instance.Catalog.textShader){name="HousingText_"+source.name,hideFlags=HideFlags.DontSave,renderQueue=3101};
            if(source.HasProperty("_MainTex"))copy.SetTexture("_MainTex",source.GetTexture("_MainTex"));
            if(copy.HasProperty("_ZTestMode"))copy.SetFloat("_ZTestMode",(float)CompareFunction.Always);
            if(copy.HasProperty("_CullMode"))copy.SetFloat("_CullMode",0);
            replacements[id]=copy;ownedIds.Add(copy.GetInstanceID());return copy;
        }
        void Awake(){text=GetComponent<TMP_Text>();text.fontSharedMaterial=Own(text.fontSharedMaterial);}
        void LateUpdate()
        {
            // Preserve TMP's generated indices for every atlas page.
            if(text.fontSharedMaterial&&!ownedIds.Contains(text.fontSharedMaterial.GetInstanceID()))text.fontSharedMaterial=Own(text.fontSharedMaterial);
            foreach(var sub in GetComponentsInChildren<TMP_SubMeshUI>(true))
                if(sub.sharedMaterial&&!ownedIds.Contains(sub.sharedMaterial.GetInstanceID()))sub.sharedMaterial=Own(sub.sharedMaterial);
        }
        void OnDestroy(){foreach(var mat in replacements.Values)if(mat)Destroy(mat);}
    }
}
