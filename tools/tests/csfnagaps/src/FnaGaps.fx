sampler uImage0 : register(s0);

float uTime : TIME < string UIName = "Time"; float UIMin = 0.5; int Steps = 3; bool Visible = true; >;
float3 uColor;
float4x4 uMatrix;
float uArray[3];

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords) * float4(uColor, 1) * uTime;
    return color + mul(float4(coords, 0, 1), uMatrix) * 0.001 + uArray[1] * 0.001;
}

technique Technique1 < string Kind = "test"; >
{
    pass Main < int Order = 7; >
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
