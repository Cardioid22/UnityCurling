namespace Curling.Core
{
    public static class Constants
    {
        public const float StoneRadius = 0.145f;
        public const float HouseRadius = 1.829f;
        public const float SheetWidth = 4.75f;
        public const float SheetHalfWidth = SheetWidth * 0.5f;
        public const float SheetLength = 40.234f;

        public const float TeeLineY = 38.405f;
        public const float HouseCenterY = TeeLineY;
        public const float HouseCenterX = 0f;

        public const float HogLineY = 28.345f;
        public const float BackLineY = 40.234f;

        public const float MaxSpeed = 4.0f;

        public const int StonesPerTeamStandard = 8;
        public const int ShotsPerEndStandard = 16;
        public const int PlayersPerTeamStandard = 4;
        public const byte DefaultStandardEndCount = 10;

        public const float DefaultThinkingTimeSec = 219f;
        public const float DefaultExtraEndThinkingTimeSec = 21.9f;

        public const float DefaultStddevSpeed = 0.0076f;
        public const float DefaultStddevAngle = 0.0018f;

        public const float Gravity = 9.81f;
        public const float IceFrictionCoefficient = 0.0168f;
        public const float CurlCoefficient = 0.12f;
        public const float AngularDecel = 0.4f;

        public const float StopLinearEps = 0.001f;
        public const float StopAngularEps = 0.01f;
        public const int StopFramesRequired = 10;
    }
}
