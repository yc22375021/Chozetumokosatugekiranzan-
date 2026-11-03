namespace Chozetumokosatugekiranzan;

internal sealed class ShinkokyuMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("DEEP BREATH", 0, 1, 0, 23, 0);
    public override SwordState? RequiredState => SwordState.Sheathed;
    public override bool IsAttack => false;
    public override void Start(Fighter fighter) => fighter.BreathFramesRemaining = CombatTuning.BreathFrames;
}
