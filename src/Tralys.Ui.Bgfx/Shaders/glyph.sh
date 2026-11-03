// A distance-field texel holds 128 at the edge and 32 levels per texel either way, higher inside.
float glyphCoverage(float _texel, float _texelsPerPixel)
{
    float distance = 7.96875 * (_texel - 0.50196078431);
    float minified = 1.0 - (clamp(1.0 / _texelsPerPixel, 0.125, 0.25) - 0.125) / 0.125;
    float edge = -0.65 * minified;
    float soft = 0.5 * _texelsPerPixel;
    // The field clamps four texels outside, so a ramp starting past that would tint the box around the glyph.
    return smoothstep(max(edge - soft, -4.0), edge + soft, distance);
}

// A stretch or skew gives each direction its own texels per pixel, so they are measured along the field's slope.
float glyphTexelsPerPixel(vec2 _slope, vec2 _across, vec2 _down)
{
    float slope = length(_slope);
    return slope > 0.0 ? length(vec2(dot(_across, _slope), dot(_down, _slope))) / slope : length(_across);
}
