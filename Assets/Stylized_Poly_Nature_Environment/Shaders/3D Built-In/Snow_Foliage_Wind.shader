Shader "Custom/Snow_Foliage_Wind"
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

        [Header(WIND)]
        [Space(10)]
        _WindStrength ("Wind Strength", Range(0,1)) = 0.2
        _WindSpeed ("Wind Speed", Range(0,10)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        sampler2D _ColorTexture;
        sampler2D _EmissiveTexture;
        float _EmissiveIntensity;

        float4 _SnowColor;
        float _SnowAmount;
        float _SnowSharpness;

        float _WindStrength;
        float _WindSpeed;

        struct Input
        {
            float2 uv_ColorTexture;
            float2 uv_EmissiveTexture;
            float3 worldNormal;
            float facing : VFACE;
        };

        // Wind Animation
        void vert (inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float mask = v.color.r;
            float swayX = sin(worldPos.z * 0.3 + _Time.y * _WindSpeed) * _WindStrength * mask;
            float swayY = cos(worldPos.x * 0.2 + _Time.y * (_WindSpeed * 0.5)) * (_WindStrength * 0.5) * mask;
            v.vertex.x += swayX;
            v.vertex.y += swayY;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 baseCol = tex2D(_ColorTexture, IN.uv_ColorTexture).rgb;
            float3 n = normalize(IN.worldNormal) * (IN.facing > 0 ? 1 : -1);
            float up = saturate(dot(n, float3(0,1,0)));

            // Snow shading
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
