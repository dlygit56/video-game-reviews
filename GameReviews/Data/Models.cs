using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameReviews.Data;

public static class RatingFormat
{
    // Те же пять значков, что и на tijoe.net/game-reviews
    public static string Emoji(decimal score) => score switch
    {
        <= 0m => "▫️",
        >= 9m => "🔥",
        >= 7m => "👍",
        >= 5m => "🍺",
        >= 3m => "😐",
        _ => "👎"
    };

    public static string Text(decimal score) =>
        score <= 0m ? "—" : score.ToString("0.0", CultureInfo.InvariantCulture);
}

public record Genre(short Id, string Name);

public record AppUser(long Id, string Username, string Email, string RoleCode, DateTime CreatedAt)
{
    public bool IsCritic => RoleCode == "critic";
    public bool IsAdmin => RoleCode == "admin";
    public string RoleName => RoleCode switch
    {
        "admin" => "Администратор",
        "critic" => "Критик",
        _ => "Пользователь"
    };
}

public record GameItem(
    long Id, string Title, string? Description, DateTime? ReleaseDate,
    decimal RatingAvg, int ReviewsCount, string Genres)
{
    public string Emoji => RatingFormat.Emoji(RatingAvg);
    public string RatingText => RatingFormat.Text(RatingAvg);
    public string Year => ReleaseDate?.Year.ToString() ?? "";
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Year)) parts.Add(Year);
            if (!string.IsNullOrEmpty(Genres)) parts.Add(Genres);
            parts.Add($"рецензий: {ReviewsCount}");
            return string.Join(" · ", parts);
        }
    }
}

public record ReviewItem(
    long Id, long GameId, string GameTitle, long CriticId, string Critic,
    string? Title, string Body, short StatusId, string StatusName,
    short? Score, DateTime CreatedAt, int Views)
{
    public string Emoji => Score is null ? "▫️" : RatingFormat.Emoji(Score.Value);
    public string ScoreText => Score is null ? "—" : $"{Score}/10";
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"Рецензия на {GameTitle}" : Title!;
    public string Meta => $"{Critic} · {CreatedAt.ToLocalTime():dd.MM.yyyy} · просмотров: {Views}";
    public bool IsPublished => StatusId == 2;
    public bool ShowStatus => StatusId != 2;

    public bool CanEdit => Session.Current is { } u && u.Id == CriticId;
    public bool CanModerate => Session.Current?.IsAdmin == true;
    public bool CanDelete => CanEdit || CanModerate;
    public string ToggleStatusText => IsPublished ? "Скрыть" : "Опубликовать";
}

public record CriticStat(string Username, int ReviewsCount, decimal AvgScore)
{
    public string Emoji => RatingFormat.Emoji(AvgScore);
    public string AvgText => RatingFormat.Text(AvgScore);
}

public record ViewedReview(long ReviewId, string GameTitle, string? ReviewTitle, DateTime ViewedAt)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(ReviewTitle) ? $"Рецензия на {GameTitle}" : ReviewTitle!;
    public string When => ViewedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}

public record UserRow(long Id, string Username, string Email, string RoleCode, short RoleId, int ReviewsCount)
{
    public string RoleName => RoleCode switch
    {
        "admin" => "Администратор",
        "critic" => "Критик",
        _ => "Пользователь"
    };
}
