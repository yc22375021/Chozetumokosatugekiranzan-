namespace Chozetumokosatugekiranzan;

internal sealed class ZanMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("SLASH", 9, 5, 3, 14, 70);
    public override SwordState? RequiredState => SwordState.Drawn;
    public override int[] BranchTargets => [MoveIds.NikukotsuDanzai];
    public override bool Continue(Fighter fighter, AttackInput input)
    {
        // Initial slash plus three additional presses: four hits maximum.
        if (input != AttackInput.Light || fighter.Execution!.Stage >= 4 || fighter.Execution.FollowupQueued)
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
