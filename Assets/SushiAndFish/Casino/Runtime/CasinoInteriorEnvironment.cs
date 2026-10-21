using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SushiAndFish.Casino
{
    // The imported rooms are relocated outside the island. Render them with
    // the source renderer/sky instead of the island's ocean renderer and
    // reflection environment. Host weather is restored after this camera.
    public sealed class CasinoInteriorEnvironment : MonoBehaviour
    {
        public Material sourceSky;
        public ParkCasinoIntegration casino;
        Camera target;
        int originalRenderer, interiorRenderer=-1;
        bool inside, scoped;
        Cubemap sourceReflection;
        Material sky; Texture reflection;
        DefaultReflectionMode reflectionMode;
        AmbientMode ambientMode;
        Color fogColor, ambientSky, ambientEquator, ambientGround;
        bool fog;
        FogMode fogMode;
        float fogStart, fogEnd, fogDensity, intensity;
        void Awake()
        {
            target=GetComponent<Camera>();
            var data=target.GetUniversalAdditionalCameraData();
            originalRenderer=(int)typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(data);
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline)
            {
                var list=(ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pipeline);
                interiorRenderer=Array.FindIndex(list,r=>r&&r.name=="CasinoInteriorRenderer");
            }
            RenderPipelineManager.beginCameraRendering+=Begin;
            RenderPipelineManager.endCameraRendering+=End;
        }
        void LateUpdate()
        {
            bool next=casino&&casino.Ready&&casino.CurrentFloor>0;
            if(next==inside)return;
            inside=next;
            if(interiorRenderer>=0)target.GetUniversalAdditionalCameraData().SetRenderer(inside?interiorRenderer:originalRenderer);
            if(inside&&!sourceReflection&&sourceSky)sourceReflection=ReflectionFromSourceSky(sourceSky);
        }
        static Cubemap ReflectionFromSourceSky(Material material)
        {
            var cube=new Cubemap(64,TextureFormat.RGBAHalf,true){name="OriginalCasinoMidnightReflection",hideFlags=HideFlags.DontSave};
            var faces=new[]{"_RightTex","_LeftTex","_UpTex","_DownTex","_FrontTex","_BackTex"};
            var rt=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            var old=RenderTexture.active;
            var read=new Texture2D(64,64,TextureFormat.RGBAHalf,false,true);
            for(int i=0;i<6;i++)
            {
                var texture=material.GetTexture(faces[i]);
                Graphics.Blit(texture?texture:Texture2D.blackTexture,rt);
                RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,64,64),0,0);read.Apply();
                cube.SetPixels(read.GetPixels(),(CubemapFace)i);
            }
            cube.Apply(true,false);RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(read);return cube;
        }
        void Begin(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=target||!inside||!sourceSky||scoped)return;
            scoped=true;
            sky=RenderSettings.skybox;fog=RenderSettings.fog;fogColor=RenderSettings.fogColor;fogMode=RenderSettings.fogMode;fogStart=RenderSettings.fogStartDistance;fogEnd=RenderSettings.fogEndDistance;fogDensity=RenderSettings.fogDensity;
            ambientMode=RenderSettings.ambientMode;ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;
            reflectionMode=RenderSettings.defaultReflectionMode;reflection=RenderSettings.customReflectionTexture;intensity=RenderSettings.reflectionIntensity;
            RenderSettings.skybox=sourceSky;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.039738342f,.1065236f,.1792453f);RenderSettings.fogStartDistance=50;RenderSettings.fogEndDistance=300;RenderSettings.fogDensity=.01f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.10034709f,.3177999f,.5188679f);RenderSettings.ambientEquatorColor=new Color(.079298675f,.13360624f,.16981131f);RenderSettings.ambientGroundColor=new Color(.030170888f,.049108002f,.05660379f);
            if(sourceReflection){RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=sourceReflection;RenderSettings.reflectionIntensity=1;}
        }
        void End(ScriptableRenderContext context,Camera camera){if(camera==target)Restore();}
        void Restore()
        {
            if(!scoped)return;scoped=false;
            RenderSettings.skybox=sky;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogMode=fogMode;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;RenderSettings.fogDensity=fogDensity;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
            RenderSettings.defaultReflectionMode=reflectionMode;RenderSettings.customReflectionTexture=reflection;RenderSettings.reflectionIntensity=intensity;
        }
        void OnDestroy()
        {
            Restore();RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.endCameraRendering-=End;
            if(target)target.GetUniversalAdditionalCameraData().SetRenderer(originalRenderer);
            if(sourceReflection)Destroy(sourceReflection);
        }
    }
}
