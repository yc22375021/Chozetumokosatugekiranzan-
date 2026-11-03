using Microsoft.Xna.Framework.Input;

namespace Chozetumokosatugekiranzan;

internal static class InstructionsVerification
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Instructions self-test failed: {name}");
            }

            checks++;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"fighter-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Check(InstructionSettings.DefaultPath == Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Chozetumokosatugekiranzan", "settings.json"), "settings use LocalApplicationData");
            var settings = new InstructionSettings(path);
            Check(!settings.SkipInstructions && settings.Error is null, "missing settings default to showing help");
            var session = new GameSession(settings);
            void Update(params Keys[] keys) => session.Update(new KeyboardState(keys), default, true);
            void Tap(Keys key)
            {
                Update(key);
                Update();
            }

            void FinishRound()
            {
                session.Battle.Player.Health = 0;
                Update();
                Check(session.Battle.Phase == RoundPhase.Result, "round reaches result");
            }

            Check(session.InstructionsVisible && session.Battle.Phase == RoundPhase.Title &&
                  !settings.SkipInstructions, "startup shows unchecked instructions before fighting");
            for (var i = 0; i < 180; i++)
            {
                Update(GameData.Controls.Light, GameData.Controls.Jump);
            }

            Check(session.Battle.Frame == 0 && session.Battle.Phase == RoundPhase.Title,
                "startup instructions do not advance battle or accept fighting input");
            Update();
            Tap(GameData.Controls.Start);
            Check(!session.InstructionsVisible && session.Battle.Phase == RoundPhase.Fighting,
                "separate start begins battle");
            FinishRound();
            Tap(GameData.Controls.Start);
            Check(session.InstructionsVisible && session.Battle.Phase == RoundPhase.Result,
                "unchecked rematch shows instructions");
            Tap(GameData.Controls.ToggleInstructions);
            Check(settings.SkipInstructions && session.InstructionsVisible &&
                  session.Battle.Phase == RoundPhase.Result, "keyboard checkbox does not start round");
            Check(new InstructionSettings(path).SkipInstructions, "checked setting survives a new settings instance");
            Tap(GameData.Controls.Start);
            FinishRound();
            Tap(GameData.Controls.Start);
            Check(!session.InstructionsVisible && session.Battle.Phase == RoundPhase.Fighting,
                "checked rematch skips instructions");

            var restarted = new GameSession(new InstructionSettings(path));
            Check(restarted.Battle.Phase == RoundPhase.Fighting && !restarted.InstructionsVisible,
                "restart retains automatic help opt-out");
            var frame = session.Battle.Frame;
            Update(GameData.Controls.Help, GameData.Controls.Light, GameData.Controls.Jump);
            Check(session.InstructionsVisible && session.Battle.Frame == frame &&
                  session.Battle.Player.Action != FighterAction.Attack && session.Battle.Player.Grounded,
                "help opening consumes simultaneous attack and jump");
            Update(GameData.Controls.Start, GameData.Controls.Light, GameData.Controls.Jump);
            Check(session.InstructionsVisible, "held opening inputs cannot dismiss help");
            Update();

            session.Battle.Player.Action = FighterAction.Hitstun;
            session.Battle.Player.StunFramesRemaining = 12;
            Battle.TryStartMove(session.Battle.Cpu, MoveIds.ShinsokuBattoGekihatsu);
            var snapshot = (
                session.Battle.Frame, session.Battle.RemainingSeconds,
                session.Battle.Player.X, session.Battle.Player.Health, session.Battle.Player.Meter,
                session.Battle.Player.Altitude, session.Battle.Player.StunFramesRemaining,
                session.Battle.Cpu.X, session.Battle.Cpu.Health, session.Battle.Cpu.Meter,
                session.Battle.Cpu.MoveFrame, session.Battle.Cpu.Action);
            for (var i = 0; i < 180; i++)
            {
                Update(GameData.Controls.Right, GameData.Controls.Light, GameData.Controls.Jump);
            }

            Check(snapshot == (
                    session.Battle.Frame, session.Battle.RemainingSeconds,
                    session.Battle.Player.X, session.Battle.Player.Health, session.Battle.Player.Meter,
                    session.Battle.Player.Altitude, session.Battle.Player.StunFramesRemaining,
                    session.Battle.Cpu.X, session.Battle.Cpu.Health, session.Battle.Cpu.Meter,
                    session.Battle.Cpu.MoveFrame, session.Battle.Cpu.Action),
                "help freezes timer, both fighters, CPU, animation, stun, health and meter");
            Update();

            var checkbox = GameSession.CheckboxBounds;
            var click = new MouseState(checkbox.Center.X, checkbox.Center.Y, 0,
                ButtonState.Pressed, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released);
            session.Update(default, click, true);
            Check(!settings.SkipInstructions && session.InstructionsVisible &&
                  !new InstructionSettings(path).SkipInstructions, "mouse can re-enable and save automatic help");
            session.Update(default, click, true);
            Check(!settings.SkipInstructions, "held mouse click toggles only once");
            Update();
            Tap(GameData.Controls.Help);
            Check(!session.InstructionsVisible && session.Battle.Frame == frame,
                "F1 closes manual help without ticking battle");

            Tap(GameData.Controls.Help);
            Update(GameData.Controls.Start, GameData.Controls.Light, GameData.Controls.Jump);
            Check(!session.InstructionsVisible && session.Battle.Frame == frame,
                "resume consumes simultaneous attack and jump");
            for (var i = 0; i < 5; i++)
            {
                Update(GameData.Controls.Light, GameData.Controls.Jump);
            }

            Check(session.Battle.Frame == frame, "resume waits for held controls to be released");
            Update();
            Update();
            Check(session.Battle.Frame == frame + 1 && session.Battle.Player.Grounded &&
                  session.Battle.Player.CurrentMove == MoveIds.None &&
                  session.Battle.Player.Commands.Count == 0, "resume does not leak attack, jump or history");
            FinishRound();
            Tap(GameData.Controls.Start);
            Check(session.InstructionsVisible, "re-enabled automatic help appears on next rematch");
            var button = GameSession.ContinueBounds;
            var startClick = new MouseState(button.Center.X, button.Center.Y, 0,
                ButtonState.Pressed, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released);
            session.Update(default, startClick, true);
            Check(!session.InstructionsVisible && session.Battle.Phase == RoundPhase.Fighting,
                "mouse start button starts battle separately");
            session.Update(default, startClick, true);
            Check(session.Battle.Frame == 0, "held start click is suppressed");
            Update();
            Tap(GameData.Controls.Help);
            session.Update(new KeyboardState(GameData.Controls.Start), default, false);
            session.Update(new KeyboardState(GameData.Controls.Start), default, true);
            Check(session.InstructionsVisible, "focus recovery cannot dismiss help with held input");
            Update();
            Tap(GameData.Controls.Start);

            var autoHelpRestart = new GameSession(new InstructionSettings(path));
            Check(autoHelpRestart.InstructionsVisible, "re-enabled automatic help survives restart");

            foreach (var invalid in new[] { "{broken", "null", "{}", "{\"SkipInstructions\":\"yes\"}", "[]" })
            {
                File.WriteAllText(path, invalid);
                var invalidSettings = new InstructionSettings(path);
                Check(!invalidSettings.SkipInstructions && invalidSettings.Error is not null,
                    $"invalid settings safely show help: {invalid}");
                var invalidSession = new GameSession(invalidSettings);
                Check(invalidSession.InstructionsVisible, "invalid settings do not prevent game startup");
            }

            var blockedPath = Path.Combine(directory, "blocked");
            Directory.CreateDirectory(blockedPath);
            var blocked = new InstructionSettings(blockedPath);
            blocked.SetSkipInstructions(true);
            Check(blocked.Error is not null, "write failure is explicitly reported without crashing");
            var conflictKeys = new[]
            {
                GameData.Controls.Left, GameData.Controls.Right, GameData.Controls.Jump,
                GameData.Controls.Down, GameData.Controls.Light, GameData.Controls.Heavy,
                GameData.Controls.Medium, GameData.Controls.Guard, GameData.Controls.DebugOverlay,
                GameData.Controls.Start, GameData.Controls.Quit, GameData.Controls.ToggleInstructions
            };
            Check(!conflictKeys.Contains(GameData.Controls.Help), "help key does not conflict with existing controls");
            Check(conflictKeys.Append(GameData.Controls.Help).Distinct().Count() == conflictKeys.Length + 1,
                "all bindings are distinct including new medium and guard");
            var inputSettings = new InstructionSettings(Path.Combine(directory, "controls.json"));
            inputSettings.SetSkipInstructions(true);
            var inputSession = new GameSession(inputSettings);
            void Input(params Keys[] keys) => inputSession.Update(new KeyboardState(keys), default, true);
            void Settle() { for (var i = 0; i < CombatTuning.ChordFrames; i++) Input(); }
            Input(GameData.Controls.Light, GameData.Controls.Medium);
            Settle();
            Check(inputSession.Battle.Player.CurrentMove == MoveIds.KanzenKaihi,
                "real keyboard weak plus medium starts evade, not single attacks");
            inputSession.Battle.StartRound();
            Input(GameData.Controls.Heavy);
            Settle();
            Check(inputSession.Battle.Player.CurrentMove == MoveIds.ShinsokuBattoGekihatsu &&
                  inputSession.Battle.Player.SwordState == SwordState.Drawn, "original heavy key preserved");
            inputSession.Battle.StartRound();
            inputSession.Battle.Player.SwordState = SwordState.Drawn;
            Input(GameData.Controls.Guard, Keys.Down);
            Check(inputSession.Battle.Player.CurrentMove == MoveIds.SpecialGuard &&
                  inputSession.Battle.Player.SwordState == SwordState.Sheathed, "guard key plus arrow down sheaths");
            Input();
            inputSession.Battle.StartRound();
            inputSession.Battle.Player.SwordState = SwordState.Drawn;
            Input(GameData.Controls.Medium, Keys.Up);
            Settle();
            Check(inputSession.Battle.Player.CurrentMove == MoveIds.Toppa &&
                  inputSession.Battle.Player.Execution?.DirectionY == 1 && inputSession.Battle.Player.Grounded,
                "keyboard up plus medium is directional thrust, not jump");
            Input(GameData.Controls.Help);
            var pausedFrame = inputSession.Battle.Frame;
            Input();
            Input(GameData.Controls.Start, GameData.Controls.Medium, GameData.Controls.Guard, Keys.Right);
            for (var i = 0; i < 10; i++) Input(GameData.Controls.Medium, GameData.Controls.Guard, Keys.Right);
            Check(!inputSession.InstructionsVisible && inputSession.Battle.Frame == pausedFrame,
                "resume waits for new controls and arrows to be released");
            Input();
            Input();
            Check(inputSession.Battle.Frame == pausedFrame + 1 && inputSession.Battle.Player.Commands.Count == 0,
                "new controls do not leak through help or focus suppression");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Console.WriteLine($"Instructions self-test passed ({checks} checks).");
    }
}
