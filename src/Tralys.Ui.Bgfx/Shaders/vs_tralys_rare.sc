$input a_position, a_texcoord0, a_texcoord1, a_texcoord2, a_texcoord3, a_texcoord4, a_texcoord5, a_texcoord6, a_texcoord7, a_color2, a_color0, a_color1, a_color3, a_tangent
$output v_local, v_bounds, v_shape, v_inner, v_innerRadii, v_fillGradient, v_strokeGradient, v_params, v_clip, v_device, v_fill, v_stroke, v_window, v_sample

#include <bgfx_shader.sh>

void main()
{
    gl_Position = mul(u_modelViewProj, vec4(a_position, 0.0, 1.0));
    v_device = a_position;
    v_local = a_texcoord0;
    v_bounds = a_texcoord1;
    v_shape = a_texcoord2;
    v_inner = a_texcoord3;
    v_innerRadii = a_texcoord4;
    v_fillGradient = a_texcoord5;
    v_strokeGradient = a_texcoord6;
    v_params = a_texcoord7;
    v_clip = a_color2;
    v_fill = a_color0;
    v_stroke = a_color1;
    v_window = a_color3;
    v_sample = a_tangent;
}
