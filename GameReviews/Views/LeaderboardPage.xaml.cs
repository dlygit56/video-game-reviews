using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using GameReviews.Data;

namespace GameReviews.Views;

public record RankedGame(int Rank, GameItem Game);

public partial class LeaderboardPage : UserControl
{
    private readonly MainWindow _main;

    public LeaderboardPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var games = await Repository.GetTopGamesAsync();
            GamesList.ItemsSource = games.Select((g, i) => new RankedGame(i + 1, g)).ToList();
            CriticsList.ItemsSource = await Repository.GetCriticStatsAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Game_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: RankedGame rg })
            _main.ShowGame(rg.Game);
    }
}
