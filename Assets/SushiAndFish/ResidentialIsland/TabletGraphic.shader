Shader "Housing/TabletGraphic"
{
 Properties{[PerRendererData]_MainTex("Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)}
 SubShader
 {
  Tags{"Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "UnityCG.cginc"
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   sampler2D _MainTex;float4 _Color;
   V Vert(A a){V v;v.position=UnityObjectToClipPos(a.position);v.uv=a.uv;v.color=a.color*_Color;return v;}
   fixed4 Frag(V v):SV_Target{return tex2D(_MainTex,v.uv)*v.color;}
   ENDCG
  }
 }
}
