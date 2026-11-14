// Flat 2-tone cel shading with a constant-width ink outline (inverted hull), like a clean 2D sports cartoon.
// _Color = lit tone, _Shade = shadow tone (defaults to a darker _Color when left black), _Outline = ink width in screen pixels.
Shader "ChiStrikers/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Shade ("Shadow Color", Color) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0.106,0.106,0.133,1)
        _Outline ("Outline Width (px)", Float) = 2.2
        _Threshold ("Light Threshold", Range(-1,1)) = 0.05
        _Emission ("Emission", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _Shade;
            float _Threshold, _Emission;

            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(normalize(i.n), L);
                fixed4 shade = _Shade.a > 0.01 ? _Shade : fixed4(_Color.rgb * 0.72, 1);
                fixed4 c = ndl > _Threshold ? _Color : shade;
                c.rgb = lerp(c.rgb, _Color.rgb, _Emission);
                c.a = 1;
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _Outline;

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata_base v)
            {
                v2f o;
                float4 clip = UnityObjectToClipPos(v.vertex);
                float3 vn = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 off = normalize(TransformViewToProjection(vn.xy) + float2(1e-5, 1e-5));
                // constant width in pixels, whatever the distance
                clip.xy += off * _Outline * 2.0 * clip.w / _ScreenParams.xy;
                o.pos = clip;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
    Fallback Off
}
