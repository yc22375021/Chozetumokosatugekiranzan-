using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Chozetumokosatugekiranzan;

internal sealed record CharacterDefinition(int Id, string Name, string Description, bool Playable, string[] Poses);
internal enum CharacterImage { Portrait, Alternate, Icon, Battle }

internal static class CharacterCatalog
{
    // IDs are permanent. Mia is a separate character, not a skin for the existing swordsman.
    public static readonly CharacterDefinition[] All =
    [
        new(1, "抜刀剣士（仮）", "納刀と抜刀を切り替えて戦う剣士。\n強攻撃で抜刀、ガード＋下で納刀。", true, ["納刀", "抜刀"]),
        new(2, "ミア", "三振りの刀剣を携え、二刀で加速し続ける使い手。\nかつて『猛獣』と呼ばれ、今は過去の不始末の責任を負わされ『ダメ』と呼ばれる。\n三振り目はガンブレード型レイピア。弾丸代がかかるため、本気で処刑する時だけ使う。", false, ["立ち姿", "別ポーズ"])
    ];
}

/// <summary>Imports owned copies; never holds a lock on the user's source image.</summary>
internal sealed class CharacterImageStore
{
    public string Root { get; }
    public CharacterImageStore(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Chozetumokosatugekiranzan", "Characters");
        if (!Path.IsPathFullyQualified(Root)) throw new IOException("画像保存先を取得できません。");
    }
    public string PathFor(int id, CharacterImage role)
    {
        if (!CharacterCatalog.All.Any(c => c.Id == id)) throw new ArgumentOutOfRangeException(nameof(id));
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        return Path.Combine(Root, id.ToString("D4"), role.ToString().ToLowerInvariant() + ".png");
    }
    public Bitmap? Load(int id, CharacterImage role)
    {
        var path = PathFor(id, role);
        if (!File.Exists(path)) return null;
        using var image = Image.FromFile(path);
        ValidateDimensions(image);
        return new Bitmap(image);
    }
    public void Import(int id, CharacterImage role, string source)
    {
        var extension = Path.GetExtension(source);
        if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("PNG または JPEG を選択してください。");
        if (new FileInfo(source).Length > 32 * 1024 * 1024)
            throw new InvalidDataException("画像は32MB以下にしてください。");
        // Decode and validate before touching a previously imported image.
        using var stream = File.OpenRead(source);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        if (image.RawFormat.Guid != ImageFormat.Png.Guid && image.RawFormat.Guid != ImageFormat.Jpeg.Guid)
            throw new InvalidDataException("画像の内容がPNG / JPEGではありません。");
        ValidateDimensions(image);
        using var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(copy))
        {
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        }
        var target = PathFor(id, role);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            copy.Save(temporary, ImageFormat.Png);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
    public void Remove(int id, CharacterImage role) => File.Delete(PathFor(id, role));
    private static void ValidateDimensions(Image image)
    {
        if (image.Width > 4096 || image.Height > 4096 || (long)image.Width * image.Height > 16_000_000)
            throw new InvalidDataException("画像は各辺4096px以下・1600万画素以下にしてください。");
    }
    public static bool IsImageError(Exception e) => e is IOException or UnauthorizedAccessException or
        ArgumentException or ExternalException or OutOfMemoryException or NotSupportedException or
        System.Security.SecurityException;
}
