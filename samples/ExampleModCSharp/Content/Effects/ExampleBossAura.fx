// This is the HLSL source of ExampleBossAura.fxc, the effect used by ExampleBossAuraEffect.
// The phone does not compile .fx files: compile this one on a Windows PC with the fxc.exe of the Windows SDK and ship the .fxc:
//   fxc /T fx_2_0 /Fo ExampleBossAura.fxc ExampleBossAura.fx
// The parameter names are the ones tModLoader's ScreenShaderData fills. FxcScreenEffect fills the same ones before every draw.

// uImage0 is the picture of the game on screen.
sampler uImage0 : register(s0);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uIntensity;
float uTime;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;

// coords goes from (0, 0) at the top-left of the screen to (1, 1) at the bottom-right.
float4 BossAura(float2 coords : TEXCOORD0) : COLOR0
{
    // uTargetPosition is the boss's center in world pixels. Turning it into screen coordinates works like on PC.
    float2 target = (uTargetPosition - uScreenPosition) / uScreenResolution;
    float aspect = uScreenResolution.x / uScreenResolution.y;
    float2 offset = coords - target;
    offset.x *= aspect;
    float dist = length(offset) + 0.0001;

    // Rings that leave the boss over time. The picture is pushed along them, so the world seems to ripple.
    float wave = sin(dist * 45 - uTime * 5);
    float2 push = offset / dist * wave * 0.004 * uIntensity;
    push.x /= aspect;
    float4 color = tex2D(uImage0, coords + push);

    // Away from the boss, the world loses its color and takes uColor. Close to it, the rings glow in uSecondaryColor.
    float away = saturate(dist * 1.8);
    float gray = dot(color.rgb, float3(0.3, 0.59, 0.11));
    float3 tinted = lerp(color.rgb, gray * uColor * 1.4, away * 0.7 * uIntensity);
    float glow = saturate(wave) * (1 - away) * uIntensity;
    tinted += uSecondaryColor * glow * 0.35;

    // uOpacity carries the fade in and out of the effect.
    color.rgb = lerp(color.rgb, tinted, uOpacity);
    return color;
}

technique Technique1
{
    pass BossAura
    {
        PixelShader = compile ps_2_0 BossAura();
    }
}
