using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace GameReviews.Data;

/// <summary>
/// Весь доступ к БД. Все значения передаются ТОЛЬКО параметрами (@name) —
/// строки запросов никогда не склеиваются с пользовательским вводом (защита от SQL-инъекций).
/// Пароли проверяются и хэшируются самой БД через pgcrypto: crypt(@p, password_hash).
/// </summary>
public static class Repository
{
    private static readonly NpgsqlDataSource Source = NpgsqlDataSource.Create(Config.ConnectionString);

    // ------------------------------------------------------------------ жанры и игры

    public static async Task<List<Genre>> GetGenresAsync()
    {
        const string sql = "SELECT id, name FROM genres ORDER BY name";
        await using var cmd = Source.CreateCommand(sql);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Genre>();
        while (await r.ReadAsync())
            list.Add(new Genre(r.GetInt16(0), r.GetString(1)));
        return list;
    }

    private const string GameSelect = @"
        SELECT g.id, g.title, g.description, g.release_date, g.rating_avg,
               (SELECT count(*) FROM reviews v WHERE v.game_id = g.id AND v.status_id = 2) AS reviews_count,
               COALESCE((SELECT string_agg(gn.name, ', ' ORDER BY gn.name)
                           FROM game_genres gg JOIN genres gn ON gn.id = gg.genre_id
                          WHERE gg.game_id = g.id), '') AS genres
          FROM games g";

    private static GameItem ReadGame(NpgsqlDataReader r) => new(
        r.GetInt64(0),
        r.GetString(1),
        r.IsDBNull(2) ? null : r.GetString(2),
        r.IsDBNull(3) ? null : r.GetDateTime(3),
        r.GetDecimal(4),
        (int)r.GetInt64(5),
        r.GetString(6));

    public static async Task<List<GameItem>> GetGamesAsync(string search, IReadOnlyCollection<short> genreIds)
    {
        var sql = GameSelect + @"
         WHERE (@q = '' OR g.title ILIKE '%' || @q || '%')
           AND (cardinality(@genres) = 0
                OR EXISTS (SELECT 1 FROM game_genres gg
                            WHERE gg.game_id = g.id AND gg.genre_id = ANY(@genres)))
         ORDER BY g.rating_avg DESC, g.title
         LIMIT 500";

        var escaped = (search ?? "").Trim()
            .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("q", escaped);
        cmd.Parameters.Add(new NpgsqlParameter("genres", NpgsqlDbType.Array | NpgsqlDbType.Smallint)
        {
            Value = genreIds.ToArray()
        });

        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<GameItem>();
        while (await r.ReadAsync())
            list.Add(ReadGame(r));
        return list;
    }

    public static async Task<GameItem?> GetGameAsync(long id)
    {
        await using var cmd = Source.CreateCommand(GameSelect + " WHERE g.id = @id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadGame(r) : null;
    }

    // ------------------------------------------------------------------ рецензии

    private const string ReviewSelect = @"
        SELECT v.id, v.game_id, g.title, v.critic_id, u.username, v.title, v.body,
               v.status_id, s.name, rt.score, v.created_at,
               (SELECT count(*) FROM review_views rv WHERE rv.review_id = v.id) AS views
          FROM reviews v
          JOIN games g            ON g.id = v.game_id
          JOIN users u            ON u.id = v.critic_id
          JOIN review_statuses s  ON s.id = v.status_id
          LEFT JOIN ratings rt    ON rt.review_id = v.id";

    private static ReviewItem ReadReview(NpgsqlDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt64(3), r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5),
        r.GetString(6),
        r.GetInt16(7), r.GetString(8),
        r.IsDBNull(9) ? null : r.GetInt16(9),
        r.GetDateTime(10),
        (int)r.GetInt64(11));

    /// <summary>Опубликованные рецензии игры + свои (для критика) + все (для админа).</summary>
    public static async Task<List<ReviewItem>> GetReviewsForGameAsync(long gameId)
    {
        var sql = ReviewSelect + @"
         WHERE v.game_id = @g
           AND (v.status_id = 2 OR @isAdmin OR v.critic_id = @uid)
         ORDER BY v.created_at DESC";

        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("g", gameId);
        cmd.Parameters.AddWithValue("isAdmin", Session.Current?.IsAdmin == true);
        cmd.Parameters.AddWithValue("uid", Session.UserId);
        return await ReadReviews(cmd);
    }

    public static async Task<List<ReviewItem>> GetMyReviewsAsync(long userId)
    {
        await using var cmd = Source.CreateCommand(ReviewSelect + " WHERE v.critic_id = @u ORDER BY v.created_at DESC");
        cmd.Parameters.AddWithValue("u", userId);
        return await ReadReviews(cmd);
    }

    private static async Task<List<ReviewItem>> ReadReviews(NpgsqlCommand cmd)
    {
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<ReviewItem>();
        while (await r.ReadAsync())
            list.Add(ReadReview(r));
        return list;
    }

    public static async Task RecordViewAsync(long userId, long reviewId)
    {
        const string sql = @"INSERT INTO review_views (user_id, review_id) VALUES (@u, @r)
                             ON CONFLICT (user_id, review_id) DO NOTHING";
        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("r", reviewId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<ViewedReview>> GetViewedAsync(long userId)
    {
        const string sql = @"
            SELECT v.id, g.title, v.title, rv.viewed_at
              FROM review_views rv
              JOIN reviews v ON v.id = rv.review_id
              JOIN games g   ON g.id = v.game_id
             WHERE rv.user_id = @u
             ORDER BY rv.viewed_at DESC
             LIMIT 100";
        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<ViewedReview>();
        while (await r.ReadAsync())
            list.Add(new ViewedReview(r.GetInt64(0), r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2), r.GetDateTime(3)));
        return list;
    }

    /// <summary>Создание (reviewId == null) или правка рецензии вместе с оценкой — в одной транзакции.</summary>
    public static async Task SaveReviewAsync(long? reviewId, long criticId, long gameId,
        string? title, string body, short statusId, short score)
    {
        await using var conn = await Source.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var titleValue = string.IsNullOrWhiteSpace(title) ? (object)DBNull.Value : title.Trim();

        if (reviewId is null)
        {
            long newId;
            await using (var ins = new NpgsqlCommand(
                @"INSERT INTO reviews (critic_id, game_id, title, body, status_id)
                  VALUES (@c, @g, @t, @b, @s) RETURNING id", conn, tx))
            {
                ins.Parameters.AddWithValue("c", criticId);
                ins.Parameters.AddWithValue("g", gameId);
                ins.Parameters.AddWithValue("t", titleValue);
                ins.Parameters.AddWithValue("b", body);
                ins.Parameters.AddWithValue("s", statusId);
                newId = (long)(await ins.ExecuteScalarAsync())!;
            }

            await using var rate = new NpgsqlCommand(
                "INSERT INTO ratings (review_id, score) VALUES (@r, @sc)", conn, tx);
            rate.Parameters.AddWithValue("r", newId);
            rate.Parameters.AddWithValue("sc", score);
            await rate.ExecuteNonQueryAsync();
        }
        else
        {
            await using (var upd = new NpgsqlCommand(
                @"UPDATE reviews SET title = @t, body = @b, status_id = @s
                   WHERE id = @id AND critic_id = @c", conn, tx))
            {
                upd.Parameters.AddWithValue("t", titleValue);
                upd.Parameters.AddWithValue("b", body);
                upd.Parameters.AddWithValue("s", statusId);
                upd.Parameters.AddWithValue("id", reviewId.Value);
                upd.Parameters.AddWithValue("c", criticId);
                if (await upd.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("Рецензия не найдена или принадлежит другому автору.");
            }

            await using var rate = new NpgsqlCommand(
                @"INSERT INTO ratings (review_id, score) VALUES (@r, @sc)
                  ON CONFLICT (review_id) DO UPDATE SET score = EXCLUDED.score", conn, tx);
            rate.Parameters.AddWithValue("r", reviewId.Value);
            rate.Parameters.AddWithValue("sc", score);
            await rate.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    /// <summary>Автор удаляет свою рецензию, админ — любую.</summary>
    public static async Task DeleteReviewAsync(long reviewId)
    {
        const string sql = "DELETE FROM reviews WHERE id = @id AND (critic_id = @u OR @isAdmin)";
        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", reviewId);
        cmd.Parameters.AddWithValue("u", Session.UserId);
        cmd.Parameters.AddWithValue("isAdmin", Session.Current?.IsAdmin == true);
        if (await cmd.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Недостаточно прав или рецензия уже удалена.");
    }

    /// <summary>Модерация (только админ): опубликовать / скрыть.</summary>
    public static async Task SetReviewStatusAsync(long reviewId, short statusId)
    {
        if (Session.Current?.IsAdmin != true)
            throw new InvalidOperationException("Модерация доступна только администратору.");

        await using var cmd = Source.CreateCommand("UPDATE reviews SET status_id = @s WHERE id = @id");
        cmd.Parameters.AddWithValue("s", statusId);
        cmd.Parameters.AddWithValue("id", reviewId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------ лидерборд

    public static async Task<List<GameItem>> GetTopGamesAsync(int limit = 50)
    {
        await using var cmd = Source.CreateCommand(
            GameSelect + " WHERE g.rating_avg > 0 ORDER BY g.rating_avg DESC, g.title LIMIT @n");
        cmd.Parameters.AddWithValue("n", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<GameItem>();
        while (await r.ReadAsync())
            list.Add(ReadGame(r));
        return list;
    }

    public static async Task<List<CriticStat>> GetCriticStatsAsync()
    {
        const string sql = @"
            SELECT u.username, count(v.id) AS cnt, round(avg(rt.score), 2) AS avg_score
              FROM users u
              JOIN reviews v ON v.critic_id = u.id AND v.status_id = 2
              JOIN ratings rt ON rt.review_id = v.id
             GROUP BY u.id, u.username
             ORDER BY cnt DESC, avg_score DESC
             LIMIT 50";
        await using var cmd = Source.CreateCommand(sql);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<CriticStat>();
        while (await r.ReadAsync())
            list.Add(new CriticStat(r.GetString(0), (int)r.GetInt64(1), r.GetDecimal(2)));
        return list;
    }

    // ------------------------------------------------------------------ пользователи

    public static async Task<AppUser?> LoginAsync(string login, string password)
    {
        const string sql = @"
            SELECT u.id, u.username, u.email, r.code, u.created_at
              FROM users u
              JOIN roles r ON r.id = u.role_id
             WHERE (lower(u.username) = lower(@l) OR lower(u.email) = lower(@l))
               AND u.password_hash = crypt(@p, u.password_hash)";
        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("l", login.Trim());
        cmd.Parameters.AddWithValue("p", password);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new AppUser(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDateTime(4));
    }

    /// <summary>Регистрация всегда создаёт обычного пользователя (role_id = 1 по умолчанию).</summary>
    public static async Task<AppUser> RegisterAsync(string username, string email, string password)
    {
        const string sql = @"
            WITH ins AS (
                INSERT INTO users (username, email, password_hash)
                VALUES (@u, @e, crypt(@p, gen_salt('bf', 8)))
                RETURNING id, username, email, role_id, created_at)
            SELECT ins.id, ins.username, ins.email, r.code, ins.created_at
              FROM ins JOIN roles r ON r.id = ins.role_id";
        await using var cmd = Source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("u", username.Trim());
        cmd.Parameters.AddWithValue("e", email.Trim());
        cmd.Parameters.AddWithValue("p", password);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new AppUser(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDateTime(4));
    }

    // ------------------------------------------------------------------ админка

    public static async Task<List<UserRow>> GetUsersAsync()
    {
        const string sql = @"
            SELECT u.id, u.username, u.email, r.code, r.id,
                   (SELECT count(*) FROM reviews v WHERE v.critic_id = u.id) AS reviews_count
              FROM users u JOIN roles r ON r.id = u.role_id
             ORDER BY r.id DESC, u.username
             LIMIT 500";
        await using var cmd = Source.CreateCommand(sql);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<UserRow>();
        while (await r.ReadAsync())
            list.Add(new UserRow(r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.GetString(3), r.GetInt16(4), (int)r.GetInt64(5)));
        return list;
    }

    public static async Task SetUserRoleAsync(long userId, short roleId)
    {
        if (Session.Current?.IsAdmin != true)
            throw new InvalidOperationException("Менять роли может только администратор.");
        if (userId == Session.UserId)
            throw new InvalidOperationException("Нельзя менять роль самому себе.");

        await using var cmd = Source.CreateCommand("UPDATE users SET role_id = @r WHERE id = @id");
        cmd.Parameters.AddWithValue("r", roleId);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
