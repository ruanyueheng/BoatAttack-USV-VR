Shader "BoatAttack/FishingSpray"
{
    Properties { _Round("Round droplets",Float)=1 }
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
            CBUFFER_START(UnityPerMaterial)
            float _Round;
            CBUFFER_END
            struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=TransformObjectToHClip(a.p.xyz);v.c=a.c;v.uv=a.uv;v.fog=ComputeFogFactor(v.p.z);return v;}
            half4 Frag(V v):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);float2 q=v.uv*2-1;half alpha=lerp(1,saturate((1-length(q))*3),saturate(_Round));
                // _Round 2: a glowing four-point sparkle for pickups, harvests and catches.
                // _Round 3: bubble ring with a highlight; _Round 4: pointed leaf or petal.
                if(_Round>3.5){half w=.5*(1-q.y*q.y);alpha=saturate((1-abs(q.x)/max(w,.001))*3)*step(abs(q.y),1)*(.75+.25*q.y);return half4(MixFog(v.c.rgb,v.fog),v.c.a*alpha);}
                if(_Round>2.5){float l=length(q);alpha=saturate(exp(-pow((l-.72)*9,2))+exp(-dot(q-float2(-.3,.3),q-float2(-.3,.3))*30)*.8+saturate(1-l)*.08);return half4(MixFog(v.c.rgb,v.fog),v.c.a*alpha);}
                if(_Round>1.5){float2 d=abs(q);alpha=saturate(exp(-dot(q,q)*14)+saturate(1-d.x*9)*saturate(1-d.y)*.9+saturate(1-d.y*9)*saturate(1-d.x)*.9+exp(-dot(q,q)*3)*.25);return half4(MixFog(v.c.rgb*(1+alpha),v.fog),v.c.a*alpha);}
                return half4(MixFog(v.c.rgb,v.fog),v.c.a*alpha);}
            ENDHLSL
        }
    }
}
