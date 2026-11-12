shader_type canvas_item;
render_mode unshaded;

// Procedural playmat: two tinted fields, manga halftone, circuit traces and a live divider.
uniform vec2 size = vec2(1440.0, 1024.0);
uniform float seam = -1.0;         // divider height in UV; negative draws one neutral field
uniform float board_right = 1.0;   // UV where the board ends and the reading dock begins
uniform float active_side = 0.0;   // 1 own turn, -1 rival turn, 0 nobody
uniform vec4 base_color : source_color = vec4(0.027, 0.039, 0.078, 1.0);
uniform vec4 trace_color : source_color = vec4(0.44, 0.56, 0.82, 1.0);
uniform vec4 own_color : source_color = vec4(0.31, 0.89, 0.72, 1.0);
uniform vec4 rival_color : source_color = vec4(1.0, 0.44, 0.56, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float chevron(vec2 p, float width) {
	// Distance to a ">" stroke, the forward mark of the logotype.
	float d = abs(abs(p.y) + p.x);
	return smoothstep(width, width * 0.55, d) * step(abs(p.y), 1.0);
}

void fragment() {
	vec2 px = UV * size;
	bool split = seam >= 0.0;
	float seam_y = (split ? seam : 0.5) * size.y;
	float own = split ? smoothstep(-0.05, 0.05, UV.y - seam) : 0.5;
	vec3 tint = split ? mix(rival_color.rgb, own_color.rgb, own) : trace_color.rgb;
	float lit = split ? mix(max(0.0, -active_side), max(0.0, active_side), own) : 0.0;
	float breath = 0.5 + 0.5 * sin(TIME * 1.3);

	vec3 color = base_color.rgb;
	color += tint * (0.055 + 0.04 * lit);

	// Light pooled around the middle of the board.
	vec2 centre = vec2(size.x * board_right * 0.5, seam_y);
	vec2 rel = (px - centre) / size.y;
	float pool = exp(-dot(rel * vec2(0.75, 1.5), rel * vec2(0.75, 1.5)) * 3.2);
	color += tint * pool * (0.16 + 0.09 * lit * (0.7 + 0.3 * breath));

	// Halftone dots thicken toward each player's edge, like screentone on a manga page.
	float edge = split ? abs(UV.y - seam) * 2.0 : abs(UV.y - 0.5) * 2.0;
	vec2 cell = px / 13.0;
	vec2 dot_uv = fract(vec2(cell.x + floor(cell.y) * 0.5, cell.y)) - 0.5;
	float radius = 0.34 * smoothstep(0.35, 1.05, edge);
	color += tint * smoothstep(radius, radius - 0.07, length(dot_uv)) * 0.085;

	// Diagonal speed lines sweep slowly across the mat.
	float lane = px.x * 0.7071 + px.y * 0.7071;
	float band = floor(lane / 46.0);
	float line = smoothstep(0.5, 0.42, abs(fract(lane / 46.0) - 0.5) * 20.0);
	float travel = fract(hash(vec2(band, 3.0)) + TIME * (0.015 + 0.02 * hash(vec2(band, 9.0))));
	float along = fract((px.x * 0.7071 - px.y * 0.7071) / 900.0 - travel);
	color += trace_color.rgb * line * (0.022 + 0.07 * smoothstep(0.86, 1.0, along) * hash(vec2(band, 5.0)));

	// Faint giant chevrons point each field toward the clash.
	if (split) {
		float dir = own > 0.5 ? -1.0 : 1.0;
		vec2 c = (px - vec2(size.x * board_right * 0.5, seam_y + dir * -size.y * 0.24)) / (size.y * 0.16);
		vec2 q = vec2(c.y * dir, c.x * 0.55);
		float marks = chevron(q, 0.16) + chevron(q + vec2(0.42, 0.0), 0.16);
		color += tint * marks * 0.045;
	}

	// The divider: a hot core, a soft bloom and pulses racing toward the active player.
	if (split) {
		float d = abs(px.y - seam_y);
		float inside = step(UV.x, board_right);
		float fade = smoothstep(0.0, 0.12, UV.x) * smoothstep(board_right, board_right - 0.10, UV.x);
		vec3 seam_tint = mix(mix(rival_color.rgb, own_color.rgb, 0.5), active_side > 0.0 ? own_color.rgb : rival_color.rgb, abs(active_side) * 0.7);
		float core = exp(-d * 0.9) * 0.55 + exp(-d * 0.055) * 0.10;
		float pulse = pow(0.5 + 0.5 * sin(px.x * 0.018 - TIME * 2.2), 10.0);
		color += seam_tint * (core + exp(-d * 0.35) * pulse * 0.45) * fade * inside;
	}

	// Vignette and a touch of grain keep large flat areas alive.
	vec2 v = UV - 0.5;
	color *= 1.0 - dot(v, v) * 0.85;
	color += (hash(px + fract(TIME) * 61.0) - 0.5) * 0.012;
	COLOR = vec4(max(color, vec3(0.0)), 1.0);
}
