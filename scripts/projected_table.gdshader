shader_type canvas_item;
render_mode unshaded;

uniform vec2 design_size;
// Horizontal center, near edge in design/screen coordinates, and the perspective slope.
uniform vec4 projection;
uniform float rise;
varying vec4 tint;

void vertex() {
	tint = COLOR;
}

void fragment() {
	vec2 pixel = UV * design_size;
	float dy = pixel.y - projection.z;
	float offset = dy / (rise + dy * projection.w);
	vec2 board_point = vec2(projection.x + (pixel.x - projection.x) * (1.0 - offset * projection.w), projection.y + offset);
	vec2 source = board_point / design_size;
	if (any(lessThan(source, vec2(0.0))) || any(greaterThan(source, vec2(1.0)))) {
		discard;
	}
	COLOR = texture(TEXTURE, source) * tint;
}
