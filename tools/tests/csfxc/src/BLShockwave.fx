sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uIntensity;
float uTime;
float uProgress;
float2 uImageSize1;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;

float4 Shockwave(float2 coords : TEXCOORD0) : COLOR0
{
    float2 target = (uTargetPosition - uScreenPosition) / uScreenResolution;
    float2 d = coords - target;
    float aspect = uScreenResolution.x / uScreenResolution.y;
    d.x *= aspect;
    float dist = length(d) + 0.0001;
    float ring = uProgress * 0.6;
    float k = saturate(1 - abs(dist - ring) / 0.06);
    float2 dir = d / dist;
    dir.x /= aspect;
    float2 uv = coords - dir * k * 0.04 * uIntensity;
    float4 color = tex2D(uImage0, uv);
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    color.rgb = lerp(color.rgb, gray * uColor, uOpacity * (1 - k));
    color.rgb += k * uSecondaryColor * 0.5;
    return color;
}

float4 Stripes(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    float wave = sin(coords.y * 40 + uTime * 6) * 0.5 + 0.5;
    float3 tint = lerp(uColor, uSecondaryColor, wave);
    color.rgb = lerp(color.rgb, color.rgb * tint * 1.6, uOpacity);
    return color;
}

technique Technique1
{
    pass Shockwave
    {
        PixelShader = compile ps_2_0 Shockwave();
    }
    pass Stripes
    {
        PixelShader = compile ps_3_0 Stripes();
    }
}
