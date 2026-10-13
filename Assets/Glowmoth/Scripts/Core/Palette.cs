using UnityEngine;

namespace Glowmoth
{
    /// <summary>One cohesive colour set per world. All sprites are white masks tinted with these colours.</summary>
    public readonly struct WorldPalette
    {
        public readonly string name;
        public readonly Color bgTop, bgBottom, wall, wallDark, lamp, moth, mothWing, hazard, goal, firefly, mote, accent, text;

        public WorldPalette(string name, uint bgTop, uint bgBottom, uint wall, uint wallDark, uint lamp, uint moth,
                            uint mothWing, uint hazard, uint goal, uint firefly, uint mote, uint accent, uint text)
        {
            this.name = name;
            this.bgTop = Hex(bgTop);
            this.bgBottom = Hex(bgBottom);
            this.wall = Hex(wall);
            this.wallDark = Hex(wallDark);
            this.lamp = Hex(lamp);
            this.moth = Hex(moth);
            this.mothWing = Hex(mothWing);
            this.hazard = Hex(hazard);
            this.goal = Hex(goal);
            this.firefly = Hex(firefly);
            this.mote = Hex(mote);
            this.accent = Hex(accent);
            this.text = Hex(text);
        }

        public static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }

    public static class Palettes
    {
        //                                name               bgTop     bgBottom  wall      wallDark  lamp      moth      wing      hazard    goal      firefly   mote      accent    text
        public static readonly WorldPalette[] Worlds =
        {
            new WorldPalette("Twilight Garden", 0x2B1B4D, 0x0F2A3D, 0x56428A, 0x2E2152, 0xFFD36E, 0xF6EEDC, 0xC9B8FF, 0xFF5D7A, 0x7CF2C8, 0xE8FF7A, 0x8F7BD6, 0xFFD36E, 0xF5F1FF),
            new WorldPalette("Misty Forest",    0x154038, 0x071B1A, 0x2F6352, 0x163A30, 0xFFE08A, 0xEEF5E6, 0x9FE3B5, 0xFF6B5E, 0xF7A6D9, 0xD6FF6B, 0x5FA88A, 0xFFE08A, 0xEAF7EF),
            new WorldPalette("Dusty Attic",     0x45291A, 0x1A0F0A, 0x7A4E36, 0x3E2618, 0xFFC46B, 0xFFF0DC, 0xF2B880, 0xFF5050, 0x8FD6FF, 0xFFE27A, 0xA67C5B, 0xFFC46B, 0xFFF3E6),
            new WorldPalette("Starlit Sky",     0x0E1A4A, 0x050A1F, 0x2F4290, 0x18235A, 0xFFF0A0, 0xF6F7FF, 0x9CC4FF, 0xFF6FAE, 0xFFD86B, 0xB6FFF2, 0x6F86D9, 0xFFE38A, 0xEEF2FF),
        };

        public static WorldPalette ForLevel(int levelIndex)
        {
            return Worlds[LevelDatabase.WorldOf(levelIndex)];
        }
    }
}
