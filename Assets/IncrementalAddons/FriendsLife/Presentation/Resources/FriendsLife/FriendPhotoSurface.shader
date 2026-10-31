Shader "SushiAndFish/FriendPhotoSurface"
{
 Properties
 {
  [MainTexture] _MainTex("Photo",2D)="white"{}
  [HideInInspector] _IDMap("Native ID-map compatibility",2D)=""{}
  _RegionMask("Semantic regions",2D)="black"{}
  _UseRegions("Use semantic regions",Float)=0
  _RegionHair("Hair tint",Color)=(1,1,1,1)
  _RegionTop("Top tint",Color)=(1,1,1,1)
  _RegionBottom("Bottom tint",Color)=(1,1,1,1)
  _RegionSkin("Body skin tint",Color)=(1,1,1,1)
  _RegionShoes("Shoes tint",Color)=(1,1,1,1)
  _RegionFace("Face tint",Color)=(1,1,1,1)
  _Color_1("Base tint",Color)=(1,1,1,1)
  _Color_2("Tint 2",Color)=(1,1,1,1)
  _Color_3("Tint 3",Color)=(1,1,1,1)
  _Color_4("Tint 4",Color)=(1,1,1,1)
  _Color_5("Tint 5",Color)=(1,1,1,1)
  _Color_6("Tint 6",Color)=(1,1,1,1)
  _Color_7("Tint 7",Color)=(1,1,1,1)
  _Color_8("Tint 8",Color)=(1,1,1,1)
  _Color_9("Tint 9",Color)=(1,1,1,1)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Name "PhotoSurface"
   Tags { "LightMode"="UniversalForward" }
   Cull Back ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   TEXTURE2D(_RegionMask);SAMPLER(sampler_RegionMask);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;half4 _Color_1,_Color_2,_Color_3,_Color_4,_Color_5,_Color_6,_Color_7,_Color_8,_Color_9;
    float _UseRegions;half4 _RegionHair,_RegionTop,_RegionBottom,_RegionSkin,_RegionShoes,_RegionFace;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings vert(Attributes input)
   {Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=TRANSFORM_TEX(input.uv,_MainTex);return output;}
   half4 frag(Varyings input):SV_Target
   {
    half3 n=normalize(input.normalWS);Light light=GetMainLight();half diffuse=.35h+.65h*saturate(dot(n,light.direction));half3 illumination=max(SampleSH(n)+light.color*diffuse,half3(.35h,.35h,.35h));
    half3 tint=_Color_1.rgb;
    if(_UseRegions>.5)
    {
     float region=round(SAMPLE_TEXTURE2D(_RegionMask,sampler_RegionMask,input.uv).r*255.0);
     tint=half3(1,1,1);
     if(region==1)tint=_RegionHair.rgb;else if(region==2)tint=_RegionTop.rgb;else if(region==3)tint=_RegionBottom.rgb;
     else if(region==4)tint=_RegionSkin.rgb;else if(region==5)tint=_RegionShoes.rgb;else if(region==6)tint=_RegionFace.rgb;
    }
    half3 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).rgb*tint;return half4(color*illumination,1);
   }
   ENDHLSL
  }
 }
}
