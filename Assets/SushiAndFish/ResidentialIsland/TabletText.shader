Shader "Housing/TabletText"
{
 Properties
 {
  _MainTex("SDF Atlas",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _CullMode("Cull",Float)=0 _ZTestMode("Depth test",Float)=8
  _StencilComp("Stencil comparison",Float)=8 _Stencil("Stencil",Float)=0 _StencilOp("Stencil operation",Float)=0
  _StencilReadMask("Read mask",Float)=255 _StencilWriteMask("Write mask",Float)=255 _ColorMask("Color mask",Float)=15
  _TextureWidth("Atlas width",Float)=2048 _TextureHeight("Atlas height",Float)=2048 _GradientScale("Gradient scale",Float)=6
  _ScaleX("Scale X",Float)=1 _ScaleY("Scale Y",Float)=1 _ScaleRatioA("Ratio A",Float)=1 _ScaleRatioB("Ratio B",Float)=1 _ScaleRatioC("Ratio C",Float)=1
  _FaceDilate("Face dilate",Float)=0 _OutlineWidth("Outline width",Float)=0 _OutlineSoftness("Outline softness",Float)=0
  _WeightNormal("Normal weight",Float)=0 _WeightBold("Bold weight",Float)=.5 _PerspectiveFilter("Perspective",Float)=.875
  _ShaderFlags("Flags",Float)=0 _Sharpness("Sharpness",Float)=0 _MaskSoftnessX("Mask X",Float)=0 _MaskSoftnessY("Mask Y",Float)=0
 }
 SubShader
 {
  Tags{"Queue"="Transparent+101" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull [_CullMode] ZWrite Off ZTest [_ZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
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
   fixed4 Frag(V v):SV_Target{float distance=tex2D(_MainTex,v.uv).a;float width=max(fwidth(distance)*.75,.012);float alpha=smoothstep(.5-width,.5+width,distance);return fixed4(v.color.rgb,v.color.a*alpha);}
   ENDCG
  }
 }
}
