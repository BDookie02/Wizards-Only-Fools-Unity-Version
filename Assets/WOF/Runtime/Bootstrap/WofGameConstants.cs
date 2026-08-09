namespace WOF
{
    public static class WofGameConstants
    {
        public const ushort ProtocolVersion = 1;
        public const ushort DefaultPort = 7777;
        public const int MaxPlayers = 32;
        public const uint ServerTickRate = 30;

        public const int MaxHealth = 100;
        public const int MaxArmor = 50;
        public const float WalkSpeed = 8f;
        public const float JumpSpeed = 8f;
        public const float Gravity = -20f;
        public const float GroundCoyoteSeconds = 0.18f;
        public const float MouseSensitivity = 0.12f;

        public const int FireballDamage = 20;
        public const float FireballSpeed = 22f;
        public const float FireballLifetimeSeconds = 4f;
        public const float GeneralCastCooldownSeconds = 1f;
        public const float RespawnDelaySeconds = 3f;
    }
}

