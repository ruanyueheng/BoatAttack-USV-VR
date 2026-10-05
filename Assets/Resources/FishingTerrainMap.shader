Shader "Fishing/TerrainMap"
{
    Properties
    {
        _MainTex ("Baked terrain", 2D) = "white" {}
        _ViewUV ("World UV window (not the circular mask)", Vector) = (0,0,1,1)
        _HasMap ("Valid capture", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ViewUV;
            float _HasMap;
            struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 localUV : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.localUV = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // Local UV supplies the circle. World UV supplies ONLY the texture sample.
                float d = length(input.localUV * 2 - 1);
                clip(1 - d);
                float edge = saturate((1 - d) / max(fwidth(d), .0001));
                float2 worldUV = _ViewUV.xy + input.localUV * _ViewUV.zw;
                float known = _HasMap * step(0, worldUV.x) * step(worldUV.x, 1)
                    * step(0, worldUV.y) * step(worldUV.y, 1);
                // Sampling may clamp at the hardware edge; the outside is ALWAYS replaced by unknown.
                half3 sampleColor = tex2D(_MainTex, worldUV).rgb;
                float hatch = step(.72, frac((input.position.x + input.position.y) / 12));
                half3 unknown = lerp(half3(.055,.065,.075), half3(.13,.15,.17), hatch);
                return half4(lerp(unknown, sampleColor, known) * input.color.rgb, edge * input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
