#include <bl/screen>
#include <bl/noise>
#include <bl/color>
uniform float uWarp;

void main() {
    float r = blDistance(vUV);
    if (r > uRadius) discard;

    float f = 1.0 - smoothstep(uRadius * 0.5, uRadius, r);
    vec2 n = vec2(blNoise(vUV * 40.0 + uTime), blNoise(vUV * 40.0 - uTime)) - 0.5;
    vec4 base = blScreen(vUV + n * uWarp * f * uIntensity);
    vec3 tinted = blBlend(base.rgb, uColor.rgb, BL_BLEND_SCREEN);
    fragColor = vec4(mix(base.rgb, tinted, f * 0.6), 1.0);
}
