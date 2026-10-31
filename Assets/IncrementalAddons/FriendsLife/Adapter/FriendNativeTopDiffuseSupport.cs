using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    // An owned-rig source adapter, not an alteration of the vendor shader,
    // renderer, source material asset, model, saved wardrobe or global quality.
    public sealed class FriendNativeTopDiffuseSupport : MonoBehaviour
    {
        const string ResourceKey="FriendsLife/Photo/NativeTopTshirtDiffuse";
        const string ExpectedPngSha256="06704f250c900cb6133931503585a6da26c173ee3f48ff6fd46fc128525f60fa";
        const string SourceTextureName="Top_Shirt_D",SourceShaderName="Shader Graphs/BSMC_ClothesCustom";
        sealed class TextureCache {public Texture2D texture;public int references;}
        static TextureCache cache;
        static bool loadFailureReported;
        OutfitSystem system;FriendAppearanceOwnedRig marker;bool subscribed;
        sealed class Binding
        {public Outfit outfit;public Material original,owned;public TextureCache textureLease;}
        readonly Dictionary<Outfit,Binding> bindings=new Dictionary<Outfit,Binding>();
        public int PatchedOutfitCount=>bindings.Count;
        public static int LiveTextureReferences=>cache?.references??0;
        public static Texture2D LiveAuthoredTexture=>cache?.texture;
        public static string AuthoredPngSha256=>ExpectedPngSha256;

        public static void Attach(OutfitSystem target)
        {
            if(!Application.isPlaying||!target||!target.GetComponent<FriendAppearanceOwnedRig>())return;
            var support=target.GetComponent<FriendNativeTopDiffuseSupport>();
            if(!support)support=target.gameObject.AddComponent<FriendNativeTopDiffuseSupport>();
            support.Bind();
        }
        void Awake(){Bind();}
        void OnEnable(){Bind();}
        void Bind()
        {
            var current=GetComponent<OutfitSystem>();var ownedMarker=GetComponent<FriendAppearanceOwnedRig>();
            if(!Application.isPlaying||!current||!ownedMarker)return;
            if(system&&system!=current)ReleaseAll();system=current;
            marker=ownedMarker;
            if(!subscribed){system.OnOutfitChanged+=Changed;subscribed=true;}
            // MarkOwnedClone callers load natively immediately afterwards.
            // Patch existing source pointers, but never start another merge.
            if(system.initalized)Changed(system.GetOutfit("Top"));
        }
        void Changed(Outfit changed)
        {
            if(!system||!marker)return;
            RemoveReplacedBindings();
            if(!changed||system.GetOutfit("Top")!=changed||bindings.ContainsKey(changed))return;
            if(!MatchesNativeTop(changed,out var original))return;
            Material owned=null;TextureCache borrowed=null;Binding registered=null;
            void Rollback()
            {
                if(registered!=null){Release(registered);return;}
                if(owned)DisposeOwned(owned);if(borrowed!=null)ReleaseTexture(borrowed);
            }
            try
            {
                borrowed=AcquireTexture();var texture=borrowed.texture;
                owned=new Material(original){name="Friend native Top_Tshirt source (owned)",hideFlags=HideFlags.DontSave};
                // Both setters address this private copy. Keep all existing
                // shader/keyword/colour/pattern/decal/scale/offset state.
                owned.SetTexture("_Diffuse",texture);owned.mainTexture=texture;
                if(!system||system.GetOutfit("Top")!=changed||!changed||changed.material!=original)
                    throw new OperationCanceledException("Owned Top source changed before binding");
                var binding=new Binding{outfit=changed,original=original,owned=owned,textureLease=borrowed};
                bindings.Add(changed,binding);registered=binding;changed.material=owned;FriendNativeTopDiffuseSweep.Track(this);
                // Native AttachOutfit invokes this callback before its existing
                // MergeCharacter/requested merge. No source input is replayed.
            }
            catch(OperationCanceledException){Rollback();}
            catch(Exception error)
            {
                Rollback();
                if(!loadFailureReported){loadFailureReported=true;Debug.LogWarning("[FriendsLife] Native Top source retained: "+error.Message,this);}
            }
        }
        static bool MatchesNativeTop(Outfit outfit,out Material material)
        {
            material=null;
            if(!outfit.Type||outfit.Type.name!="Top"||outfit.name.Replace("(Clone)","").Trim()!="Top_Tshirt"||
                outfit.customShader||!outfit.mergeTexture||!outfit.mergeMesh)return false;
            var renderers=outfit.skinnedRenderers;
            if(renderers==null||renderers.Length!=1||!renderers[0]||!renderers[0].sharedMesh)return false;
            var mesh=renderers[0].sharedMesh;
            if(mesh.vertexCount!=1258||mesh.name.Replace("(Clone)","").Trim()!="Top_SimpleTShirt")return false;
            // Runtime Resources identity works in a packaged Player. Names
            // and vertex counts alone cannot authorize replacing an input.
            var template=Resources.Load<Outfit>("top/Top_Tshirt");
            var authored=template?template.GetComponentsInChildren<SkinnedMeshRenderer>(true):null;
            if(authored==null||authored.Length!=1||!authored[0]||!ReferenceEquals(mesh,authored[0].sharedMesh))return false;
            var templateMaterial=authored[0].sharedMaterial;
            material=outfit.material;
            if(!material||!material.shader||material.shader.name!=SourceShaderName||!material.HasProperty("_Diffuse")||
                !templateMaterial||!ReferenceEquals(material.shader,templateMaterial.shader))return false;
            var texture=material.GetTexture("_Diffuse") as Texture2D;
            // A custom user texture/material is not replaced. The live field
            // and native main-texture alias must both be this exact source.
            return texture&&texture.name==SourceTextureName&&texture.width==2048&&texture.height==2048&&
                ReferenceEquals(material.mainTexture,texture)&&ReferenceEquals(templateMaterial.GetTexture("_Diffuse"),texture);
        }
        static TextureCache AcquireTexture()
        {
            if(cache!=null&&cache.texture){cache.references++;return cache;}
            var resource=Resources.Load<TextAsset>(ResourceKey);
            if(!resource)throw new InvalidDataException("Native Top PNG resource is missing");
            Texture2D created=null;
            try
            {
                var bytes=resource.bytes;string actual;
                using(var hash=SHA256.Create())actual=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                if(actual!=ExpectedPngSha256)throw new InvalidDataException("Native Top PNG resource identity changed");
                // Exactly the successful uncompressed PNG control: sRGB,
                // one mip, authored RGB and binary/exterior alpha untouched.
                created=new Texture2D(2,2,TextureFormat.RGBA32,false,false){name="Top_Shirt_D (friends authored source)",hideFlags=HideFlags.DontSave};
                if(!ImageConversion.LoadImage(created,bytes,true)||created.width!=2048||created.height!=2048||created.mipmapCount!=1||
                    UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(created.graphicsFormat))
                    throw new InvalidDataException("Native Top PNG could not be decoded without compression/mips");
                created.filterMode=FilterMode.Bilinear;created.wrapModeU=TextureWrapMode.Repeat;created.wrapModeV=TextureWrapMode.Repeat;created.anisoLevel=1;created.mipMapBias=0;
                cache=new TextureCache{texture=created,references=1};return cache;
            }
            catch{if(created)DisposeOwned(created);throw;}
            // The TextAsset is a packaged asset managed by Resources. Only the
            // decoded runtime Texture2D is ours to destroy; do not unload or
            // mutate a shared asset during source acquisition.
        }
        static void ReleaseTexture(TextureCache lease)
        {
            if(lease==null||lease.references<=0)return;
            lease.references--;
            if(lease.references!=0)return;
            if(ReferenceEquals(cache,lease))cache=null;
            var prior=lease.texture;lease.texture=null;if(prior)DisposeOwned(prior);
        }
        void RemoveReplacedBindings()
        {
            while(bindings.Count>0)
            {
                Binding stale=null;
                foreach(var binding in bindings.Values)
                    if(!binding.outfit||system.GetOutfit("Top")!=binding.outfit||binding.outfit.material!=binding.owned){stale=binding;break;}
                if(stale==null)break;Release(stale);
            }
        }
        // Called by the active owned sweep, including when this MonoBehaviour
        // or its source Outfit never became active and receives no OnDestroy.
        internal bool MaintainOwnership()
        {
            if(!this||!system||!marker){ReleaseAll();return false;}
            RemoveReplacedBindings();return true;
        }
        void Release(Binding binding)
        {
            bindings.Remove(binding.outfit);
            if(binding.outfit&&binding.outfit.material==binding.owned)binding.outfit.material=binding.original;
            if(binding.owned)DisposeOwned(binding.owned);
            if(binding.textureLease!=null){var lease=binding.textureLease;binding.textureLease=null;ReleaseTexture(lease);}
            if(bindings.Count==0)FriendNativeTopDiffuseSweep.Untrack(this);
        }
        void ReleaseAll()
        {
            if(subscribed&&!ReferenceEquals(system,null))system.OnOutfitChanged-=Changed;subscribed=false;
            foreach(var binding in bindings.Values.ToArray())Release(binding);
            system=null;marker=null;FriendNativeTopDiffuseSweep.Untrack(this);
        }
        internal void ReleaseFromSweep(){ReleaseAll();}
        // Visibility/Behaviour disable does not cancel the vendor's awaited
        // merge/load. Keep its live source ownership and callback until actual
        // outfit replacement or rig destruction; re-enable is idempotent.
        void OnDisable(){ }
        void OnDestroy(){ReleaseAll();}
        static void DisposeOwned(UnityEngine.Object value)
        {if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
    // Only registered owned-rig references are inspected, four times a second;
    // there is no scene/renderer search. Active host lifecycle is independent
    // of the inactive native outfit hierarchy and of its destroy messages.
    public sealed class FriendNativeTopDiffuseSweep : MonoBehaviour
    {
        static FriendNativeTopDiffuseSweep instance;
        readonly List<FriendNativeTopDiffuseSupport> owners=new List<FriendNativeTopDiffuseSupport>();float next;
        public static int TrackedRigCount=>instance?instance.owners.Count:0;
        internal static void Track(FriendNativeTopDiffuseSupport owner)
        {
            if(!instance)
            {
                var root=new GameObject("Friends native diffuse ownership");DontDestroyOnLoad(root);
                instance=root.AddComponent<FriendNativeTopDiffuseSweep>();
            }
            if(!instance.owners.Any(p=>ReferenceEquals(p,owner)))instance.owners.Add(owner);
        }
        internal static void Untrack(FriendNativeTopDiffuseSupport owner)
        {if(instance)instance.owners.RemoveAll(p=>ReferenceEquals(p,owner));}
        void Awake(){SceneManager.sceneUnloaded+=SceneUnloaded;}
        void SceneUnloaded(Scene scene){Sweep();}
        void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;Sweep();}
        void Sweep()
        {
            for(int i=owners.Count-1;i>=0;i--)
            {
                var owner=owners[i];
                if(ReferenceEquals(owner,null)){owners.RemoveAt(i);continue;}
                try
                {
                    if(!owner.MaintainOwnership()&&i<owners.Count&&ReferenceEquals(owners[i],owner))owners.RemoveAt(i);
                }
                catch(Exception error)
                {
                    owner.ReleaseFromSweep();owners.RemoveAll(p=>ReferenceEquals(p,owner));
                    Debug.LogWarning("[FriendsLife] Native Top ownership cleanup: "+error.Message);
                }
            }
            if(owners.Count==0&&instance==this){instance=null;Destroy(gameObject);}
        }
        void ReleaseOwners()
        {
            var prior=owners.ToArray();owners.Clear();
            foreach(var owner in prior)if(!ReferenceEquals(owner,null))owner.ReleaseFromSweep();
        }
        // The host is not disabled for rig visibility. This also clears owned
        // sources before editor domain teardown or actual host destruction.
        void OnDisable(){ReleaseOwners();}
        void OnDestroy(){SceneManager.sceneUnloaded-=SceneUnloaded;ReleaseOwners();if(instance==this)instance=null;}
    }
}
