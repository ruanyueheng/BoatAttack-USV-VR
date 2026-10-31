Shader "BoatAttack/FishingWorldText"
{
    Properties { _MainTex("Font atlas",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct A { float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position=TransformObjectToHClip(input.vertex.xyz);output.uv=input.uv;output.color=input.color;return output;
            }
            half4 Frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(input.color.rgb,input.color.a*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a);
            }
            ENDHLSL
        }
    }
}
