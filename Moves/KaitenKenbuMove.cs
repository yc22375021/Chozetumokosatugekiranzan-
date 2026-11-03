namespace Chozetumokosatugekiranzan;

internal sealed class KaitenKenbuMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("ROTATING SWORD DANCE", 7, 4, 4, 14, 90);
    public override bool BranchOnly => true;
    public override bool DrawsSword => true;
    public override RectF AttackBox(Fighter fighter) => RotatingBox(fighter);
    public override bool Continue(Fighter fighter, AttackInput input)
    {
        if (input != AttackInput.Medium || fighter.Execution!.Stage >= 6 || fighter.Execution.FollowupQueued)
            return false;
        fighter.Execution.FollowupQueued = true;
        return true;
    }

    public override void Tick(Fighter fighter)
    {
        if (fighter.MoveFrame < Performance.StartupFrames + Performance.ActiveFrames ||
            !fighter.Execution!.FollowupQueued) return;
        fighter.Execution.Stage++;
        RestartStage(fighter);
    }
}
