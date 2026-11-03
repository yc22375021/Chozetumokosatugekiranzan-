using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Chozetumokosatugekiranzan;

internal enum AnimationKind
{
    Idle,
    Walk,
    Crouch,
    JumpStart,
    JumpRise,
    JumpFall,
    Attack,
    Hit,
    Guard
}

internal readonly record struct KeyBindings(
    Keys Left,
    Keys Right,
    Keys Jump,
    Keys Down,
    Keys Light,
    Keys Heavy,
    Keys DebugOverlay,
    Keys Start,
    Keys Quit,
    Keys Help,
    Keys ToggleInstructions,
    Keys Medium,
    Keys Guard);

internal readonly record struct MoveDefinition(
    string Name,
    int Damage,
    int StartupFrames,
    int ActiveFrames,
    int RecoveryFrames,
    int Range,
    int HitHeight,
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
    public const int MatchTimeSeconds = 99;
    public const int MaximumHealth = 100;
    public const int MaximumMeter = 100;
    public const float WalkSpeed = 3.4f;
    public const float JumpSpeed = 13.2f;
    public const float Gravity = 0.62f;
    public const float SpriteScale = 2.4f;
    public const int FighterWidth = 40;
    public const int StandingHeight = 82;
    public const int CrouchingHeight = 52;
    public const int CpuDecisionMinFrames = 12;
    public const int CpuDecisionMaxFrames = 20;
    public const int CpuApproachDistance = 112;
    public const int JumpPrepFrames = 3;

    public static readonly KeyBindings Controls = new(
        Keys.A, Keys.D, Keys.W, Keys.S, Keys.J, Keys.K, Keys.F3, Keys.Enter, Keys.Escape,
        Keys.F1, Keys.Space, Keys.U, Keys.L);

    public static readonly IReadOnlyDictionary<AnimationKind, SpriteAnimationDefinition> SpriteAnimations =
        new Dictionary<AnimationKind, SpriteAnimationDefinition>
        {
            [AnimationKind.Idle] = Candidate("3316.png"),
            [AnimationKind.Walk] = Candidate("3312.png"),
            [AnimationKind.Crouch] = Candidate("3317.png"),
            [AnimationKind.JumpStart] = Candidate("3313.png", loop: false),
            [AnimationKind.JumpRise] = Candidate("3313.png"),
            [AnimationKind.JumpFall] = Candidate("3313.png"),
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

    private static SpriteAnimationDefinition Candidate(string fileName, bool loop = true) =>
        new(fileName, [], loop);
}
