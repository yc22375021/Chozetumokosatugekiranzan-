namespace Chozetumokosatugekiranzan;

internal static class BattleVerification
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"Combat self-test failed: {name}");
            checks++;
        }
        void Rejects(Action action, string name)
        {
            var rejected = false;
            try { action(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, name);
        }
        static Fighter New(SwordState state = SwordState.Sheathed) => new("TEST", false, 400) { SwordState = state };
        var registry = MoveRegistry.Default;
        Check(registry.All.Count() == 13, "13 fixed registrations");
        Check(MoveIds.Shuto == 1001 && MoveIds.SpecialGuard == 1013, "fixed numeric IDs");
        foreach (var registration in registry.All)
        {
            Check(registry.TryGet(registration.Id, out var byId) &&
                  registry.TryGet(registration.Key, out var byKey) &&
                  ReferenceEquals(byId, byKey), $"same registration by ID and key: {registration.Key}");
        }
        Check(!registry.TryGet(-1, out _) && !registry.TryGet("missing", out _), "invalid lookup safe");
        var registrations = registry.All.ToArray();
        Rejects(() => _ = new MoveRegistry(registrations.Append(registrations[0])), "duplicate ID detected");
        Rejects(() => _ = new MoveRegistry(registrations.Append(registrations[0] with { Id = 9999 })), "duplicate key detected");
        Rejects(() => _ = new MoveRegistry([registrations.Single(r => r.Id == MoveIds.Shuto)]), "missing branch ID detected");
        Rejects(() => CommandDefinitions.Validate(new MoveRegistry([])), "missing command target ID detected");
        var reordered = new MoveRegistry(registrations.Reverse());
        Check(reordered.Get(MoveIds.Shuto).Key == "shuto", "registration order does not affect IDs");

        foreach (var state in Enum.GetValues<SwordState>())
        foreach (var buttons in new[] { (1, 2), (1, 4), (2, 4) })
        foreach (var reverse in new[] { false, true })
        for (var shift = 0; shift <= CombatTuning.ChordFrames; shift++)
        {
            var driver = new Driver(New(state));
            var first = reverse ? buttons.Item2 : buttons.Item1;
            var second = reverse ? buttons.Item1 : buttons.Item2;
            driver.Step(Buttons(shift == 0 ? first | second : first));
            for (var frame = 1; frame <= CombatTuning.ChordFrames; frame++)
            {
                if (frame < CombatTuning.ChordFrames)
                    Check(driver.Fighter.CurrentMove == MoveIds.None, "no single before chord resolution");
                driver.Step(frame == shift && shift > 0 ? Buttons(second) : default);
            }
            var chord = buttons switch { (1, 2) => AttackInput.LightMedium, (1, 4) => AttackInput.LightHeavy, _ => AttackInput.MediumHeavy };
            if (chord == AttackInput.MediumHeavy)
                Check(driver.Fighter.CurrentMove == MoveIds.None &&
                      driver.Fighter.LastMoveResult == MoveRequestResult.UndefinedCommand, "undefined M+H no fallback");
            else
            {
                CommandDefinitions.TryResolve(New(state), chord, out var expected);
                Check(driver.Fighter.CurrentMove == (registry.Get(expected).Move.Implemented ? expected : MoveIds.None),
                    $"chord {state} {chord} skew {shift} reverse {reverse}");
                Check(driver.Fighter.LastMoveResult == (registry.Get(expected).Move.Implemented
                    ? MoveRequestResult.Started : MoveRequestResult.Unimplemented), "unimplemented chord explicit");
            }
        }
        var triple = new Driver(New());
        triple.Press(Buttons(7));
        Check(triple.Fighter.CurrentMove == MoveIds.None &&
              triple.Fighter.LastMoveResult == MoveRequestResult.UndefinedCommand, "triple press no fallback");

        var chop = new Driver(New());
        chop.Press(Buttons(1));
        Check(chop.Fighter.CurrentMove == MoveIds.Shuto, "single weak");
        chop.Press(Buttons(2));
        Check(chop.Fighter.CurrentMove == MoveIds.KaitenKenbu &&
              chop.Fighter.Execution?.OriginId == MoveIds.Shuto &&
              chop.Fighter.SwordState == SwordState.Drawn, "weak then medium branches and draws");
        var dance = chop.Fighter;
        for (var stage = 2; stage <= 6; stage++)
        {
            while (dance.Execution!.FollowupQueued) chop.Step();
            chop.Press(Buttons(2));
            while (dance.Execution!.Stage < stage) chop.Step();
            Check(dance.CurrentMove == MoveIds.KaitenKenbu && dance.Execution.Stage == stage,
                $"dance maintains chain after draw: rotation {stage}");
        }
        Check(!registry.Get(MoveIds.KaitenKenbu).Move.Continue(dance, AttackInput.Medium), "dance cannot exceed six rotations");
        Check(dance.Execution!.OriginId == MoveIds.Shuto, "dance preserves originating move");
        chop.Press(Buttons(2));
        chop.Idle(5);
        Check(dance.CurrentMove == MoveIds.KaitenKenbu && dance.Execution!.Stage == 6,
            "actual extra input cannot start seventh rotation");
        var otherDance = new Driver(New());
        otherDance.Press(Buttons(1));
        otherDance.Press(Buttons(2));
        Check(otherDance.Fighter.Execution!.Stage == 1 && dance.Execution!.Stage == 6,
            "two fighters using same dance definition do not share rotation counts");

        var slash = new Driver(New(SwordState.Drawn));
        slash.Press(Buttons(1));
        slash.Press(Buttons(2));
        Check(slash.Fighter.CurrentMove == MoveIds.NikukotsuDanzai &&
              slash.Fighter.Execution?.OriginId == MoveIds.Zan, "drawn weak then medium armored branch");
        Check(registry.Get(MoveIds.NikukotsuDanzai).Move.SuperArmor, "armored slash armor flag");

        var saya = new Driver(New());
        saya.Press(Buttons(2));
        saya.Press(Buttons(2));
        while (saya.Fighter.Execution!.Stage < 2) saya.Step();
        Check(saya.Fighter.CurrentMove == MoveIds.SayaAte && saya.Fighter.SwordState == SwordState.Drawn,
            "scabbard spin is its own stage, not sword dance");
        Check(saya.Fighter.Execution!.OriginId == MoveIds.SayaAte, "scabbard origin preserved");
        saya.Idle(6);
        Check(saya.Fighter.Execution.Stage == 2, "one medium followup does not advance two scabbard stages");
        var other = new Driver(New());
        other.Press(Buttons(2));
        Check(other.Fighter.Execution!.Stage == 1 && saya.Fighter.Execution.Stage == 2, "two scabbard chains have separate stages");
        saya.Press(Buttons(2));
        while (saya.Fighter.Execution!.Stage < 3) saya.Step();
        Check(saya.Fighter.SwordState == SwordState.Sheathed &&
              saya.Fighter.CurrentMove == MoveIds.SayaAte, "scabbard final thrust sheaths");
        var finalStage = saya.Fighter.Execution.Stage;
        saya.Idle(6);
        Check(saya.Fighter.Execution!.Stage == finalStage, "one input is not consumed for multiple stages");

        var zan = new Driver(New(SwordState.Drawn));
        zan.Press(Buttons(1));
        for (var stage = 2; stage <= 4; stage++)
        {
            zan.Press(Buttons(1));
            while (zan.Fighter.Execution!.Stage < stage) zan.Step();
        }
        Check(zan.Fighter.Execution!.Stage == 4 &&
              !registry.Get(MoveIds.Zan).Move.Continue(zan.Fighter, AttackInput.Light), "slash initial plus three = four stages");

        var fighter = New();
        Check(Battle.TryStartMove(fighter, MoveIds.Shuto), "ID starts legal move");
        fighter.MoveFrame = 4;
        Check(!fighter.AttackHasHit && Battle.TryStartMove(fighter, MoveIds.Shuto) && fighter.MoveFrame == 0,
            "whiff chop cancels into itself");
        fighter.SwordState = SwordState.Drawn;
        Check(Battle.TryStartMove(fighter, MoveIds.SpecialGuard) && fighter.SwordState == SwordState.Sheathed &&
              fighter.Action == FighterAction.Attack, "whiff chop into special guard sheaths independently of recovery/hit");
        Check(!Battle.TryStartMove(fighter, MoveIds.Shuto), "special guard recovery not cleared by sword state change");
        var guard = new Driver(New(SwordState.Drawn));
        guard.Step(new(GuardHeld: true, Down: true));
        Check(guard.Fighter.SwordState == SwordState.Sheathed &&
              guard.Fighter.CurrentMove == MoveIds.SpecialGuard && guard.Fighter.MoveFrame == 1,
            "guard plus down sheaths on input without opponent");
        guard.Idle(20);
        guard.Fighter.SwordState = SwordState.Drawn;
        guard.Step(new(GuardHeld: true, Down: true));
        Check(guard.Fighter.SwordState == SwordState.Sheathed, "special guard accepts fresh chord");
        var heldGuard = new Driver(New(SwordState.Drawn));
        heldGuard.Step(new(GuardHeld: true, Down: true));
        for (var frame = 0; frame < 30; frame++) heldGuard.Step(new(GuardHeld: true, Down: true));
        Check(heldGuard.Fighter.CurrentMove == MoveIds.None, "holding special guard does not restart recovery");
        var stunned = New(SwordState.Drawn);
        stunned.Action = FighterAction.Hitstun;
        stunned.StunFramesRemaining = 20;
        Check(!Battle.TryStartMove(stunned, MoveIds.SpecialGuard) && stunned.SwordState == SwordState.Drawn,
            "special guard cannot escape hitstun");
        var illegal = New();
        Check(MoveController.Request(illegal, -1) == MoveRequestResult.UnknownId, "bad ID explicit result");
        Check(MoveController.Request(illegal, MoveIds.Senkoken) == MoveRequestResult.Unimplemented &&
              illegal.SwordState == SwordState.Sheathed && illegal.CurrentMove == MoveIds.None, "placeholder has no side effects");
        Check(MoveController.Request(illegal, MoveIds.NikukotsuDanzai) == MoveRequestResult.BranchOnly, "ID cannot bypass branch");
        Check(MoveController.Request(illegal, MoveIds.Zan) == MoveRequestResult.WrongState, "ID cannot bypass sword state");
        Check(MoveController.Request(illegal, "shinsoku_batto_gekihatsu") == MoveRequestResult.Started &&
              illegal.SwordState == SwordState.Drawn && illegal.MoveFrame == 0, "heavy draws at accepted start by key");
        Check(MoveController.Request(illegal, MoveIds.SpecialGuard) == MoveRequestResult.Busy, "ID cannot bypass recovery");

        var buffered = new Driver(New());
        buffered.Fighter.Action = FighterAction.Hitstun;
        buffered.Fighter.StunFramesRemaining = 5;
        buffered.Press(Buttons(1));
        buffered.Idle(3);
        Check(buffered.Fighter.CurrentMove == MoveIds.Shuto, "attack buffer starts once recovery ends");
        var expired = new Driver(New());
        expired.Fighter.Action = FighterAction.Hitstun;
        expired.Fighter.StunFramesRemaining = 20;
        expired.Press(Buttons(1));
        expired.Idle(24);
        Check(expired.Fighter.CurrentMove == MoveIds.None, "expired buffer does not leak");
        var customChord = CombatTuning.ChordFrames;
        try
        {
            CombatTuning.ChordFrames = 1;
            var fast = new Driver(New());
            fast.Step(Buttons(1));
            fast.Step(Buttons(2));
            Check(fast.Fighter.CurrentMove == MoveIds.KanzenKaihi, "chord tolerance configurable");
        }
        finally { CombatTuning.ChordFrames = customChord; }

        var breath = new Driver(New());
        breath.Press(Buttons(5));
        Check(breath.Fighter.BreathFramesRemaining == CombatTuning.BreathFrames - 1, "breath counts initial simulation frame");
        breath.Idle(24);
        MoveController.Request(breath.Fighter, MoveIds.ShinsokuBattoGekihatsu);
        Check(breath.Fighter.Execution?.DamageMultiplier == CombatTuning.BreathDamageMultiplier,
            "buff applies to sword-drawing attack");
        breath.Idle(180);
        Check(breath.Fighter.BreathFramesRemaining == 0, "buff expires");
        var duration = New();
        Battle.TryStartMove(duration, MoveIds.Shinkokyu);
        for (var frame = 0; frame < CombatTuning.BreathFrames - 1; frame++) Battle.AdvanceAction(duration);
        Check(duration.BreathFramesRemaining == 1, "buff remains through first 179 frames");
        Battle.AdvanceAction(duration);
        Check(duration.BreathFramesRemaining == 0, "buff expires at exactly 180 frames");
        var nonDraw = New(SwordState.Drawn);
        nonDraw.BreathFramesRemaining = 30;
        Battle.TryStartMove(nonDraw, MoveIds.Zan);
        Check(nonDraw.Execution?.DamageMultiplier == 1, "buff does not boost attacks that do not draw");

        foreach (var direction in new[] { (-1, 0), (1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (0, 0) })
        {
            var thrust = new Driver(New(SwordState.Drawn));
            thrust.Press(new(Horizontal: direction.Item1, UpHeld: direction.Item2 > 0, Down: direction.Item2 < 0, MediumPressed: true));
            var execution = thrust.Fighter.Execution!;
            var length = MathF.Sqrt(execution.DirectionX * execution.DirectionX + execution.DirectionY * execution.DirectionY);
            Check(Math.Abs(length - 1) < .001f, "thrust normalized, neutral uses facing");
            if (direction.Item2 != 0) Check(Math.Sign(execution.DirectionY) == direction.Item2, "thrust vertical direction");
        }

        Battle Contact(int attack, int defense = MoveIds.None)
        {
            var battle = new Battle();
            battle.StartRound();
            battle.Player.X = 400;
            battle.Cpu.X = 450;
            if (attack == MoveIds.NikukotsuDanzai)
            {
                battle.Player.SwordState = SwordState.Drawn;
                Battle.TryStartMove(battle.Player, MoveIds.Zan);
            }
            if (defense == MoveIds.NikukotsuDanzai)
            {
                battle.Cpu.SwordState = SwordState.Drawn;
                Battle.TryStartMove(battle.Cpu, MoveIds.Zan);
            }
            Battle.TryStartMove(battle.Player, attack);
            battle.Player.MoveFrame = registry.Get(attack).Move.Performance.StartupFrames;
            if (defense != MoveIds.None) Battle.TryStartMove(battle.Cpu, defense);
            return battle;
        }
        var guarded = Contact(MoveIds.Shuto, MoveIds.SpecialGuard);
        guarded.Cpu.IsGuarding = true;
        guarded.ResolveAttacks();
        Check(guarded.Cpu.Health == GameData.MaximumHealth &&
              guarded.Cpu.Action == FighterAction.Guardstun, "prototype special guard blocks without damage");
        var hit = Contact(MoveIds.Shuto);
        hit.ResolveAttacks();
        var health = hit.Cpu.Health;
        hit.ResolveAttacks();
        Check(health == GameData.MaximumHealth - registry.Get(MoveIds.Shuto).Move.Performance.Damage &&
              hit.Cpu.Health == health, "single hit per swing");
        var trade = Contact(MoveIds.Shuto, MoveIds.Shuto);
        trade.Cpu.MoveFrame = 2;
        trade.ResolveAttacks();
        Check(trade.Player.Health < GameData.MaximumHealth && trade.Cpu.Health < GameData.MaximumHealth,
            "simultaneous hits preserved");
        var armor = Contact(MoveIds.Shuto, MoveIds.NikukotsuDanzai);
        armor.ResolveAttacks();
        Check(armor.Cpu.Health < GameData.MaximumHealth && armor.Cpu.CurrentMove == MoveIds.NikukotsuDanzai &&
              armor.Cpu.Action == FighterAction.Attack, "armor takes damage but prevents interruption");
        var dodge = Contact(MoveIds.Shuto, MoveIds.KanzenKaihi);
        dodge.Cpu.MoveFrame = 3;
        dodge.ResolveAttacks();
        Check(dodge.Cpu.Health == GameData.MaximumHealth && dodge.Cpu.Execution!.EvadeSucceeded,
            "overlapping live attack in success window detects evade separately from invincibility");
        Check(MoveController.Request(dodge.Cpu, MoveIds.NikukotsuDanzai) == MoveRequestResult.Started,
            "successful evade may cancel to any implemented attack");
        Check(dodge.Cpu.Execution!.OriginId == MoveIds.KanzenKaihi, "evade attack preserves chain origin");
        var late = Contact(MoveIds.Shuto, MoveIds.KanzenKaihi);
        late.Cpu.MoveFrame = CombatTuning.EvadeSuccessFrames + 1;
        late.ResolveAttacks();
        Check(late.Cpu.Health == GameData.MaximumHealth && !late.Cpu.Execution!.EvadeSucceeded,
            "invincibility outside success window is not evade success");
        Check(!Battle.TryStartMove(late.Cpu, MoveIds.Shuto), "failed evade cannot attack cancel");
        var emptyDodge = new Driver(New());
        emptyDodge.Press(Buttons(3));
        emptyDodge.Press(Buttons(1));
        Check(emptyDodge.Fighter.CurrentMove == MoveIds.KanzenKaihi && !emptyDodge.Fighter.Execution!.EvadeSucceeded,
            "no threat means no success-only followup");
        var early = Contact(MoveIds.Shuto, MoveIds.KanzenKaihi);
        early.Player.MoveFrame = 1;
        early.Cpu.MoveFrame = 3;
        early.ResolveAttacks();
        Check(!early.Cpu.Execution!.EvadeSucceeded, "startup-only overlap is not evade success");
        var counter = new Battle();
        counter.StartRound();
        counter.Player.X = 400;
        counter.Cpu.X = 570;
        counter.Player.SwordState = SwordState.Drawn;
        Battle.TryStartMove(counter.Player, MoveIds.KatsujinSoha);
        Battle.TryStartMove(counter.Cpu, MoveIds.Shuto);
        counter.Player.MoveFrame = registry.Get(MoveIds.KatsujinSoha).Move.Performance.StartupFrames;
        counter.Cpu.MoveFrame = registry.Get(MoveIds.Shuto).Move.Performance.StartupFrames;
        counter.ResolveAttacks();
        Check(counter.Player.CounterTipContact && counter.Player.Health == GameData.MaximumHealth &&
              counter.Cpu.Health == GameData.MaximumHealth, "tip contact detection has no invented counter reward");

        var assist = CombatTuning.EvadeAssistEnabled;
        try
        {
            CombatTuning.EvadeAssistEnabled = true;
            var assisted = Contact(MoveIds.Shuto, MoveIds.KanzenKaihi);
            var driver = new Driver(assisted.Cpu);
            driver.Step(Buttons(1));
            assisted.Cpu.MoveFrame = 3;
            assisted.ResolveAttacks();
            Check(assisted.Cpu.EvadeStopRemaining == CombatTuning.EvadeStopFrames, "optional success stop");
            driver.Idle(CombatTuning.ChordFrames);
            Check(assisted.Cpu.CurrentMove == MoveIds.Shuto, "optional pre-success input becomes attack only after success");
            var failed = new Driver(New());
            failed.Press(Buttons(3));
            failed.Press(Buttons(1));
            failed.Idle(12);
            Check(failed.Fighter.CurrentMove == MoveIds.KanzenKaihi, "assist never authorizes failed evade");
            var stopped = Contact(MoveIds.Shuto, MoveIds.KanzenKaihi);
            stopped.Cpu.MoveFrame = 3;
            stopped.ResolveAttacks();
            stopped.Player.BreathFramesRemaining = 60;
            var remaining = stopped.RemainingFrames;
            var position = stopped.Player.X;
            stopped.Tick(Buttons(1));
            for (var frame = 1; frame < CombatTuning.EvadeStopFrames; frame++) stopped.Tick(default);
            Check(stopped.RemainingFrames == remaining && stopped.Player.X == position &&
                  stopped.Player.BreathFramesRemaining == 60, "assist stop freezes timer, movement and buffs");
            Check(stopped.Player.CurrentMove == MoveIds.Shuto && stopped.Player.MoveFrame == 0,
                "attack input accepted during stop without advancing new action");
        }
        finally { CombatTuning.EvadeAssistEnabled = assist; }

        var mirrorRight = New();
        var mirrorLeft = New();
        mirrorLeft.Facing = -1;
        Battle.TryStartMove(mirrorRight, MoveIds.Shuto);
        Battle.TryStartMove(mirrorLeft, MoveIds.Shuto);
        Check(Battle.GetAttackBox(mirrorRight).X > mirrorRight.X && Battle.GetAttackBox(mirrorLeft).Right < mirrorLeft.X,
            "attack boxes mirror with facing");
        var movement = new Battle();
        movement.StartRound();
        movement.Player.X = GameData.StageLeft + GameData.FighterWidth / 2f;
        for (var frame = 0; frame < 5; frame++) movement.Tick(new(Horizontal: -1));
        Check(movement.Player.X >= GameData.StageLeft + GameData.FighterWidth / 2f, "stage boundary");
        movement.Tick(new(JumpPressed: true));
        for (var frame = 0; frame < 90; frame++) movement.Tick(default);
        Check(movement.Player.Grounded && movement.Player.Altitude == 0, "jump landing");
        var movingChop = new Battle();
        movingChop.StartRound();
        for (var frame = 0; frame <= CombatTuning.ChordFrames; frame++)
            movingChop.Tick(new(Horizontal: 1, LightPressed: frame == 0));
        Check(movingChop.Player.CurrentMove == MoveIds.Shuto, "held movement does not erase new chop");
        movingChop.Tick(new(Horizontal: 1, JumpPressed: true));
        Check(!movingChop.Player.Grounded && movingChop.Player.CurrentMove == MoveIds.None, "whiff chop jump cancel");
        var guardCancel = new Battle();
        guardCancel.StartRound();
        Battle.TryStartMove(guardCancel.Player, MoveIds.Shuto);
        guardCancel.Tick(default);
        guardCancel.Tick(Buttons(1));
        guardCancel.Tick(new(GuardHeld: true));
        Check(guardCancel.Player.IsGuarding && guardCancel.Player.CurrentMove == MoveIds.None,
            "normal guard cancels whiff chop and clears unresolved attack input");
        var upward = new Battle();
        upward.StartRound();
        upward.Player.SwordState = SwordState.Drawn;
        upward.Tick(new(UpHeld: true, JumpPressed: true, MediumPressed: true));
        for (var frame = 0; frame < CombatTuning.ChordFrames; frame++) upward.Tick(new(UpHeld: true));
        Check(upward.Player.Grounded && upward.Player.CurrentMove == MoveIds.Toppa &&
              upward.Player.Execution!.DirectionY == 1, "up plus medium thrust takes priority over jump");
        var walking = new Battle();
        walking.StartRound();
        walking.Player.BreathFramesRemaining = 60;
        var startX = walking.Player.X;
        walking.Tick(new(Horizontal: 1));
        Check(Math.Abs(walking.Player.X - startX - GameData.WalkSpeed * CombatTuning.BreathSpeedMultiplier) < .001f,
            "breath boosts walking speed");
        var l = New();
        var r = New();
        r.X += 10;
        Battle.ResolvePushApart(l, r);
        Check(r.X - l.X >= GameData.FighterWidth - .01f, "push apart");
        var air = New();
        air.Altitude = GameData.StandingHeight + 1;
        air.Grounded = false;
        Battle.ResolvePushApart(l, air);
        Check(air.X == 400, "no vertical push collision");
        l.X = GameData.StageRight - GameData.FighterWidth / 2f;
        r.X = l.X - 10;
        Battle.ResolvePushApart(l, r);
        Check(Math.Abs(r.X - l.X) >= GameData.FighterWidth - .01f &&
              l.X <= GameData.StageRight - GameData.FighterWidth / 2f, "push apart at wall");
        var knockout = Contact(MoveIds.Shuto, MoveIds.Shuto);
        knockout.Player.Health = 1;
        knockout.Cpu.Health = 1;
        knockout.Cpu.MoveFrame = 2;
        knockout.ResolveAttacks();
        knockout.Tick(default);
        Check(knockout.Phase == RoundPhase.Result && knockout.Result == RoundResult.Draw, "simultaneous knockout");
        var match = new Battle();
        match.StartRound();
        for (var frame = 0; frame < GameData.MatchTimeSeconds * GameData.FramesPerSecond &&
             match.Phase == RoundPhase.Fighting; frame++) match.Tick(default);
        Check(match.Phase == RoundPhase.Result, "CPU completes a match");
        match.StartRound();
        Check(match.Player.SwordState == SwordState.Sheathed && match.Player.Execution is null &&
              match.Player.Commands.Count == 0 && match.Player.Health == GameData.MaximumHealth, "round reset");
        Console.WriteLine($"Combat self-test passed ({checks} checks).");
    }

    private static FrameInput Buttons(int mask) => new(LightPressed: (mask & 1) != 0,
        MediumPressed: (mask & 2) != 0, HeavyPressed: (mask & 4) != 0);

    private sealed class Driver(Fighter fighter)
    {
        public Fighter Fighter { get; } = fighter;
        private int _frame;
        public void Step(FrameInput input = default)
        {
            Fighter.PreviousInput = Fighter.CurrentInput;
            Fighter.CurrentInput = input;
            MoveController.ProcessInput(Fighter, input, ++_frame);
            Battle.AdvanceAction(Fighter);
        }
        public void Idle(int count) { for (var i = 0; i < count; i++) Step(); }
        public void Press(FrameInput input)
        {
            Step(input);
            Idle(CombatTuning.ChordFrames);
        }
    }
}
