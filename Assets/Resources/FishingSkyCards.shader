Shader "BoatAttack/FishingSkyCards"
{
    // Distant watercolor cumulus cards (one mesh, one draw). Density atlas R; colours from FishingWeather.
    // Transparent queue: URP draws the skybox after opaques, which would paint over depth-less cards.
    Properties { [NoScaleOffset]_MainTex("Cloud density atlas",2D)="black"{} _Lit("Lit",Color)=(1,.95,.85,1) _Shade("Shade",Color)=(.55,.62,.76,1) _Rim("Sunset rim",Color)=(.94,.73,.45,1) _Glow("Twilight",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-400" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Lit,_Shade,_Rim;float _Glow;
            CBUFFER_END
            // uv: atlas uv; uv2.x: sunward direction across the card (-1..1); color.a: fade.
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float2 sun:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 sun:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.sun=i.sun;o.color=i.color;return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).r;
                float toward=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(i.sun.x*.012,.010)).r;
                // Lit where density thins toward the sun and on the crown; flat bases darker.
                float lit=saturate(.55+(d-toward)*5+(i.sun.y-.5)*.5);
                float3 c=lerp(_Shade.rgb,_Lit.rgb,lit);
                float rim=saturate((d-toward)*12)*saturate(1.3-d)*_Glow;
                c=lerp(c,_Rim.rgb*1.4,rim);
                return half4(c,smoothstep(.04,.45,d)*i.color.a);
            }
            ENDHLSL
        }
    }
}
