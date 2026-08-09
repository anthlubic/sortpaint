shader_type canvas_item;

// Draws a lit, beveled bead inside its rect. The bead takes its colour from the node's
// own colour/modulate, so one shared material can serve every sphere on screen.

uniform vec2 light_dir = vec2(-0.45, -0.6);
uniform float radius : hint_range(0.4, 1.0) = 0.86;
uniform float ambient : hint_range(0.0, 1.0) = 0.45;
uniform float specular_strength : hint_range(0.0, 2.0) = 0.7;
uniform float specular_tightness : hint_range(1.0, 128.0) = 24.0;
uniform float rim_darkening : hint_range(0.0, 1.0) = 0.3;
uniform float edge_softness : hint_range(0.001, 0.25) = 0.045;
uniform float shadow_strength : hint_range(0.0, 1.0) = 0.22;
uniform float shadow_offset : hint_range(0.0, 0.5) = 0.14;
uniform float shadow_spread : hint_range(0.0, 0.6) = 0.22;

void fragment() {
	vec2 p = (UV * 2.0 - 1.0) / radius;
	float r = length(p);

	float mask = 1.0 - smoothstep(1.0 - edge_softness, 1.0, r);

	// Sphere normal from the unit disc, so the bead reads as round rather than flat.
	float z = sqrt(max(0.0, 1.0 - min(r * r, 1.0)));
	vec3 normal = vec3(p, z);
	vec3 light = normalize(vec3(light_dir, 0.8));
	float lambert = clamp(dot(normalize(normal), light), 0.0, 1.0);

	vec3 base = COLOR.rgb;
	vec3 lit = base * (ambient + (1.0 - ambient) * lambert);
	lit += vec3(pow(lambert, specular_tightness) * specular_strength);
	lit = mix(lit, base * (1.0 - rim_darkening), smoothstep(0.7, 1.0, r));

	// Contact shadow cast away from the light, so the bead sits on the pixel instead of floating.
	float shade = length(p + light.xy * shadow_offset);
	float shadow = (1.0 - smoothstep(1.0 - edge_softness, 1.0 + shadow_spread, shade)) * shadow_strength;

	float alpha = mask + shadow * (1.0 - mask);
	vec3 rgb = mix(vec3(0.0), lit, mask / max(alpha, 0.0001));

	COLOR = vec4(rgb, COLOR.a * alpha);
}
