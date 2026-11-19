shader_type canvas_item;
render_mode unshaded;

uniform vec2 viewport_size = vec2(1.0);
uniform vec2 origin;
uniform float unit = 1.0;
uniform vec4 projection;
uniform float rise;
uniform vec4 field; // width, far edge, seam, near edge in board coordinates
uniform vec2 unrolled; // rival from the left, player from the right
uniform vec4 background : source_color;
uniform vec4 cloth : source_color;
uniform vec4 rival_color : source_color;
uniform vec4 own_color : source_color;

vec2 unproject(vec2 pixel) {
	float dy = pixel.y - projection.z;
	float offset = dy / (rise + dy * projection.w);
	return vec2(projection.x + (pixel.x - projection.x) * (1.0 - offset * projection.w), projection.y + offset);
}

float inset(vec2 p, vec2 low, vec2 high) {
	vec2 d = min(p - low, high - p);
	return min(d.x, d.y);
}

void fragment() {
	vec2 pixel = (UV * viewport_size - origin) / unit;
	vec2 p = unproject(pixel);
	float is_own = step(field.z, p.y);
	vec3 accent = mix(rival_color.rgb, own_color.rgb, is_own);
	float glow = exp(-dot(UV - 0.5, UV - 0.5) * 8.0);
	vec3 floor_color = background.rgb + glow * (vec3(0.024, 0.036, 0.054) + accent * 0.029);
	float grain = fract(sin(dot(floor(UV * viewport_size), vec2(41.71, 92.83))) * 43758.5453) - 0.5;
	floor_color += grain * 0.012;

	vec2 left_field = vec2(0.0, field.y);
	vec2 right_field = vec2(field.x * unrolled.x, field.z);
	vec2 own_left = vec2(field.x * (1.0 - unrolled.y), field.z);
	vec2 own_right = vec2(field.x, field.w);
	vec2 shadow_p = unproject(pixel - vec2(0.0, 9.0));
	float shadow = max(inset(shadow_p, left_field, right_field), inset(shadow_p, own_left, own_right));
	if (max(unrolled.x, unrolled.y) > 0.0) {
		floor_color = mix(floor_color, background.rgb * 0.65, 0.64 * smoothstep(-10.0, 4.0, shadow));
	}

	vec2 low = mix(left_field, own_left, is_own);
	vec2 high = mix(right_field, own_right, is_own);
	float edge = inset(p, low, high);
	float deployed = mix(unrolled.x, unrolled.y, is_own);
	float center_light = exp(-length((p - vec2(field.x * 0.5, field.z)) / vec2(720.0, 300.0)) * 1.7);
	vec3 mat = cloth.rgb + accent * (0.047 + center_light * 0.16);
	vec2 dots = fract(p / 13.0) - 0.5;
	mat += accent * 0.1 * (1.0 - smoothstep(0.11, 0.22, length(dots))) * (1.0 - center_light * 0.7);
	float trace = 1.0 - smoothstep(0.017, 0.039, abs(fract((p.x + p.y * 0.83) / 57.0) - 0.5));
	mat += accent * trace * 0.027;
	// Two fabric chevrons, in the same direction as the marks on the regular arena.
	float fold = abs(p.x - field.x * 0.5) * 0.36 + abs(p.y - field.z);
	float mark = min(abs(fold - 160.0), abs(fold - 209.0));
	mat += accent * 0.072 * (1.0 - smoothstep(6.0, 9.0, mark)) * (1.0 - smoothstep(208.0, 246.0, abs(p.x - field.x * 0.5)));
	float outer = min(min(p.x, field.x - p.x), min(p.y - low.y, high.y - p.y));
	float stitch = (1.0 - smoothstep(1.0, 2.2, abs(outer - 10.0))) * step(0.35, fract((p.x + p.y) / 14.0));
	mat = mix(mat, accent, 0.31 * stitch + 0.42 * (1.0 - smoothstep(0.4, 2.0, outer)));
	float coverage = smoothstep(-0.75, 0.75, edge) * step(0.0005, deployed);
	COLOR *= vec4(mix(floor_color, mat, coverage), 1.0);
}
