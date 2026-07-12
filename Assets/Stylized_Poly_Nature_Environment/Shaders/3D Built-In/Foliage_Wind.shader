Shader "Custom/Foliage_Wind"
{
    Properties
    {
        _ColorTexture ("Color Texture", 2D) = "white" {}
        _EmissiveTexture ("Emissive Texture", 2D) = "black" {}
        _EmissiveIntensity ("Emissive Intensity", Range(0,10)) = 1

        [Header(WIND)]
        [Space(10)]
        _WindStrength ("Wind Strength", Range(0,1)) = 0.2
        _WindSpeed ("Wind Speed", Range(0,15)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow

        sampler2D _ColorTexture;
        sampler2D _EmissiveTexture;
        float _EmissiveIntensity;
        float _WindStrength;
        float _WindSpeed;

        struct Input
        {
            float2 uv_ColorTexture;
            float2 uv_EmissiveTexture;
        };

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
            fixed4 c = tex2D(_ColorTexture, IN.uv_ColorTexture);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            fixed4 e = tex2D(_EmissiveTexture, IN.uv_EmissiveTexture);
            o.Emission = e.rgb * _EmissiveIntensity;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
