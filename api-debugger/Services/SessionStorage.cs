using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Handles saving and loading recorded sessions to/from JSON files.
/// </summary>
public class SessionStorage
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc
    };

    /// <summary>
    /// Gets the default folder for storing session files.
    /// </summary>
    public static string GetDefaultSessionsFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "MusicBeeRemote", "Sessions");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        return folder;
    }

    /// <summary>
    /// Saves a session to a JSON file.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="filePath">The file path to save to.</param>
    public static Task SaveSessionAsync(RecordedSession session, string filePath)
    {
        var json = JsonConvert.SerializeObject(session, JsonSettings);
        return File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a session from a JSON file.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>The loaded session.</returns>
    public static async Task<RecordedSession> LoadSessionAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var session = JsonConvert.DeserializeObject<RecordedSession>(json, JsonSettings);
        return session ?? throw new InvalidDataException("Failed to deserialize session file");
    }

    /// <summary>
    /// Gets a list of available session files in the default folder.
    /// </summary>
    /// <returns>List of file paths.</returns>
    public static List<string> GetAvailableSessionFiles()
    {
        var folder = GetDefaultSessionsFolder();
        if (!Directory.Exists(folder))
        {
            return new List<string>();
        }

        return Directory.GetFiles(folder, "*.json")
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();
    }

    /// <summary>
    /// Generates a default filename for a session.
    /// </summary>
    /// <param name="session">The session to generate a filename for.</param>
    /// <returns>A suggested filename (without path).</returns>
    public static string GenerateFilename(RecordedSession session)
    {
        var safeName = string.Join("_", session.Name.Split(Path.GetInvalidFileNameChars()));
        var timestamp = session.RecordedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{safeName}_{timestamp}.json";
    }
}
