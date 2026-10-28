using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Chozetumokosatugekiranzan;

internal enum MoveId
{
    None,
    Light,
    Heavy,
    Special
}

internal enum AnimationKind
{
    Idle,
    Walk,
    Crouch,
    Airborne,
    Attack,
    Hit,
    Guard
}

internal readonly record struct MoveDefinition(
    string Name,
    int Damage,
    int StartupFrames,
    int ActiveFrames,
    int RecoveryFrames,
    int Range,
    int HitstunFrames,
    int GuardstunFrames,
    int Pushback,
    int MeterCost,
    int MeterGain,
    int GuardDamage)
{
    public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;
}

internal readonly record struct SpriteFrameDefinition(Rectangle Source, Vector2 Origin, int DurationFrames);

internal sealed record SpriteAnimationDefinition(string FileName, SpriteFrameDefinition[] Frames, bool Loop);

internal static class GameData
{
    public const int ScreenWidth = 960;
    public const int ScreenHeight = 540;
    public const int GroundY = 438;
    public const float StageLeft = 64;
    public const float StageRight = 896;
    public const int FramesPerSecond = 60;
    public const int CommandWindowFrames = 16;
    public const int InputBufferCapacity = 24;
    public const int MatchTimeSeconds = 99;
    public const int MaximumHealth = 100;
    public const int MaximumMeter = 100;
    public const int SpecialMeterCost = 35;
    public const float WalkSpeed = 3.4f;
    public const float JumpSpeed = 13.2f;
    public const float Gravity = 0.62f;
    public const float SpriteScale = 2.4f;
    public const int FighterWidth = 40;
    public const int StandingHeight = 82;
    public const int CrouchingHeight = 52;
    public const int HealthPerHit = 10;

    public static readonly MoveDefinition Light = new(
        "LIGHT", 9, 5, 3, 10, 66, 14, 8, 20, 0, 14, 2);

    public static readonly MoveDefinition Heavy = new(
        "HEAVY", 15, 9, 4, 17, 82, 22, 13, 34, 0, 18, 3);

    public static readonly MoveDefinition Special = new(
        "SPECIAL", 24, 11, 5, 23, 142, 32, 19, 52, SpecialMeterCost, 20, 4);

    public static readonly IReadOnlyDictionary<MoveId, MoveDefinition> Moves =
        new Dictionary<MoveId, MoveDefinition>
        {
            [MoveId.Light] = Light,
            [MoveId.Heavy] = Heavy,
            [MoveId.Special] = Special
        };

    public static readonly IReadOnlyDictionary<Keys, string> KeyBindings =
        new Dictionary<Keys, string>
        {
            [Keys.A] = "Left",
            [Keys.D] = "Right",
            [Keys.W] = "Jump",
            [Keys.S] = "Down / Crouch",
            [Keys.J] = "Light",
            [Keys.K] = "Heavy",
            [Keys.F2] = "Debug Special",
            [Keys.F3] = "Debug Overlay",
            [Keys.Enter] = "Start / Rematch",
            [Keys.Escape] = "Quit"
        };

    public static readonly IReadOnlyDictionary<AnimationKind, SpriteAnimationDefinition> SpriteAnimations =
        new Dictionary<AnimationKind, SpriteAnimationDefinition>
        {
            [AnimationKind.Idle] = Candidate("3316.png"),
            [AnimationKind.Walk] = Candidate("3312.png"),
            [AnimationKind.Crouch] = Candidate("3317.png"),
            [AnimationKind.Airborne] = Candidate("3313.png"),
            [AnimationKind.Attack] = Candidate("3316.png"),
            [AnimationKind.Hit] = Candidate("3316.png"),
            [AnimationKind.Guard] = Candidate("3315.png")
        };

    public static readonly string[] KnownSpriteCandidates =
    [
        "3312.png",
        "3313.png",
        "3314.png",
        "3315.png",
        "3316.png",
        "3317.png"
    ];

    private static SpriteAnimationDefinition Candidate(string fileName) =>
        new(fileName, [], Loop: true);
}
