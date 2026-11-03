namespace Chozetumokosatugekiranzan;

internal sealed class KanzenKaihiMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("PERFECT EVADE", 0, 1, 0, 35, 0);
    public override SwordState? RequiredState => SwordState.Sheathed;
    public override bool IsAttack => false;
    public override bool Invincible(Fighter fighter) => fighter.MoveFrame <= CombatTuning.EvadeInvincibleFrames;
    public override bool CanCancel(Fighter fighter, CombatMove target) =>
        fighter.Execution is { EvadeSucceeded: true, EvadeFollowupRemaining: > 0 } && target.IsAttack;

    public override void Tick(Fighter fighter)
    {
        if (fighter.Execution!.EvadeFollowupRemaining > 0) fighter.Execution.EvadeFollowupRemaining--;
    }

    public override void ThreatAvoided(Fighter fighter)
    {
        if (fighter.MoveFrame > CombatTuning.EvadeSuccessFrames || fighter.Execution!.EvadeSucceeded) return;
        fighter.Execution.EvadeSucceeded = true;
        fighter.Execution.EvadeSuccessInputFrame = fighter.InputFrame;
        fighter.Execution.EvadeFollowupRemaining = CombatTuning.EvadeFollowupFrames;
        if (CombatTuning.EvadeAssistEnabled) fighter.EvadeStopRemaining = CombatTuning.EvadeStopFrames;
    }
}
