// A fenda: um rasgo escuro que se abre (uOpen), com a borda acesa na cor do
// efeito e tremendo pelo ruído. O giro em volta vem do DrawDistortion.
#include <bl/screen>
#include <bl/noise>
#include <bl/shapes>
uniform float uOpen;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius) discard;

    float angle = atan(d.y, d.x);
    float wobble = blFbm(vec2(angle * 3.0, uTime * 1.5), 3);
    float size = uRadius * 0.45 * uOpen * (0.85 + 0.3 * wobble);
    float tear = length(vec2(d.x * 2.5, d.y));

    float inside = 1.0 - smoothstep(size * 0.8, size, tear);
    float rim = blRing(tear, size, size * 0.3, size * 0.2);
    float glow = blFalloff(r, uRadius) * 0.3;

    vec3 col = mix(blScreen(vUV).rgb, vec3(0.03, 0.0, 0.07), inside * uIntensity);
    col += uColor.rgb * (rim * (1.0 + wobble) + glow) * uIntensity;
    fragColor = vec4(col, 1.0);
}
