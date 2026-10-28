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
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 viewWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.viewWS=GetWorldSpaceViewDir(world);return output;
            }
            half4 Frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Clear face-on, a soft sky sheen only at grazing angles so panes read as glass, not a tinted block.
                half rim=pow(1-abs(dot(normalize(input.normalWS),normalize(input.viewWS))),4);
                return half4(lerp(_BaseColor.rgb,half3(.92,.97,1),rim),saturate(_BaseColor.a*.5+rim*.35));
            }
            ENDHLSL
        }
    }
}
