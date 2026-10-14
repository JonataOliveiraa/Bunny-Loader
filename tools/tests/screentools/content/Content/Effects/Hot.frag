#include <bl/screen>

void main() {
    vec3 c = blScreen(vUV).rgb;
    fragColor = vec4(mix(c, vec3(1.0, 0.0, 0.0), 0.35 * uIntensity), 1.0);
}
