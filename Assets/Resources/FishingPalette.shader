Shader "BoatAttack/FishingPalette"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BaseMap("Fish albedo", 2D) = "white" {}
        _Textured("Use fish albedo", Range(0,1)) = 0
        _GroundDetail("Ground surface detail", Range(0,1)) = 0
        _DoubleSidedLighting("Two-sided foliage lighting", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 2
        _FinMotion("Fin motion", Vector) = (0,0,0,0)
        _TailMotion("Fish tail motion", Vector) = (0,0,0,0)
        _Unlit("Unlit", Range(0,1)) = 0
        _Gloss("Gloss", Range(0,1)) = 0
        _Rim("Rim light", Range(0,1)) = 0
        _Shadows("Coastal shadows", Range(0,1)) = 0
        [PerRendererData] _TitleBreeze("Title foliage breeze", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Textured;
            half _GroundDetail;
            half _DoubleSidedLighting;
            float4 _FinMotion;
            float4 _TailMotion;
            half _Unlit;
            half _Gloss;
            half _Rim;
            half _Shadows;
            float _TitleBreeze;
            CBUFFER_END
            float _FishingVisualTimeOffset;
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; float3 positionWS:TEXCOORD2; float2 uv:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position=input.positionOS.xyz;
                position.y+=sin((_Time.y+_FishingVisualTimeOffset)*_FinMotion.y+abs(position.x)*2+_FinMotion.w)*max(0,abs(position.x)-_FinMotion.z)*_FinMotion.x;
                position.x+=sin((_Time.y+_FishingVisualTimeOffset)*_TailMotion.y+position.z*18+_TailMotion.w)*max(0,-position.z-_TailMotion.z)*_TailMotion.x;
                output.positionWS=TransformObjectToWorld(position);
                float leaf=_Textured>.5h?input.color.a:saturate((input.color.g-input.color.r)*8)*saturate((input.color.g-input.color.b)*8);
                output.positionWS.xz+=leaf*_TitleBreeze*sin((_Time.y+_FishingVisualTimeOffset)*1.2+output.positionWS.x*.19+output.positionWS.z*.13)*float2(1,.35);
                output.positionCS=TransformWorldToHClip(output.positionWS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=lerp(input.color,half4(1,1,1,1),_Textured);output.uv=input.uv;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb=SRGBToLinear(output.color.rgb);
                #endif
                output.color*=_BaseColor;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo=input.color.rgb;
                if(_Textured>.5h)albedo*=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                half3 normal=normalize(input.normalWS);
                if(_DoubleSidedLighting>.5h)normal*=IS_FRONT_VFACE(frontFace,1.0h,-1.0h);
                if(_GroundDetail>.001h)
                {
                    half grass=saturate((input.color.g-input.color.r)*5);
                    float2 uv=frac(input.positionWS.xz*1.4)*.22;
                    float2 sandUV=uv+float2(.015,.765),grassUV=uv+float2(.765,.515);
                    half3 detail=lerp(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,sandUV).rgb,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,grassUV).rgb,grass);
                    albedo*=lerp(.65h+detail*.65h,.72h+detail*.9h,grass);
                    half dx=lerp(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,sandUV+float2(.002,0)).r,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,grassUV+float2(.002,0)).r,grass)-detail.r;
                    half dz=lerp(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,sandUV+float2(0,.002)).r,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,grassUV+float2(0,.002)).r,grass)-detail.r;
                    normal=normalize(normal+half3(-dx,0,-dz)*2.5h);
                }
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,light.direction))*.7h*lerp(1.0h,light.shadowAttenuation,_Shadows)+.22h;
                half3 ambient=SampleSH(normal);
                half3 color=albedo*(light.color*diffuse+ambient*.35h);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count=GetAdditionalLightsCount();
                for(uint i=0u;i<count;i++)
                {
                    Light local=GetAdditionalLight(i,input.positionWS,half4(1,1,1,1));
                    color+=albedo*local.color*saturate(dot(normal,local.direction))*local.distanceAttenuation*local.shadowAttenuation;
                }
                #endif
                // Optional stylised gloss and rim; both default to zero so existing palettes are unchanged.
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfway=normalize(light.direction+view);
                color+=light.color*pow(saturate(dot(normal,halfway)),48.0h)*_Gloss;
                color+=(ambient*.6h+light.color*.12h)*pow(1.0h-saturate(dot(normal,view)),3.0h)*_Rim;
                color=lerp(color,albedo,_Unlit);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Textured;
            half _GroundDetail;
            half _DoubleSidedLighting;
            float4 _FinMotion;
            float4 _TailMotion;
            half _Unlit;
            half _Gloss;
            half _Rim;
            half _Shadows;
            float _TitleBreeze;
            CBUFFER_END
            float _FishingVisualTimeOffset;
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowInput { float3 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(ShadowInput input):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 p=TransformObjectToWorld(input.positionOS), n=TransformObjectToWorldNormal(input.normalOS);
                float leaf=_Textured>.5h?input.color.a:saturate((input.color.g-input.color.r)*8)*saturate((input.color.g-input.color.b)*8);
                p.xz+=leaf*_TitleBreeze*sin((_Time.y+_FishingVisualTimeOffset)*1.2+p.x*.19+p.z*.13)*float2(1,.35);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-p);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clipPos=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                clipPos.z=min(clipPos.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clipPos.z=max(clipPos.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clipPos;
            }
            half4 ShadowFrag():SV_Target { clip(_Shadows-.5h);return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Textured;
            half _GroundDetail;
            half _DoubleSidedLighting;
            float4 _FinMotion;
            float4 _TailMotion;
            half _Unlit;
            half _Gloss;
            half _Rim;
            half _Shadows;
            float _TitleBreeze;
            CBUFFER_END
            float _FishingVisualTimeOffset;
            struct DepthInput { float3 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthOutput { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            DepthOutput DepthVert(DepthInput input)
            {
                DepthOutput output=(DepthOutput)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS;
                p.y+=sin((_Time.y+_FishingVisualTimeOffset)*_FinMotion.y+abs(p.x)*2+_FinMotion.w)*max(0,abs(p.x)-_FinMotion.z)*_FinMotion.x;
                p.x+=sin((_Time.y+_FishingVisualTimeOffset)*_TailMotion.y+p.z*18+_TailMotion.w)*max(0,-p.z-_TailMotion.z)*_TailMotion.x;
                p=TransformObjectToWorld(p);
                float leaf=_Textured>.5h?input.color.a:saturate((input.color.g-input.color.r)*8)*saturate((input.color.g-input.color.b)*8);
                p.xz+=leaf*_TitleBreeze*sin((_Time.y+_FishingVisualTimeOffset)*1.2+p.x*.19+p.z*.13)*float2(1,.35);
                output.positionCS=TransformWorldToHClip(p);return output;
            }
            half4 DepthFrag(DepthOutput input):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);return 0; }
            ENDHLSL
        }
    }
}
