Shader "Custom/TopDownWater2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (alpha = water mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.30, 0.75, 0.85, 1)
        _DeepColor ("Deep Color", Color) = (0.08, 0.30, 0.55, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _PatchScale ("Color Patch Scale", Float) = 0.6
        _PatchSpeed ("Color Patch Speed", Float) = 0.05

        [Header(Ripples)]
        _RippleColor ("Ripple Color", Color) = (0.75, 0.95, 1, 1)
        _RippleScale ("Ripple Scale", Float) = 1.2
        _RippleSpeed ("Ripple Speed", Float) = 0.12
        _RippleWidth ("Ripple Width", Range(0.005, 0.2)) = 0.04
        _RippleIntensity ("Ripple Intensity", Range(0, 1)) = 0.5

        [Header(Caustics)]
        _CausticsScale ("Caustics Scale", Float) = 2.5
        _CausticsSpeed ("Caustics Speed", Float) = 0.2
        _CausticsIntensity ("Caustics Intensity", Range(0, 1)) = 0.25

        [Header(Edge Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth ("Foam Width (texels)", Range(0, 8)) = 2
        _FoamNoiseScale ("Foam Noise Scale", Float) = 5
        _FoamSpeed ("Foam Speed", Float) = 0.4
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 0.8

        [Header(Sparkle)]
        _SparkleIntensity ("Sparkle Intensity", Range(0, 1)) = 0.5

        [Header(Pixel Art)]
        _PixelsPerUnit ("Snap Pattern To Pixels Per Unit (0 = off)", Float) = 16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "TopDownWater2D"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ShallowColor;
                half4 _DeepColor;
                float _Opacity;
                float _PatchScale;
                float _PatchSpeed;
                half4 _RippleColor;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleIntensity;
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsIntensity;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamNoiseScale;
                float _FoamSpeed;
                float _FoamIntensity;
                float _SparkleIntensity;
                float _PixelsPerUnit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  color      : COLOR;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * valueNoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // Sprite / tile alpha is the water mask
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float mask = sprite.a * IN.color.a;
                if (mask <= 0.001) discard;

                // 2D: patterns live in world XY. Optionally snap to the pixel grid.
                float2 wp = IN.positionWS.xy;
                if (_PixelsPerUnit > 0.5)
                    wp = floor(wp * _PixelsPerUnit) / _PixelsPerUnit;

                // --- Base color: slowly drifting shallow/deep patches ---
                float patch = fbm(wp * _PatchScale + float2(t, t * 0.6) * _PatchSpeed);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.3, 0.7, patch));

                // --- Stylized ripple lines ---
                float r = fbm(wp * _RippleScale + float2(-t * 0.7, t) * _RippleSpeed);
                float ripple = 1.0 - smoothstep(0.0, _RippleWidth, abs(r - 0.5));
                col = lerp(col, _RippleColor.rgb, ripple * _RippleIntensity);

                // --- Caustics ---
                float2 cUV = wp * _CausticsScale;
                float c1 = valueNoise(cUV + float2(t, t * 0.6) * _CausticsSpeed);
                float c2 = valueNoise(cUV * 1.3 - float2(t * 0.7, t) * _CausticsSpeed);
                float caustic = pow(saturate(1.0 - abs(c1 - c2) * 3.0), 4.0);
                col += caustic * _CausticsIntensity;

                // --- Edge foam from the sprite's own alpha ---
                // Foam appears where this pixel is water but nearby pixels are not.
                float2 off = _MainTex_TexelSize.xy * _FoamWidth;
                float a0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( off.x, 0)).a;
                float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-off.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,  off.y)).a;
                float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -off.y)).a;
                float edge = (_FoamWidth > 0.01) ? saturate(1.0 - min(min(a0, a1), min(a2, a3))) : 0.0;
                float foamNoise = fbm(wp * _FoamNoiseScale + float2(t * _FoamSpeed, -t * _FoamSpeed * 0.6));
                float foam = smoothstep(0.35, 0.55, foamNoise + edge * 0.5) * edge;
                col = lerp(col, _FoamColor.rgb, foam * _FoamIntensity * _FoamColor.a);

                // --- Sparkles ---
                float2 sUV = wp * 10.0;
                float sparkle = pow(saturate(valueNoise(sUV + t * 0.5) * valueNoise(sUV * 1.7 - t * 0.4) * 3.0 - 0.9), 3.0);
                col += sparkle * _SparkleIntensity;

                return half4(col, mask * _Opacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
