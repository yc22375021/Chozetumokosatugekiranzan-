namespace Chozetumokosatugekiranzan;

internal static class MoveIds
{
    public const int None = 0;
    public const int Shuto = 1001;
    public const int SayaAte = 1002;
    public const int ShinsokuBattoGekihatsu = 1003;
    public const int KanzenKaihi = 1004;
    public const int Shinkokyu = 1005;
    public const int KaitenKenbu = 1006;
    public const int Zan = 1007;
    public const int Toppa = 1008;
    public const int Senkoken = 1009;
    public const int TaikuToppa = 1010;
    public const int KatsujinSoha = 1011;
    public const int NikukotsuDanzai = 1012;
    public const int SpecialGuard = 1013;
}

internal sealed record MoveRegistration(int Id, string Key, string Name, string DisplayName, CombatMove Move);

internal sealed class MoveRegistry
{
    public static MoveRegistry Default { get; } = new(
    [
        new(MoveIds.Shuto, "shuto", "手刀", "HAND CHOP", new ShutoMove()),
        new(MoveIds.SayaAte, "saya_ate", "鞘当て", "SCABBARD THRUST", new SayaAteMove()),
        new(MoveIds.ShinsokuBattoGekihatsu, "shinsoku_batto_gekihatsu", "神速抜刀　撃発", "QUICK DRAW", new ShinsokuBattoGekihatsuMove()),
        new(MoveIds.KanzenKaihi, "kanzen_kaihi", "完全回避", "PERFECT EVADE", new KanzenKaihiMove()),
        new(MoveIds.Shinkokyu, "shinkokyu", "深呼吸", "DEEP BREATH", new ShinkokyuMove()),
        new(MoveIds.KaitenKenbu, "kaiten_kenbu", "撃発剣術式　回転剣舞", "ROTATING SWORD DANCE", new KaitenKenbuMove()),
        new(MoveIds.Zan, "zan", "撃発剣術式　斬", "SLASH", new ZanMove()),
        new(MoveIds.Toppa, "toppa", "撃発剣術式　突破", "DIRECTIONAL THRUST", new ToppaMove()),
        new(MoveIds.Senkoken, "senkoken", "撃発剣術式　閃光剣", "FLASH SWORD", new SenkokenMove()),
        new(MoveIds.TaikuToppa, "taiku_toppa", "撃発剣術式　滞空突破", "AIR THRUST", new TaikuToppaMove()),
        new(MoveIds.KatsujinSoha, "katsujin_soha", "撃発剣術式　滑刃走破", "BLADE RUSH", new KatsujinSohaMove()),
        new(MoveIds.NikukotsuDanzai, "nikukotsu_danzai", "撃発剣術式　肉骨断罪", "ARMORED SLASH", new NikukotsuDanzaiMove()),
        new(MoveIds.SpecialGuard, "special_guard", "特殊ガード・納刀", "SPECIAL GUARD", new SpecialGuardMove())
    ]);

    private readonly Dictionary<int, MoveRegistration> _byId = [];
    private readonly Dictionary<string, MoveRegistration> _byKey = new(StringComparer.Ordinal);
    public IEnumerable<MoveRegistration> All => _byId.Values;

    public MoveRegistry(IEnumerable<MoveRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            if (registration.Id <= 0 || !_byId.TryAdd(registration.Id, registration))
                throw new InvalidOperationException($"Invalid or duplicate MoveId: {registration.Id}");
            if (string.IsNullOrWhiteSpace(registration.Key) || !_byKey.TryAdd(registration.Key, registration))
                throw new InvalidOperationException($"Invalid or duplicate MoveKey: {registration.Key}");
        }

        foreach (var registration in All)
        foreach (var target in registration.Move.BranchTargets)
            if (!_byId.ContainsKey(target))
                throw new InvalidOperationException($"Missing branch target {target} in {registration.Key}");
    }

    public bool TryGet(int id, out MoveRegistration registration) => _byId.TryGetValue(id, out registration!);
    public bool TryGet(string key, out MoveRegistration registration) => _byKey.TryGetValue(key, out registration!);
    public MoveRegistration Get(int id) => _byId[id];
}
