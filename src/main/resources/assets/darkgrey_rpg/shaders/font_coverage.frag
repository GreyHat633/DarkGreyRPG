#version 130
uniform sampler2D fontTexture;
void main() {
    vec2 size = vec2(textureSize(fontTexture, 0));
    vec2 p = gl_TexCoord[0].xy * size;
    vec2 footprint = max(abs(dFdx(p)) + abs(dFdy(p)), vec2(0.0001));
    vec2 cellSize = size / 16.0;
    vec2 cellLow = floor(p / cellSize) * cellSize;
    vec2 cellHigh = cellLow + cellSize;
    vec2 low = max(p - footprint * 0.5, cellLow);
    vec2 high = min(p + footprint * 0.5, cellHigh);
    vec2 first = floor(low);
    vec4 total = vec4(0.0);
    // Even subpixel graph labels sample only their own atlas cell, with the full pixel area as denominator.
    for (float row = first.y; row < high.y; row += 1.0) {
        float wy = max(0.0, min(high.y, row + 1.0) - max(low.y, row));
        for (float column = first.x; column < high.x; column += 1.0) {
            float wx = max(0.0, min(high.x, column + 1.0) - max(low.x, column));
            vec4 sampleColor = texelFetch(fontTexture, ivec2(column, row), 0);
            total += vec4(sampleColor.rgb * sampleColor.a, sampleColor.a) * wx * wy;
        }
    }
    if (total.a <= 0.0) discard;
    vec4 result = vec4(total.rgb / total.a, total.a / (footprint.x * footprint.y));
    gl_FragColor = result * gl_Color;
}
