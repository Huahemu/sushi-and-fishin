Shader "Housing/FloorPlan"
{
    Properties{_BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _CutHeight("Cut height",Float)=9999 _TextureAlpha("Texture alpha",Float)=0 _AlphaClip("Alpha clip",Float)=0 _Cutoff("Alpha cutoff",Float)=.5}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite On Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor;float4 _BaseMap_ST;float _CutHeight;float _TextureAlpha;float _AlphaClip;float _Cutoff; CBUFFER_END
            V Vert(A a){V v;v.world=TransformObjectToWorld(a.positionOS.xyz);v.positionCS=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normalOS);v.uv=TRANSFORM_TEX(a.uv,_BaseMap);return v;}
            half4 Frag(V v):SV_Target
            {
                clip(_CutHeight-v.world.y);half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);
                if(_AlphaClip>.5)clip(tex.a-_Cutoff);
                half4 c=half4(tex.rgb*_BaseColor.rgb,_BaseColor.a*lerp(1,tex.a,_TextureAlpha));
                c.rgb*=.72+.28*saturate(dot(normalize(v.normal),normalize(float3(-.35,1,.2))));return c;
            }
            ENDHLSL
        }
    }
}
