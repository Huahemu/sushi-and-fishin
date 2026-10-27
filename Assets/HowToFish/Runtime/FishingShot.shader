Shader "HowToFish/Shot unlit" {
    Properties {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _SoftDisc ("Soft particle disc", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _BaseColor;
            float _SoftDisc;
            Output vert(Input input) {
                Output output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.color=input.color*_BaseColor;output.uv=input.uv;
                return output;
            }
            half4 frag(Output input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color=input.color;
                color.a*=lerp(1,saturate((1-length(input.uv*2-1))*2),_SoftDisc);
                return color;
            }
            ENDHLSL
        }
    }
}
