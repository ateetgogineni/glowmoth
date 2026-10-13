namespace Glowmoth
{
    /// <summary>
    /// Tunable constants in one place. World units: the playfield is 9 x 14 units centred on the origin,
    /// so level designers can think in a simple portrait grid (x -4.5..4.5, y -7..7).
    /// </summary>
    public static class GameConfig
    {
        // Playfield
        public const float FieldHalfWidth = 4.5f;
        public const float FieldHalfHeight = 7f;
        public const float BorderThickness = 0.5f;

        // Camera framing: always show the full width plus the border; leave room above for the HUD.
        public const float ViewHalfWidth = 4.95f;
        public const float ViewMinHalfHeight = 8.6f;

        // Moth flight (simple steering, no physics engine)
        public const float MothRadius = 0.28f;
        public const float MothMaxSpeed = 4.6f;   // cruise speed towards the lamp
        public const float MothSteer = 5.5f;      // how quickly velocity turns towards the desired velocity
        public const float MothDrag = 1.4f;       // slow-down when the moth cannot see a lamp
        public const float ArriveRadius = 1.4f;   // starts slowing within this distance of the lamp
        public const float WallBounce = 0.3f;     // 0 = slide along walls, 1 = full bounce
        public const int SubSteps = 2;            // collision sub-steps per frame

        // Interaction radii
        public const float LampClearance = 0.2f;  // lamps cannot be placed this close to walls
        public const float GoalRadius = 0.55f;
        public const float FireflyRadius = 0.32f;
        public const float SpiderRadius = 0.34f;
        public const float PortalRadius = 0.45f;
        public const float HazardForgiveness = 0.8f; // hazards use a slightly smaller moth radius

        // Timing
        public const int TargetFps = 60;
        public const float WinDuration = 1.1f;
        public const float DeathDuration = 0.9f;
    }
}
