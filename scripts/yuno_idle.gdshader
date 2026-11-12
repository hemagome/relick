shader_type canvas_item;

// Rest loop for Yuno drawn from one still: the motion is a function of time, so it is as smooth
// as the display allows and has no first or last frame to join.
uniform float phase = 0.0;      // seconds; keeps both duelists out of step
uniform float strength = 1.0;   // lowered for reduced motion
uniform float clock = -1.0;     // fixed time for previews and captures; negative follows TIME
const vec2 CANVAS = vec2(608.0, 504.0);
const float PAD = 16.0;         // transparent margin around the canvas in the still

varying vec4 tint;

void vertex() {
	tint = COLOR; // fragment COLOR already includes the unwarped texture sample
}

void fragment() {
	float t = (clock < 0.0 ? TIME : clock) + phase;
	vec2 size = CANVAS + vec2(PAD * 2.0);
	vec2 p = (UV * size - vec2(PAD)) / CANVAS;

	float breath = sin(t * TAU / 3.6);
	float late = sin(t * TAU / 3.6 - 0.9);
	vec2 shift = vec2(0.0); // in canvas pixels; positive moves the drawing right/down

	// The chest lifts everything above the waist; the hem stays planted.
	float rise = smoothstep(1.0, 0.50, p.y);
	shift.y -= breath * 3.0 * rise;
	float torso = smoothstep(0.98, 0.72, p.y) * smoothstep(0.42, 0.56, p.y);
	shift.x += (p.x - 0.45) * CANVAS.x * 0.010 * breath * torso;

	// The head settles a beat after the shoulders.
	float head = smoothstep(0.52, 0.38, p.y);
	shift.y -= late * 1.3 * head;

	// Hair: the further from the scalp, the more it trails.
	float tip = pow(clamp((0.36 - p.y) / 0.36, 0.0, 1.0), 1.5);
	shift.x += sin(t * TAU / 2.9 + p.y * 8.0 + p.x * 3.0) * 3.4 * tip;
	shift.y += cos(t * TAU / 2.9 + p.x * 6.0) * 1.1 * tip;

	// The raised guard floats on its own, slightly behind the breath.
	float arm = smoothstep(0.56, 0.76, p.x);
	shift.y += sin(t * TAU / 3.6 - 1.7) * 2.2 * arm;

	// The coat tail drifts.
	float tail = smoothstep(0.36, 0.08, p.x) * smoothstep(0.70, 0.92, p.y);
	shift.x += sin(t * TAU / 4.4 + p.y * 5.0) * 2.4 * tail;

	vec2 source = UV - shift * strength / size;
	vec4 color = texture(TEXTURE, source);
	color.a *= step(0.0, source.x) * step(source.x, 1.0) * step(0.0, source.y) * step(source.y, 1.0);

	// The gauntlet's cells pulse; the eyes share the hue but sit outside this region.
	float cell = smoothstep(0.22, 0.55, color.b - color.r) * step(0.5, p.x) * step(0.52, p.y);
	color.rgb += vec3(0.20, 0.50, 0.62) * cell * (0.5 + 0.5 * sin(t * TAU / 1.9)) * 0.55 * strength;
	COLOR = color * tint;
}
