Shader "Unlit/WaterFXFoamOnly"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		Blend One One
		LOD 100

		Pass
		{
			Name "WaterFX"
			Tags{"LightMode" = "WaterFX"}

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag

			#define _NORMALMAP 1
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				half4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				half4 color : TEXCOORD1;
				float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			
			v2f vert (appdata v)
			{
				v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				half3 posWS = TransformObjectToWorld(v.vertex.xyz);
				o.vertex = TransformWorldToHClip(posWS);
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				half4 col = tex2D(_MainTex, i.uv) * i.color;
				return half4(col.rgb, 0);
			}
			ENDHLSL
		}
	}
}
