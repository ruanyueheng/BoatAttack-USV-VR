Shader "BoatAttack/FishingGlass"
{
    Properties { _BaseColor("Glass tint",Color)=(.45,.8,.9,.08) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;
            }
            half4 Frag(V input):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);return _BaseColor; }
            ENDHLSL
        }
    }
}
