sampler uImage0 : register(s0);

float4 Main(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    return tex2D(uImage0, coords) * color;
}

technique BasicEffect
{
    pass BasicEffect
    {
        PixelShader = compile ps_2_0 Main();
    }
}
