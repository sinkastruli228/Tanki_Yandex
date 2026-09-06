# Soft Arcade Sand

`SoftArcadeSand.mat` uses the standard URP Terrain/Lit shader. The texture and its 38 × 38 world-unit tiling are configured in `SoftArcadeSand.terrainlayer`. The sand is matte (metallic 0, smoothness 0.04), without a normal map to keep the surface calm at the gameplay camera distance.

`SoftArcadeTerrain.asset` preserves the original terrain geometry, trees and details, with a new sand paint layer. The original terrain data and `New Material.mat` are retained. To restore the old ground, assign the original data to both Terrain and TerrainCollider, and reassign `New Material.mat` to Terrain.

Texture generated with the built-in imagegen tool. Final generation prompt:

> Create a single production-ready seamlessly tileable square albedo texture for soft stylized sand in a low-poly arcade tank game. Flat orthographic top-down material scan, edge-to-edge texture only. Warm pale biscuit and muted creamy beige sand, average sRGB approximately #DCC397. Extremely low contrast: broad gently meandering wind ripples only faintly visible, irregular organic spacing, soft diffuse pigment variation. Surface almost smooth and matte, no sharp grain, no stones, no objects, no cracks, no footprints, no lighting gradients, no cast shadows, no specular highlights, no vignette, no perspective, no text or border. Avoid strong parallel stripes and obvious repeated motifs. Both opposing edges must match seamlessly for repeat wrapping. This is a game base-color map, not a rendered landscape; all lighting will be applied in Unity.

The source PNG is kept at generated resolution. Unity imports it at a maximum of 1024 pixels with mipmaps, repeat wrapping and trilinear filtering.
