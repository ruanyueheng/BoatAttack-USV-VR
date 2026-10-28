Shader "Hidden/Fishing/InkWipe"
{
    // V044 ink-spread transition: one mask sample + threshold; _Progress 0 = clear, 1 = covered.
    Properties { _MainTex ("Starry sea", 2D) = "white" {} _Mask ("Ink mask", 2D) = "gray" {} _Progress ("Progress", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Mask; float _Progress; fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_full v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float m = tex2D(_Mask, i.uv).r;
                float t = _Progress * 1.15;
                float a = smoothstep(m - .02, m + .06, t);          // feathered wet edge
                float rim = smoothstep(m - .02, m + .02, t) - smoothstep(m + .02, m + .10, t); // darker pigment pooling at the edge
                fixed4 c = tex2D(_MainTex, i.uv * float2(1.6, .9));
                c.rgb *= 1 - rim * .35;
                c.a = a * i.color.a;
                return c;
            }
            ENDCG
        }
    }
}
