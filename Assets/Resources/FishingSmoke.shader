Shader "BoatAttack/FishingSmoke"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=TransformObjectToHClip(a.p.xyz);v.c=a.c;v.uv=a.uv;v.fog=ComputeFogFactor(v.p.z);return v;}
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float2 p=v.uv*2-1;
                float edge=dot(p,p)+.08*sin(p.x*13+p.y*7)*sin(p.y*11);
                half alpha=smoothstep(1,0,edge)*.65;
                return half4(MixFog(v.c.rgb,v.fog),v.c.a*alpha);
            }
            ENDHLSL
        }
    }
}
