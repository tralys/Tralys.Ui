$input a_position, a_texcoord0, a_texcoord1, a_texcoord2, a_texcoord3, a_texcoord4, a_texcoord7, a_color0, a_color1, a_color3, a_tangent
$output v_local, v_bounds, v_shape, v_innerBox, v_innerRadii, v_params, v_fill, v_stroke, v_window, v_safe, v_center

#include <bgfx_shader.sh>

void main()
{
    gl_Position = mul(u_modelViewProj, vec4(a_position, 0.0, 1.0));
    v_local = a_texcoord0;
    v_bounds = a_texcoord1;
    v_shape = a_texcoord2;
    v_innerBox = a_texcoord3;
    v_innerRadii = a_texcoord4;
    v_fill = a_color0;
    v_stroke = a_color1;
    v_window = vec4(a_color3.xy + a_tangent.yz, a_color3.zw - a_tangent.yz);

    float flags = a_texcoord7.y;
    float aa = a_texcoord7.z;
    v_params = vec4(a_texcoord7.x, mod(floor(flags / 16.0), 2.0), aa, a_tangent.x);

    vec2 extent = a_texcoord3.zw * 0.5;
    float radius = max(max(a_texcoord4.x, a_texcoord4.y), max(a_texcoord4.z, a_texcoord4.w));
    v_center = a_texcoord3.xy + extent;
    v_safe = vec4(extent - vec2(aa, aa) * 0.5, extent - vec2(radius, radius));
}
