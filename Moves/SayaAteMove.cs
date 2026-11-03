namespace Chozetumokosatugekiranzan;

internal sealed class SayaAteMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("SCABBARD THRUST", 8, 5, 3, 18, 75);
    public override SwordState? RequiredState => SwordState.Sheathed;
    public override RectF AttackBox(Fighter fighter) => fighter.Execution?.Stage == 2
        ? RotatingBox(fighter) : base.AttackBox(fighter);

    public override bool Continue(Fighter fighter, AttackInput input)
    {
        var execution = fighter.Execution!;
        if (input != AttackInput.Medium || execution.Stage >= 3 || execution.FollowupQueued) return false;
        execution.FollowupQueued = true;
        return true;
    }

    public override void Tick(Fighter fighter)
    {
        var execution = fighter.Execution!;
        if (fighter.MoveFrame < Performance.StartupFrames + Performance.ActiveFrames ||
            !execution.FollowupQueued) return;
        execution.Stage++;
        if (execution.Stage == 2)
        {
            fighter.SwordState = SwordState.Drawn;
            execution.DamageMultiplier = fighter.BreathFramesRemaining > 0 ? CombatTuning.BreathDamageMultiplier : 1;
        }
        else
        {
            fighter.SwordState = SwordState.Sheathed;
            execution.DamageMultiplier = 1;
        }
        RestartStage(fighter);
    }
}
