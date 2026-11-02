using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using GameReviews.Data;

namespace GameReviews.Views;

public partial class ReviewsPage : UserControl
{
    private readonly MainWindow _main;
    private readonly HashSet<short> _selected = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public ReviewsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await LoadGamesAsync();
        };
        Loaded += async (_, _) => await InitAsync();
    }

    private bool _initialized;

    private async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            GenresList.ItemsSource = await Repository.GetGenresAsync();
            await LoadGamesAsync();
        }
        catch (Exception ex)
        {
            _initialized = false;
            Ui.Error(ex);
        }
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            var games = await Repository.GetGamesAsync(SearchBox.Text, _selected);
            GamesList.ItemsSource = games;
            CountText.Text = $"Найдено: {games.Count}";
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private async void Genre_Click(object sender, RoutedEventArgs e)
    {
        var tb = (ToggleButton)sender;
        var id = (short)tb.Tag;
        if (tb.IsChecked == true) _selected.Add(id); else _selected.Remove(id);
        NoGenreText.Visibility = _selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        await LoadGamesAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _debounce.Stop();
        _debounce.Start();
    }

    private void Game_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: GameItem game })
            _main.ShowGame(game);
    }
}
