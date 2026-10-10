Shader "BoatAttack/FishingPalette"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _FinMotion("Fin motion", Vector) = (0,0,0,0)
        _Unlit("Unlit", Range(0,1)) = 0
        _Gloss("Gloss", Range(0,1)) = 0
        _Rim("Rim light", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _FinMotion;
            half _Unlit;
            half _Gloss;
            half _Rim;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; float3 positionWS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position=input.positionOS.xyz;
                position.y+=sin(_Time.y*_FinMotion.y+abs(position.x)*2+_FinMotion.w)*max(0,abs(position.x)-_FinMotion.z)*_FinMotion.x;
                output.positionWS=TransformObjectToWorld(position);
                output.positionCS=TransformWorldToHClip(output.positionWS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb=SRGBToLinear(output.color.rgb);
                #endif
                output.color*=_BaseColor;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal=normalize(input.normalWS);Light light=GetMainLight();
                half diffuse=saturate(dot(normal,light.direction))*.7h+.22h;
                half3 ambient=SampleSH(normal);
                half3 color=input.color.rgb*(light.color*diffuse+ambient*.35h);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count=GetAdditionalLightsCount();
                for(uint i=0u;i<count;i++)
                {
                    Light local=GetAdditionalLight(i,input.positionWS);
                    color+=input.color.rgb*local.color*saturate(dot(normal,local.direction))*local.distanceAttenuation;
                }
                #endif
                // Optional stylised gloss and rim; both default to zero so existing palettes are unchanged.
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfway=normalize(light.direction+view);
                color+=light.color*pow(saturate(dot(normal,halfway)),48.0h)*_Gloss;
                color+=(ambient*.6h+light.color*.12h)*pow(1.0h-saturate(dot(normal,view)),3.0h)*_Rim;
                color=lerp(color,input.color.rgb,_Unlit);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
