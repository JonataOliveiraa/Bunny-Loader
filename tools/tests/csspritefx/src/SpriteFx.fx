sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
float3 uColor;
float uOpacity;

float4 UV(float2 coords : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float4 c = tex2D(uImage0, coords);
    return float4(coords.x, coords.y, 0, 1) * c.a;
}

float4 Tint(float2 coords : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float4 c = tex2D(uImage0, coords);
    return float4(c.rgb * uColor, c.a) * sampleColor * uOpacity;
}

float4 Dissolve(float2 coords : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float4 c = tex2D(uImage0, coords);
    float n = tex2D(uImage1, coords).r;
    if (n > uOpacity * 1.1)
        return 0;
    if (n > uOpacity)
        return float4(1, 105.0 / 255, 180.0 / 255, 1);
    return c;
}

technique Technique1
{
    pass UV
    {
        PixelShader = compile ps_2_0 UV();
    }
    pass Tint
    {
        PixelShader = compile ps_2_0 Tint();
    }
    pass Dissolve
    {
        PixelShader = compile ps_2_0 Dissolve();
    }
}
