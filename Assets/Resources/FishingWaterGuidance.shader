Shader "BoatAttack/FishingWaterGuidance"
{
    Properties { _Tint("Tint", Color) = (.68,1,.93,.9) _Flow("Flow", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ USE_STRUCTURED_BUFFER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.verasl.water-system/Shaders/GerstnerWaves.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Flow;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                if(_WaveCount>0) { WaveStruct wave; SampleWaves(world,1,wave); world+=wave.position; }
                output.positionCS=TransformWorldToHClip(world); output.uv=input.uv;
                output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half edge=smoothstep(0,.20,input.uv.y)*smoothstep(0,.20,1-input.uv.y);
                half pulse=.62+.38*smoothstep(.25,.65,sin(input.uv.x*1.3-_Time.y*1.8)*.5+.5);
                return half4(MixFog(_Tint.rgb,input.fog),_Tint.a*edge*lerp(1,pulse,_Flow));
            }
            ENDHLSL
        }
    }
}
