namespace Chozetumokosatugekiranzan;

internal enum SwordState { Sheathed, Drawn }
internal enum AttackInput { None, Light, Medium, Heavy, LightMedium, LightHeavy, MediumHeavy, SpecialGuard }
internal enum MoveRequestResult { None, Started, Continued, Busy, WrongState, BranchOnly, Unimplemented, UnknownId, UndefinedCommand }

internal static class CombatTuning
{
    // All combat numbers are prototype values, not finalized balance.
    public static int ChordFrames { get; set; } = 3;
    public static int BufferFrames { get; set; } = 8;
    public static int BreathFrames { get; set; } = 180;
    public static float BreathSpeedMultiplier { get; set; } = 1.25f;
    public static float BreathDamageMultiplier { get; set; } = 1.25f;
    public static int EvadeInvincibleFrames { get; set; } = 14;
    public static int EvadeSuccessFrames { get; set; } = 6;
    public static int EvadeFollowupFrames { get; set; } = 24;
    public static bool EvadeAssistEnabled { get; set; }
    public static int EvadeStopFrames { get; set; } = 6;
    public static int EvadePrebufferFrames { get; set; } = 8;
}

internal sealed class MoveExecution
{
    public required int OriginId { get; init; }
    public int Stage { get; set; } = 1;
    public bool FollowupQueued { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float DamageMultiplier { get; set; } = 1;
    public bool EvadeSucceeded { get; set; }
    public int EvadeFollowupRemaining { get; set; }
    public int EvadeSuccessInputFrame { get; set; }
}

internal abstract class CombatMove
{
    public abstract MoveDefinition Performance { get; }
    public virtual SwordState? RequiredState => null;
    public virtual bool Implemented => true;
    public virtual bool IsAttack => true;
    public virtual bool BranchOnly => false;
    public virtual bool DrawsSword => false;
    public virtual float ForwardSpeed => 0;
    public virtual bool SuperArmor => false;
    public virtual bool Guards => false;
    public virtual int[] BranchTargets => [];
    public virtual void Start(Fighter fighter) { }
    public virtual void Tick(Fighter fighter) { }
    public virtual bool Continue(Fighter fighter, AttackInput input) => false;
    public virtual bool CanBranchTo(int target) => BranchTargets.Contains(target);
    public virtual bool CanCancel(Fighter fighter, CombatMove target) => false;
    public virtual bool CanCancelToMovement => false;
    public virtual bool Invincible(Fighter fighter) => false;
    public virtual void ThreatAvoided(Fighter fighter) { }
    public virtual RectF AttackBox(Fighter fighter)
    {
        var y = GameData.GroundY - fighter.Altitude - fighter.BodyHeight + (fighter.Crouching ? 10 : 24);
        var x = fighter.Facing > 0 ? fighter.X + GameData.FighterWidth / 2f :
            fighter.X - GameData.FighterWidth / 2f - Performance.Range;
        return new(x, y, Performance.Range, Performance.HitHeight);
    }
    public virtual RectF CounterTip(Fighter fighter) => default;

    protected RectF RotatingBox(Fighter fighter)
    {
        var y = GameData.GroundY - fighter.Altitude - fighter.BodyHeight + 24;
        return new(fighter.X - Performance.Range, y, Performance.Range * 2, Performance.HitHeight);
    }

    protected static void RestartStage(Fighter fighter)
    {
        fighter.MoveFrame = 0;
        fighter.AttackHasHit = false;
        fighter.Execution!.FollowupQueued = false;
    }

    protected static MoveDefinition Prototype(string name, int damage = 9, int startup = 5,
        int active = 3, int recovery = 10, int range = 66) =>
        new(name, damage, startup, active, recovery, range, 30, 14, 8, 20, 0, 14, 0);
}
