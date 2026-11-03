using System.Text.Json;

namespace Chozetumokosatugekiranzan;

internal sealed class InstructionSettings
{
    private sealed record Settings
    {
        public required bool SkipInstructions { get; init; }
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chozetumokosatugekiranzan", "settings.json");

    private readonly string _path;
    public bool SkipInstructions { get; private set; }
    public string? Error { get; private set; }

    public InstructionSettings(string? path = null)
    {
        _path = path ?? DefaultPath;
        try
        {
            EnsureAbsolutePath();
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path));
            if (settings is null)
            {
                throw new JsonException("Settings must be an object.");
            }

            SkipInstructions = settings.SkipInstructions;
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ReportError("SETTINGS UNREADABLE - SHOWING HELP", exception);
        }
    }

    public void SetSkipInstructions(bool skip)
    {
        SkipInstructions = skip;
        try
        {
            EnsureAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new Settings { SkipInstructions = skip }));
            File.Move(temporaryPath, _path, overwrite: true);
            Error = null;
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ReportError("SETTINGS NOT SAVED - CHANGE IS TEMPORARY", exception);
        }
    }

    private void EnsureAbsolutePath()
    {
        if (!Path.IsPathFullyQualified(_path))
        {
            throw new IOException("LocalApplicationData is unavailable; refusing a relative settings path.");
        }
    }

    private static bool IsSettingsError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or
            System.Security.SecurityException or NotSupportedException;

    private void ReportError(string message, Exception exception)
    {
        Error = message;
        Console.Error.WriteLine($"{message}: {exception.Message}");
    }
}
