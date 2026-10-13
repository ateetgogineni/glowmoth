# Prefabs

Glowmoth has no prefab assets. Every game object is a "code prefab" defined in
`Scripts/Gameplay/Entities.cs` (moth, lamp, goal, walls, thorns, spiders, drafts, portals,
fireflies) and recycled by `Pool<T>`. Sprites are generated at runtime by `SpriteFactory`.

To use hand-made prefabs instead, create them here and change the matching entity class to
instantiate your prefab in its constructor. The pooling and level code stay the same.
