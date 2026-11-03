namespace Chozetumokosatugekiranzan;

internal readonly record struct AttackCommand(AttackInput Input, int Frame, int Horizontal, int Vertical);

internal sealed class CommandInput
{
    private int _pendingMask;
    private int _pendingFrame;
    private int _horizontal;
    private int _vertical;
    private bool _specialHeld;
    private readonly Queue<AttackCommand> _commands = new();
    public int Count => _commands.Count + (_pendingMask != 0 ? 1 : 0);

    public void Sample(int frame, FrameInput input)
    {
        var special = input.GuardHeld && input.Down;
        if (special && !_specialHeld)
        {
            _pendingMask = 0;
            _commands.Clear();
            _commands.Enqueue(new(AttackInput.SpecialGuard, frame, input.Horizontal, -1));
        }
        _specialHeld = special;
        if (special) return;
        if (input.GuardHeld)
        {
            _pendingMask = 0;
            _commands.Clear();
            return;
        }

        var mask = (input.LightPressed ? 1 : 0) | (input.MediumPressed ? 2 : 0) |
                   (input.HeavyPressed ? 4 : 0);
        if (_pendingMask != 0 && frame - _pendingFrame > CombatTuning.ChordFrames)
            Flush();
        if (mask != 0)
        {
            // A repeated edge is a new press, never a second consumption of the first.
            if ((_pendingMask & mask) != 0) Flush();
            if (_pendingMask == 0)
            {
                _pendingFrame = frame;
                _horizontal = input.Horizontal;
                _vertical = input.UpHeld ? 1 : input.Down ? -1 : 0;
            }
            _pendingMask |= mask;
        }
        if (_pendingMask != 0 && frame - _pendingFrame >= CombatTuning.ChordFrames)
            Flush();
    }

    private void Flush()
    {
        var input = _pendingMask switch
        {
            1 => AttackInput.Light, 2 => AttackInput.Medium, 4 => AttackInput.Heavy,
            3 => AttackInput.LightMedium, 5 => AttackInput.LightHeavy,
            _ => AttackInput.MediumHeavy
        };
        _commands.Enqueue(new(input, _pendingFrame, _horizontal, _vertical));
        _pendingMask = 0;
    }

    public bool TryPeek(int frame, out AttackCommand command, int? bufferFrames = null)
    {
        while (_commands.TryPeek(out var old) && frame - old.Frame > (bufferFrames ?? CombatTuning.BufferFrames))
            _commands.Dequeue();
        return _commands.TryPeek(out command);
    }

    public void Consume() => _commands.Dequeue();
    public void Clear()
    {
        _commands.Clear();
        _pendingMask = 0;
        _specialHeld = false;
    }
}

internal static class CommandDefinitions
{
    public static void Validate(MoveRegistry registry)
    {
        foreach (var id in Bindings.Values.Concat(Branches.Values).Concat(Branches.Keys.Select(key => key.Item1)))
            if (!registry.TryGet(id, out _))
                throw new InvalidOperationException($"Unknown command MoveId: {id}");
        foreach (var branch in Branches)
            if (!registry.Get(branch.Key.Item1).Move.BranchTargets.Contains(branch.Value))
                throw new InvalidOperationException($"Unregistered branch: {branch.Key.Item1} -> {branch.Value}");
    }
    private static readonly IReadOnlyDictionary<(SwordState, AttackInput), int> Bindings =
        new Dictionary<(SwordState, AttackInput), int>
        {
            [(SwordState.Sheathed, AttackInput.Light)] = MoveIds.Shuto,
            [(SwordState.Sheathed, AttackInput.Medium)] = MoveIds.SayaAte,
            [(SwordState.Sheathed, AttackInput.Heavy)] = MoveIds.ShinsokuBattoGekihatsu,
            [(SwordState.Sheathed, AttackInput.LightMedium)] = MoveIds.KanzenKaihi,
            [(SwordState.Sheathed, AttackInput.LightHeavy)] = MoveIds.Shinkokyu,
            [(SwordState.Drawn, AttackInput.Light)] = MoveIds.Zan,
            [(SwordState.Drawn, AttackInput.Medium)] = MoveIds.Toppa,
            [(SwordState.Drawn, AttackInput.Heavy)] = MoveIds.Senkoken,
            [(SwordState.Drawn, AttackInput.LightMedium)] = MoveIds.TaikuToppa,
            [(SwordState.Drawn, AttackInput.LightHeavy)] = MoveIds.KatsujinSoha,
            [(SwordState.Sheathed, AttackInput.SpecialGuard)] = MoveIds.SpecialGuard,
            [(SwordState.Drawn, AttackInput.SpecialGuard)] = MoveIds.SpecialGuard
        };

    private static readonly IReadOnlyDictionary<(int, AttackInput), int> Branches =
        new Dictionary<(int, AttackInput), int>
        {
            [(MoveIds.Shuto, AttackInput.Medium)] = MoveIds.KaitenKenbu,
            [(MoveIds.Zan, AttackInput.Medium)] = MoveIds.NikukotsuDanzai
        };

    public static bool TryResolve(Fighter fighter, AttackInput input, out int id)
    {
        if (fighter.Action == FighterAction.Attack && Branches.TryGetValue((fighter.CurrentMove, input), out id))
            return true;
        return Bindings.TryGetValue((fighter.SwordState, input), out id);
    }
}
