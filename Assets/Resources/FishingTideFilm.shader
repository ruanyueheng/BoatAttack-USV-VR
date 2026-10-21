Shader "Fishing/Tidal picture show"
{
    Properties { _Playing("Playing",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5" }
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            CBUFFER_START(UnityPerMaterial)
            float _Playing;
            CBUFFER_END
            V vert(A a){V v;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.uv=a.uv;return v;}
            half4 frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float2 p=v.uv;float t=_Time.y*.14;
                float3 sky=lerp(float3(.17,.32,.42),float3(.74,.65,.46),p.y);
                float moon=1-smoothstep(.08,.09,length((p-float2(.74,.77))*float2(1.1,1)));
                sky=lerp(sky,float3(1,.89,.63),moon);
                float water=1-smoothstep(.31,.33+sin(p.x*14+t)*.014,p.y);
                float3 sea=lerp(float3(.09,.26,.31),float3(.31,.61,.61),.5+.18*sin(p.x*35+p.y*70-t*4));
                float3 color=lerp(sky,sea,water);
                float x=frac(t*.10+.28);float sail=step(abs(p.x-x),(.67-p.y)*.40)*step(.37,p.y)*step(p.y,.65);
                color=lerp(color,float3(.95,.85,.62),sail);
                float edge=smoothstep(0,.07,p.x)*smoothstep(0,.07,1-p.x)*smoothstep(0,.07,p.y)*smoothstep(0,.07,1-p.y);
                return half4(lerp(float3(.025,.04,.045),color*edge,_Playing),1);
            }
            ENDHLSL
        }
    }
}
