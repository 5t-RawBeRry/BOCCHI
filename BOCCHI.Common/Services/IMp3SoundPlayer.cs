namespace BOCCHI.Common.Services;

/// <summary>Plays MP3s from the plugin <c>Sounds</c> folder (Saucy-style).</summary>
public interface IMp3SoundPlayer
{
    string SoundsDirectory { get; }

    /// <summary>Sound names (filename without extension) for <c>*.mp3</c> in the Sounds folder.</summary>
    IReadOnlyList<string> ListSounds();

    void Play(string soundName);

    void OpenSoundsFolder();
}
