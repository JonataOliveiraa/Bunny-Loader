float4x4 uWorldViewProjection;
sampler uImage0 : register(s0);

struct VertexIn
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 Coords : TEXCOORD0;
};

struct VertexOut
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 Coords : TEXCOORD0;
};

VertexOut Vertex(VertexIn input)
{
    VertexOut output;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Color = input.Color;
    output.Coords = input.Coords;
    return output;
}

float4 Pixel(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    return float4(coords.x, coords.y, 0, 1) * tex2D(uImage0, coords).a;
}

technique Technique1
{
    pass Trail
    {
        VertexShader = compile vs_2_0 Vertex();
        PixelShader = compile ps_2_0 Pixel();
    }
}
