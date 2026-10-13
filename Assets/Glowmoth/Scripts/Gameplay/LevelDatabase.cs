namespace Glowmoth
{
    /// <summary>
    /// The 20 hand-made levels: 4 worlds x 5 levels. A new idea is introduced every few levels:
    ///   1-3   tap a lamp, walls, fireflies          4-5   walls block light (line of sight)
    ///   6-7   thorns                                8-10  patrolling spiders
    ///   11-13 drafts that push the moth             14-15 sliding shelves (moving walls)
    ///   16-18 portals                               19    candles that burn out       20 finale
    ///
    /// Coordinates: playfield x -4.5..4.5, y -7..7 (border walls are added automatically).
    /// Helpers for walls attached to a side:
    ///   left-attached ending at x = e  ->  Wall((-4.5 + e) / 2, y, e + 4.5, h)
    ///   right-attached starting at x = s -> Wall((s + 4.5) / 2, y, 4.5 - s, h)
    /// </summary>
    public static class LevelDatabase
    {
        public const int LevelsPerWorld = 5;
        public static readonly string[] WorldNames = { "Twilight Garden", "Misty Forest", "Dusty Attic", "Starlit Sky" };

        static readonly LevelData[] Levels =
        {
            // ---------------- World 1: Twilight Garden ----------------
            new LevelData("First Light", 1).Moth(0f, -5f).Home(0f, 4.5f)
                .Tip("Tap anywhere to place your lamp.\nThe moth flies toward the light."),

            new LevelData("Around the Hedge", 3).Moth(-2.5f, -5f).Home(-2.5f, 4.5f)
                .Wall(-1.5f, 0f, 6f, 0.6f)
                .Tip("Hedges block the way. Lead the moth around them."),

            new LevelData("Firefly Meadow", 4).Moth(0f, -5.5f).Home(0f, 5f)
                .Wall(1.8f, -2.8f, 2.4f, 0.5f).Wall(-1.8f, 2f, 2.4f, 0.5f)
                .Fly(-3f, -2f).Fly(3f, 0.5f).Fly(-2.5f, 3.2f)
                .Tip("Collect every firefly for a bonus star."),

            new LevelData("In the Shadows", 4).Moth(-3f, -5f).Home(-3f, 4.5f)
                .Wall(-1.25f, -1.5f, 6.5f, 0.6f).Wall(-0.5f, 2f, 0.5f, 4f)
                .Fly(3.6f, -5f).Fly(1.5f, 6f)
                .Tip("Walls block light too.\nThe moth must SEE the lamp to follow it."),

            new LevelData("Zigzag Lane", 7).Moth(-3f, -6f).Home(-3f, 5.6f)
                .Wall(-1.25f, -3.5f, 6.5f, 0.5f).Wall(1.25f, 0.5f, 6.5f, 0.5f).Wall(-1.25f, 3.8f, 6.5f, 0.5f)
                .Fly(3.3f, -1.5f).Fly(-3.3f, 2.2f).Fly(3.3f, 5.8f),

            // ---------------- World 2: Misty Forest ----------------
            new LevelData("Thorn Patch", 3).Moth(0f, -5.5f).Home(0f, 5f)
                .Thorn(0f, 0f, 5f, 0.6f)
                .Fly(-3.6f, 0f).Fly(3.6f, 2.5f)
                .Tip("Thorns are sharp. Keep the moth away from them!"),

            new LevelData("Bramble Walk", 6).Moth(-3f, -6f).Home(3f, 5.8f)
                .Wall(-1.5f, -3f, 6f, 0.5f).Thorn(1.5f, 1f, 6f, 0.5f).Wall(-1.5f, 4f, 6f, 0.5f)
                .Fly(3.6f, -5f).Fly(-3.5f, 2.6f),

            new LevelData("Spider Watch", 2).Moth(0f, -5.5f).Home(0f, 5f)
                .Spider(-3.5f, 0f, 3.5f, 0f, 1.8f)
                .Fly(-3.5f, 3f)
                .Tip("Spiders patrol back and forth. Time your move!"),

            new LevelData("Silk Curtains", 4).Moth(-3.5f, -5.5f).Home(3.5f, 5.5f)
                .Spider(-1.5f, -3.5f, -1.5f, 3.5f, 2f).Spider(1.5f, 3.5f, 1.5f, -3.5f, 2f)
                .Fly(0f, 0f),

            new LevelData("Web Garden", 5).Moth(0f, -5.5f).Home(-3f, 5.5f)
                .Wall(-2.75f, -2f, 3.5f, 0.5f).Wall(2.75f, -2f, 3.5f, 0.5f)
                .Spider(-2.5f, -0.6f, 2.5f, -0.6f, 1.5f)
                .Thorn(-1.5f, 2.8f, 6f, 0.5f)
                .Fly(3.6f, -4.5f).Fly(3.6f, 4.5f),

            // ---------------- World 3: Dusty Attic ----------------
            new LevelData("Drafty Window", 3).Moth(-3f, -5.5f).Home(-3f, 5f)
                .Gust(0f, 0.5f, 9f, 3f, 3.2f, 0f)
                .Thorn(4.1f, 0.5f, 0.8f, 3.6f)
                .Fly(-3.6f, 0.5f)
                .Tip("Drafts push the moth. Lean into the wind!"),

            new LevelData("Updraft", 3).Moth(-3f, -5.5f).Home(3f, 5.5f)
                .Wall(1f, 0f, 0.5f, 10f)
                .Gust(2.85f, 0f, 3.3f, 10f, 0f, 3f)
                .Fly(1f, -6f).Fly(3f, -2f),

            new LevelData("Crosswinds", 4).Moth(0f, -5.5f).Home(0f, 5.2f)
                .Gust(0f, -2f, 9f, 2f, -3.2f, 0f).Gust(0f, 3f, 9f, 2f, 3.2f, 0f)
                .Thorn(-4.1f, -2f, 0.8f, 2.4f).Thorn(4.1f, 3f, 0.8f, 2.4f)
                .Fly(3.6f, -2f).Fly(-3.6f, 3f),

            new LevelData("Sliding Shelf", 3).Moth(0f, -5.5f).Home(0f, 5f)
                .Slider(-2f, 0f, 5f, 0.6f, 4f, 0f, 4f)
                .Fly(0f, -2.5f)
                .Tip("Shelves slide back and forth. Wait for a gap."),

            new LevelData("Clockwork", 5).Moth(0f, -6f).Home(0f, 5.6f)
                .Slider(-2f, -2.5f, 5f, 0.5f, 4f, 0f, 3.5f)
                .Slider(2f, 2.5f, 5f, 0.5f, -4f, 0f, 3.5f)
                .Thorn(0f, 0f, 1.2f, 0.5f)
                .Fly(-3.6f, 0f).Fly(3.6f, 0f),

            // ---------------- World 4: Starlit Sky ----------------
            new LevelData("Star Gate", 3).Moth(0f, -5.5f).Home(0f, 5f)
                .Wall(0f, 0f, 9f, 0.6f)
                .Portal(-3f, -3f, 3f, 3f)
                .Fly(-3.5f, 4.5f)
                .Tip("Portals link two places. Fly in, pop out!"),

            new LevelData("Twin Gates", 5).Moth(-3f, -6f).Home(2.5f, 5.2f)
                .Wall(0f, -2f, 9f, 0.5f).Wall(0f, 2.5f, 9f, 0.5f)
                .Portal(0f, -4.5f, -3f, 0.2f).Portal(3f, 0.2f, -2.5f, 5f)
                .Fly(3.6f, -5.5f).Fly(-3.6f, 1.6f),

            new LevelData("Comet Run", 4).Moth(0f, -5.5f).Home(0f, 6f)
                .Wall(0f, 0f, 9f, 0.6f)
                .Portal(-3.5f, -3f, 3.5f, 3f)
                .Thorn(-1f, -3f, 2.5f, 0.5f)
                .Spider(-3f, 4.5f, 3f, 4.5f, 2.2f)
                .Fly(3.6f, -5.5f).Fly(-3f, 2.5f),

            new LevelData("Short Candles", 7).Moth(-3f, -6f).Home(3f, 5.8f)
                .Candles(2.5f)
                .Wall(-1.25f, -3f, 6.5f, 0.5f).Wall(1.25f, 1f, 6.5f, 0.5f).Wall(-1.25f, 4.3f, 6.5f, 0.5f)
                .Thorn(0f, 2.6f, 1.6f, 0.5f)
                .Fly(3.6f, -1f).Fly(-3.6f, 2.7f)
                .Tip("Candles burn out fast! Keep relighting the way."),

            new LevelData("Dawn", 6).Moth(-3f, -6f).Home(0f, 6f)
                .Gust(0f, -3f, 9f, 2f, -3f, 0f)
                .Thorn(-4.1f, -3f, 0.8f, 2.2f)
                .Wall(0f, -0.5f, 9f, 0.5f)
                .Portal(3f, -1.4f, -3f, 1.5f)
                .Slider(-2f, 3.8f, 4.5f, 0.5f, 4f, 0f, 4f)
                .Spider(3f, 1f, 3f, 5f, 1.6f)
                .Fly(3.6f, -5.5f).Fly(1f, 2f).Fly(-3.6f, 5.2f),
        };

        public static int Count => Levels.Length;

        public static LevelData Get(int index) => Levels[index];

        public static int WorldOf(int index) => index / LevelsPerWorld;
    }
}
