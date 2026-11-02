using System;
using System.Windows;
using Npgsql;

namespace GameReviews;

public static class Ui
{
    public static void Error(Exception ex, Window? owner = null)
    {
        MessageBox.Show(owner ?? Application.Current.MainWindow, Friendly(ex), "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static void Info(string text, Window? owner = null)
    {
        MessageBox.Show(owner ?? Application.Current.MainWindow, text, "Игровые рецензии",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static bool Confirm(string text, Window? owner = null) =>
        MessageBox.Show(owner ?? Application.Current.MainWindow, text, "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static string Friendly(Exception ex)
    {
        if (ex is PostgresException pg)
        {
            return pg.SqlState switch
            {
                "23505" when pg.ConstraintName?.Contains("username") == true => "Такое имя пользователя уже занято.",
                "23505" when pg.ConstraintName?.Contains("email") == true => "Этот e-mail уже зарегистрирован.",
                "23505" when pg.ConstraintName == "uq_reviews_critic_game" => "Вы уже написали рецензию на эту игру — отредактируйте её.",
                "23505" => "Такая запись уже существует.",
                "23514" when pg.ConstraintName == "chk_username_format" => "Имя пользователя: 3–64 символа, только латиница, цифры, «_», «.», «-».",
                "23514" when pg.ConstraintName == "chk_email_format" => "Некорректный e-mail.",
                "23514" => pg.MessageText,
                "42883" when pg.MessageText.Contains("crypt") => "В базе не включено расширение pgcrypto. Выполните: CREATE EXTENSION pgcrypto;",
                _ => $"Ошибка БД ({pg.SqlState}): {pg.MessageText}"
            };
        }

        if (ex is NpgsqlException or System.Net.Sockets.SocketException)
            return "Не удалось подключиться к базе данных. Проверьте строку подключения в appsettings.json и что PostgreSQL запущен.\n\n" + ex.Message;

        return ex.Message;
    }
}
