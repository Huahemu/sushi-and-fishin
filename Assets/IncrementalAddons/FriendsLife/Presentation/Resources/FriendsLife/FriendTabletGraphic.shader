Shader "FriendsLife/TabletGraphic"
{
 Properties
 {
  [PerRendererData] _MainTex("Texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _StencilComp("Stencil comparison",Float)=8 _Stencil("Stencil",Float)=0 _StencilOp("Stencil operation",Float)=0
  _StencilReadMask("Read mask",Float)=255 _StencilWriteMask("Write mask",Float)=255 _ColorMask("Color mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use alpha clip",Float)=0
 }
 SubShader
 {
  Tags{"Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
  Stencil{Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float4 localPosition:TEXCOORD1;};
   sampler2D _MainTex;float4 _Color;float4 _ClipRect;
   V Vert(A a){V v;v.localPosition=a.position;v.position=UnityObjectToClipPos(a.position);v.uv=a.uv;v.color=a.color*_Color;return v;}
   fixed4 Frag(V v):SV_Target
   {
    fixed4 color=tex2D(_MainTex,v.uv)*v.color;
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(v.localPosition.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a-.001);
    #endif
    return color;
   }
   ENDCG
  }
 }
}
