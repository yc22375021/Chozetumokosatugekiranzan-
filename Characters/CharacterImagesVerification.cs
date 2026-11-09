using System.Drawing;
using System.Drawing.Imaging;

namespace Chozetumokosatugekiranzan;

internal static class CharacterImagesVerification
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "fighter-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Image test failed: " + message);
            count++;
        }
        try
        {
            var store = new CharacterImageStore(Path.Combine(root, "saved"));
            var source = Path.Combine(root, "日本語の透過画像.png");
            using (var bitmap = new Bitmap(80, 120))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
                bitmap.Save(source, ImageFormat.Png);
            }
            Check(store.Load(1, CharacterImage.Portrait) is null, "missing is optional");
            store.Import(1, CharacterImage.Portrait, source);
            File.Delete(source);
            using (var loaded = new CharacterImageStore(store.Root).Load(1, CharacterImage.Portrait))
            {
                Check(loaded is { Width: 80, Height: 120 }, "restart and source deletion");
                Check(loaded!.GetPixel(0, 0).A == 128 && loaded.GetPixel(1, 1).A == 0, "alpha retained");
            }
            Check(store.Load(2, CharacterImage.Portrait) is null, "IDs isolated");
            var before = File.ReadAllBytes(store.PathFor(1, CharacterImage.Portrait));
            File.WriteAllText(source, "this is not an image");
            var rejected = false;
            try { store.Import(1, CharacterImage.Portrait, source); }
            catch (Exception e) when (CharacterImageStore.IsImageError(e)) { rejected = true; }
            Check(rejected, "corrupt rejected");
            Check(before.SequenceEqual(File.ReadAllBytes(store.PathFor(1, CharacterImage.Portrait))), "failed import preserves previous");
            var jpeg = Path.Combine(root, "portrait.JPG");
            using (var bitmap = new Bitmap(30, 20)) bitmap.Save(jpeg, ImageFormat.Jpeg);
            store.Import(1, CharacterImage.Icon, jpeg);
            using (var loaded = store.Load(1, CharacterImage.Icon)) Check(loaded is { Width: 30, Height: 20 }, "JPEG accepted");
            store.Import(1, CharacterImage.Portrait, jpeg);
            using (var loaded = store.Load(1, CharacterImage.Portrait)) Check(loaded is { Width: 30, Height: 20 }, "replacement");
            store.Import(2, CharacterImage.Alternate, jpeg);
            store.Remove(1, CharacterImage.Portrait);
            Check(store.Load(1, CharacterImage.Portrait) is null && File.Exists(jpeg), "removal preserves source");
            using (var loaded = store.Load(2, CharacterImage.Alternate)) Check(loaded is not null, "removal isolated");
            using (var bitmap = new Bitmap(4097, 1)) bitmap.Save(source, ImageFormat.Png);
            rejected = false;
            try { store.Import(1, CharacterImage.Battle, source); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "oversized rejected");
            Check(CharacterCatalog.All.Select(c => c.Id).Distinct().Count() == CharacterCatalog.All.Length, "unique IDs");
            Check(!CharacterCatalog.All.Single(c => c.Id == 2).Playable, "Mia not assigned existing combat");
            Console.WriteLine($"Character images: {count} checks passed.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
