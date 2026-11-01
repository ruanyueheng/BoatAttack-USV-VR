Shader "BoatAttack/FishingBinocularLens"
{
    Properties { _MainTex("Distant optical view",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;return v;}
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float2 p=(v.uv-.5)*float2(1.777778,1);
                float rim=min(length(p-float2(-.28,0)),length(p-float2(.28,0)));
                float alpha=1-smoothstep(.455,.47,rim);
                float glass=1-smoothstep(.43,.448,rim);
                half3 view=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv).rgb;
                half3 ink=half3(.012,.021,.021);
                return half4(lerp(ink,view,glass),alpha);
            }
            ENDHLSL
        }
    }
}
