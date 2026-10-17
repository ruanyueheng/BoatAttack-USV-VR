Shader "BoatAttack/FishingLighthouseBeam"
{
    Properties { _Tint("Warm beam",Color)=(4,3.6,2.8,.055) _Visibility("Visibility",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;float _Visibility;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.normal=TransformObjectToWorldNormal(i.normal);o.world=TransformObjectToWorld(i.p.xyz);return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float fade=smoothstep(0,.06,i.uv.y)*pow(saturate(1-i.uv.y),1.8);
                float edge=pow(saturate(abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world))))),1.5);
                return half4(_Tint.rgb,_Tint.a*_Visibility*fade*edge);
            }
            ENDHLSL
        }
    }
}
