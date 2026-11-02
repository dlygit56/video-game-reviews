using System.Windows;
using GameReviews.Data;
using GameReviews.Views;

namespace GameReviews;

public partial class MainWindow : Window
{
    private ReviewsPage? _reviewsPage;

    public MainWindow()
    {
        InitializeComponent();
        Session.Changed += OnSessionChanged;
        OnSessionChanged();
        ShowReviews();
    }

    private void OnSessionChanged()
    {
        var u = Session.Current;
        UserLabel.Text = u is null ? "Гость" : $"{u.Username} · {u.RoleName}";
        AuthButton.Content = u is null ? "Войти" : "Выйти";
        NavAdmin.Visibility = u?.IsAdmin == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- навигация

    private void SetActive(System.Windows.Controls.Primitives.ToggleButton? active)
    {
        NavReviews.IsChecked = ReferenceEquals(active, NavReviews);
        NavLeaderboard.IsChecked = ReferenceEquals(active, NavLeaderboard);
        NavProfile.IsChecked = ReferenceEquals(active, NavProfile);
        NavAdmin.IsChecked = ReferenceEquals(active, NavAdmin);
    }

    public void ShowReviews()
    {
        _reviewsPage ??= new ReviewsPage(this);
        PageHost.Content = _reviewsPage;
        SetActive(NavReviews);
    }

    public void ShowGame(GameItem game)
    {
        PageHost.Content = new GameDetailPage(this, game);
        SetActive(NavReviews);
    }

    private void NavReviews_Click(object sender, RoutedEventArgs e) => ShowReviews();

    private void NavLeaderboard_Click(object sender, RoutedEventArgs e)
    {
        PageHost.Content = new LeaderboardPage(this);
        SetActive(NavLeaderboard);
    }

    private void NavProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Current is null && !Login())
        {
            SetActive(NavReviews);
            return;
        }
        PageHost.Content = new ProfilePage(this);
        SetActive(NavProfile);
    }

    private void NavAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Current?.IsAdmin != true) return;
        PageHost.Content = new AdminPage();
        SetActive(NavAdmin);
    }

    // ---------------------------------------------------------------- вход / выход

    public bool Login()
    {
        var dlg = new LoginWindow { Owner = this };
        var ok = dlg.ShowDialog() == true;
        if (ok) RefreshCurrentPage();
        return ok;
    }

    private void Auth_Click(object sender, RoutedEventArgs e)
    {
        if (Session.Current is null)
        {
            Login();
        }
        else
        {
            Session.Current = null;
            RefreshCurrentPage();
        }
    }

    /// <summary>После входа/выхода возвращаемся на главную — права на страницах меняются.</summary>
    private void RefreshCurrentPage()
    {
        _reviewsPage = null;
        ShowReviews();
    }
}
