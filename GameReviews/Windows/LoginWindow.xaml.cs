using System;
using System.Text.RegularExpressions;
using System.Windows;
using GameReviews.Data;

namespace GameReviews;

public partial class LoginWindow : Window
{
    private bool _register;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoginBox.Focus();
    }

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        _register = !_register;
        ErrorText.Visibility = Visibility.Collapsed;

        Title = HeaderText.Text = _register ? "Регистрация" : "Вход";
        LoginLabel.Text = _register ? "Имя пользователя" : "Имя пользователя или e-mail";
        EmailPanel.Visibility = ConfirmPanel.Visibility = _register ? Visibility.Visible : Visibility.Collapsed;
        SubmitButton.Content = _register ? "Зарегистрироваться" : "Войти";
        SwitchButton.Content = _register ? "Уже есть аккаунт? Войти" : "Нет аккаунта? Зарегистрироваться";
    }

    private void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorText.Visibility = Visibility.Visible;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        var login = LoginBox.Text.Trim();
        var password = PasswordBox.Password;

        if (login.Length == 0 || password.Length == 0)
        {
            ShowError("Заполните все поля.");
            return;
        }

        SubmitButton.IsEnabled = false;
        try
        {
            AppUser? user;
            if (_register)
            {
                if (!Regex.IsMatch(login, "^[A-Za-z0-9_.-]{3,64}$"))
                {
                    ShowError("Имя пользователя: 3–64 символа, латиница, цифры, «_», «.», «-».");
                    return;
                }
                var email = EmailBox.Text.Trim();
                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    ShowError("Введите корректный e-mail.");
                    return;
                }
                if (password.Length < 6)
                {
                    ShowError("Пароль должен быть не короче 6 символов.");
                    return;
                }
                if (password != ConfirmBox.Password)
                {
                    ShowError("Пароли не совпадают.");
                    return;
                }
                user = await Repository.RegisterAsync(login, email, password);
            }
            else
            {
                user = await Repository.LoginAsync(login, password);
                if (user is null)
                {
                    ShowError("Неверное имя пользователя или пароль.");
                    return;
                }
            }

            Session.Current = user;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(Ui.Friendly(ex));
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }
}
