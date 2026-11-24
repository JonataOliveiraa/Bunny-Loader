sampler uImage0 : register(s0);
float2 uPoints[3];
float3 uTint;

float4 Points(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    float glow = 0;
    for (int i = 0; i < 3; i++)
        glow += 0.002 / (0.001 + distance(coords, uPoints[i]));
    color.rgb += uTint * saturate(glow);
    return color;
}

technique Technique1
{
    pass Points
    {
        PixelShader = compile ps_3_0 Points();
    }
}
