shader_type canvas_item;

// Presses a round dimple into a flat square of colour, so the square reads as a socket a bead
// drops into rather than a tile a bead sits on top of. Serves the picture pixels on the board and
// the empty sockets in the tray. The colour comes from the node's own colour/modulate, so one
// shared material can serve every square on screen.
//
// The light matches sphere.gdshader. Because the dimple is concave, its far wall is the one that
// catches the light while the near wall falls into shadow, which is the reverse of a bead.

uniform vec2 light_dir = vec2(-0.45, -0.6);
uniform float radius : hint_range(0.2, 1.0) = 0.78;
uniform float curvature : hint_range(0.0, 2.0) = 0.85;
uniform float shading : hint_range(0.0, 1.0) = 0.34;
uniform float rim_light : hint_range(0.0, 1.0) = 0.26;
uniform float pooling : hint_range(0.0, 1.0) = 0.13;
uniform float edge_softness : hint_range(0.001, 0.4) = 0.09;

// A hairline of shade around the square keeps neighbouring pixels apart, which is what lets a pale
// picture read against a pale page.
uniform float tile_edge : hint_range(0.0, 1.0) = 0.09;
uniform float tile_edge_width : hint_range(0.0, 0.3) = 0.055;

void fragment() {
	vec2 p = (UV * 2.0 - 1.0) / radius;
	float r = length(p);

	// How much of the dimple this pixel is inside. Fading it at the lip both softens the rim and
	// blends the dished normal back to the flat face of the square.
	float pit = 1.0 - smoothstep(1.0 - edge_softness, 1.0, r);

	// Bowl of depth `curvature`, as a height field h = -curvature * (1 - r^2). Its slope is zero at
	// the centre and steepest at the lip, which is where the shading ring wants to be.
	vec3 normal = normalize(vec3(-2.0 * curvature * p * pit, 1.0));
	vec3 light = normalize(vec3(light_dir, 0.8));

	// Measured against the flat face rather than against black, so the square keeps its own colour
	// and only the walls of the dimple move away from it.
	float delta = dot(normal, light) - light.z;

	vec3 col = COLOR.rgb;
	col *= 1.0 - shading * max(0.0, -delta) * 2.0;
	col = mix(col, vec3(1.0), rim_light * max(0.0, delta) * 1.6);

	// Shade gathering in the bottom of the dish, deepest where the dish is deepest.
	col *= 1.0 - pooling * pit * (1.0 - min(r * r, 1.0));

	vec2 toEdge = min(UV, vec2(1.0) - UV);
	float edge = 1.0 - smoothstep(0.0, tile_edge_width, min(toEdge.x, toEdge.y));
	col *= 1.0 - tile_edge * edge;

	COLOR = vec4(col, COLOR.a);
}
