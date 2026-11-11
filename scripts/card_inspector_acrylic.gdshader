shader_type canvas_item;
render_mode unshaded;

uniform sampler2D backdrop : hint_screen_texture, repeat_disable, filter_linear_mipmap;

void fragment() {
	vec3 frosted = textureLod(backdrop, SCREEN_UV, 3.0).rgb;
	float grain = fract(dot(fract(FRAGCOORD.xy * vec2(0.06711056, 0.00583715)), vec2(52.9829189, 52.9829189)));
	COLOR.rgb = mix(frosted, COLOR.rgb, 0.70) + (grain - 0.5) * 0.012;
}
