namespace Chozetumokosatugekiranzan;

internal static class JapaneseFont
{
    public const int CellSize = 16;
    public const int Columns = 16;
    public const string GlyphCharacters =
        "―←↑→↓　、。いかがきけしじすせたてでなにのはへまみめもゃらりるろをんアイウェカガキクコジスセタチッドパフプペマムャヤリルレン・ー一上不中了今体作保停先光入全再刀刃分切利前剣力功加効動勝化可同向吸呼回地基報変大始存完定実対専常引弱強弾当待後情態成戟戦手技抜押振接撃操攻斬断方明時更替最有未本果機欄次止殊段気求派深滑滞点特状生用画略発直省破硬示神秒移空突端納終継続罪義肉致舞行表被装要触設説読走起転込追通速避閃閉開間階面鞘骨（）／：";
    public static int MeasureTextWidth(string text, int scale) =>
        text.Sum(character => (char.IsAscii(character) ? 12 : CellSize) * scale);
}
