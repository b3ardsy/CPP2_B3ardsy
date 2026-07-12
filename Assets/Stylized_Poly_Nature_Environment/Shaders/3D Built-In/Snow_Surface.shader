Shader "Custom/Snow_Surface"
{
    Properties
    {
        _ColorTexture ("Color Texture", 2D) = "white" {}
        _EmissiveTexture ("Emissive Texture", 2D) = "black" {}
        _EmissiveIntensity ("Emissive Intensity", Range(0,10)) = 1

        [Header(SNOW)]
        [Space(10)]
        _SnowColor ("Snow Color", Color) = (1,1,1,1)
        _SnowAmount ("Snow Amount", Range(0,1)) = 0.5
        _SnowSharpness ("Snow Sharpness", Range(0.1, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        sampler2D _ColorTexture;
        float4 _SnowColor;
        float _SnowAmount;
        float _SnowSharpness;
        sampler2D _EmissiveTexture;
        float _EmissiveIntensity;

        struct Input
        {
            float2 uv_ColorTexture;
            float2 uv_EmissiveTexture;
            float3 worldNormal;
            float  facing : VFACE;
        };

        void vert(inout appdata_full v) {}

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 baseCol = tex2D(_ColorTexture, IN.uv_ColorTexture).rgb;

            float3 n = normalize(IN.worldNormal) * (IN.facing > 0 ? 1 : -1);
            float up = saturate(dot(n, float3(0,1,0)));
            float snowMask = pow(up, _SnowSharpness) * _SnowAmount;

            float3 finalCol = lerp(baseCol, _SnowColor.rgb, snowMask);
            o.Albedo = finalCol;
            o.Alpha = 1.0;
            float3 e = tex2D(_EmissiveTexture, IN.uv_EmissiveTexture).rgb;
            o.Emission = e * _EmissiveIntensity;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
