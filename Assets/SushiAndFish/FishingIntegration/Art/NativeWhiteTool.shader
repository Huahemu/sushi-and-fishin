Shader "SushiAndFish/Native white tool glyph" {
 Properties {
  [PerRendererData] _MainTex ("Tool silhouette",2D)="white" {}
  _Color ("Tint",Color)=(1,1,1,1)
  _EdgePixels ("Native gray contour width",Float)=5
  _OutlineLuma ("Native gray contour",Range(0,1))=.30
  _StencilComp ("Stencil Comparison",Float)=8
  _Stencil ("Stencil ID",Float)=0
  _StencilOp ("Stencil Operation",Float)=0
  _StencilWriteMask ("Stencil Write Mask",Float)=255
  _StencilReadMask ("Stencil Read Mask",Float)=255
  _ColorMask ("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip",Float)=0
 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Output {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   sampler2D _MainTex;float4 _MainTex_TexelSize,_Color,_ClipRect;float _EdgePixels,_OutlineLuma;
   Output vert(Input input){Output output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.local=input.vertex;output.vertex=UnityObjectToClipPos(input.vertex);output.color=input.color*_Color;output.uv=input.uv;return output;}
   half4 frag(Output input):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 edge=_MainTex_TexelSize.xy*_EdgePixels;half alpha=tex2D(_MainTex,input.uv).a;half inside=alpha;
    inside=min(inside,tex2D(_MainTex,input.uv+float2(edge.x,0)).a);inside=min(inside,tex2D(_MainTex,input.uv-float2(edge.x,0)).a);
    inside=min(inside,tex2D(_MainTex,input.uv+float2(0,edge.y)).a);inside=min(inside,tex2D(_MainTex,input.uv-float2(0,edge.y)).a);
    inside=min(inside,tex2D(_MainTex,input.uv+edge*.7071).a);inside=min(inside,tex2D(_MainTex,input.uv-edge*.7071).a);
    inside=min(inside,tex2D(_MainTex,input.uv+float2(edge.x,-edge.y)*.7071).a);inside=min(inside,tex2D(_MainTex,input.uv+float2(-edge.x,edge.y)*.7071).a);
    half luma=lerp(_OutlineLuma,1,smoothstep(.05,.95,inside));half4 color=half4(luma,luma,luma,alpha)*input.color;
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(input.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a-.001);
    #endif
    return color;
   }
   ENDHLSL
  }
 }
}
