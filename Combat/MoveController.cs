namespace Chozetumokosatugekiranzan;

internal static class MoveController
{
    public static MoveRequestResult Request(Fighter fighter, string key)
    {
        if (MoveRegistry.Default.TryGet(key, out var registration)) return Request(fighter, registration.Id);
        fighter.LastRequestedMoveId = MoveIds.None;
        return Report(fighter, MoveRequestResult.UnknownId);
    }

    public static MoveRequestResult Request(Fighter fighter, int id)
    {
        fighter.LastRequestedMoveId = id;
        if (!MoveRegistry.Default.TryGet(id, out var registration))
            return Report(fighter, MoveRequestResult.UnknownId);
        var move = registration.Move;
        if (!move.Implemented) return Report(fighter, MoveRequestResult.Unimplemented);
        if (!fighter.Grounded || fighter.Health <= 0 ||
            fighter.Action is FighterAction.Hitstun or FighterAction.Guardstun or FighterAction.Knockout)
            return Report(fighter, MoveRequestResult.Busy);
        var active = Active(fighter);
        var evadeCancel = active is KanzenKaihiMove && active.CanCancel(fighter, move);
        if (move.BranchOnly && !evadeCancel && (active is null || !active.CanBranchTo(id)))
            return Report(fighter, MoveRequestResult.BranchOnly);
        if (!evadeCancel && move.RequiredState is { } state && state != fighter.SwordState)
            return Report(fighter, MoveRequestResult.WrongState);
        if (active is not null && !active.CanBranchTo(id) && !active.CanCancel(fighter, move))
            return Report(fighter, MoveRequestResult.Busy);

        var origin = active is not null && (active.CanBranchTo(id) || evadeCancel) ? fighter.Execution!.OriginId : id;
        fighter.Execution = new MoveExecution
        {
            OriginId = origin,
            DamageMultiplier = fighter.BreathFramesRemaining > 0 && move.DrawsSword
                ? CombatTuning.BreathDamageMultiplier : 1
        };
        fighter.CurrentMove = id;
        fighter.Action = FighterAction.Attack;
        fighter.MoveFrame = 0;
        fighter.AttackHasHit = false;
        fighter.IsGuarding = false;
        fighter.Crouching = false;
        if (move.DrawsSword) fighter.SwordState = SwordState.Drawn;
        move.Start(fighter);
        return Report(fighter, MoveRequestResult.Started);
    }

    public static CombatMove? Active(Fighter fighter) =>
        fighter.Action == FighterAction.Attack && MoveRegistry.Default.TryGet(fighter.CurrentMove, out var move)
            ? move.Move : null;

    public static void ProcessInput(Fighter fighter, FrameInput input, int frame)
    {
        fighter.InputFrame = frame;
        fighter.Commands.Sample(frame, input);
        var active = Active(fighter);
        var bufferFrames = active is KanzenKaihiMove && CombatTuning.EvadeAssistEnabled
            ? Math.Max(CombatTuning.EvadePrebufferFrames, CombatTuning.BufferFrames) : CombatTuning.BufferFrames;
        if (!fighter.Commands.TryPeek(frame, out var command, bufferFrames)) return;
        var execution = fighter.Execution;
        if (active is KanzenKaihiMove &&
            ((!CombatTuning.EvadeAssistEnabled && !execution!.EvadeSucceeded) ||
             (execution!.EvadeSucceeded && command.Frame < execution.EvadeSuccessInputFrame &&
              (!CombatTuning.EvadeAssistEnabled ||
               execution.EvadeSuccessInputFrame - command.Frame > CombatTuning.EvadePrebufferFrames))))
        {
            fighter.Commands.Consume();
            Report(fighter, MoveRequestResult.Busy);
            return;
        }
        if (active is not null && active.Continue(fighter, command.Input))
        {
            fighter.LastRequestedMoveId = fighter.CurrentMove;
            fighter.Commands.Consume();
            Report(fighter, MoveRequestResult.Continued);
            return;
        }
        if (!CommandDefinitions.TryResolve(fighter, command.Input, out var id))
        {
            fighter.LastRequestedMoveId = MoveIds.None;
            fighter.Commands.Consume();
            Report(fighter, MoveRequestResult.UndefinedCommand);
            return;
        }
        var result = Request(fighter, id);
        if (result != MoveRequestResult.Busy)
            fighter.Commands.Consume();
        if (result == MoveRequestResult.Started)
        {
            var x = command.Horizontal;
            var y = command.Vertical;
            var length = MathF.Sqrt(x * x + y * y);
            fighter.Execution!.DirectionX = length > 0 ? x / length : fighter.Facing;
            fighter.Execution.DirectionY = length > 0 ? y / length : 0;
        }
    }

    public static bool CanMove(Fighter fighter) =>
        fighter.Action is FighterAction.Idle or FighterAction.Walking or FighterAction.Crouching ||
        Active(fighter)?.CanCancelToMovement == true;

    public static void End(Fighter fighter)
    {
        fighter.Action = FighterAction.Idle;
        fighter.CurrentMove = MoveIds.None;
        fighter.Execution = null;
        fighter.MoveFrame = 0;
        fighter.AttackHasHit = false;
    }

    public static MoveRequestResult Report(Fighter fighter, MoveRequestResult result)
    {
        fighter.LastMoveResult = result;
        return result;
    }
}
