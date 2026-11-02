using System;
using System.IO;
using System.Text.Json;

namespace GameReviews.Data;

/// <summary>
/// Строка подключения берётся из переменной окружения GAMEREVIEWS_CONNECTION
/// или из appsettings.json рядом с exe.
/// </summary>
public static class Config
{
    public static string ConnectionString { get; } = Load();

    private static string Load()
    {
        var env = Environment.GetEnvironmentVariable("GAMEREVIEWS_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("ConnectionString").GetString()
               ?? throw new InvalidOperationException("ConnectionString пуст в appsettings.json");
    }
}
