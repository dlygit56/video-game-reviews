using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameReviews.Data;

namespace GameReviews.Views;

public partial class GameDetailPage : UserControl
{
    private readonly MainWindow _main;
    private GameItem _game;
    private List<ReviewItem> _reviews = new();

    public GameDetailPage(MainWindow main, GameItem game)
    {
        InitializeComponent();
        _main = main;
        _game = game;
        ShowGame();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private void ShowGame()
    {
        GameEmoji.Text = _game.Emoji;
        GameTitle.Text = _game.Title;
        GameMeta.Text = _game.Subtitle;
        GameDescription.Text = _game.Description ?? "";
        GameDescription.Visibility = string.IsNullOrWhiteSpace(_game.Description) ? Visibility.Collapsed : Visibility.Visible;
        GameRating.Text = _game.RatingText;
    }

    private async Task ReloadAsync()
    {
        try
        {
            _game = await Repository.GetGameAsync(_game.Id) ?? _game;
            ShowGame();

            _reviews = await Repository.GetReviewsForGameAsync(_game.Id);
            ReviewsList.ItemsSource = _reviews;
            EmptyText.Visibility = _reviews.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Кнопка «Написать рецензию» — только критику и только если у него ещё нет рецензии на эту игру
            var me = Session.Current;
            WriteButton.Visibility = me is { IsCritic: true } && _reviews.All(r => r.CriticId != me.Id)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    /// <summary>Показанная пользователю опубликованная рецензия считается просмотренной (review_views).</summary>
    private async void ReviewCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ReviewItem r }) return;
        var me = Session.Current;
        if (me is null || !r.IsPublished || r.CriticId == me.Id) return;
        try
        {
            await Repository.RecordViewAsync(me.Id, r.Id);
        }
        catch
        {
            // просмотр — вспомогательная статистика, ошибку пользователю не показываем
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _main.ShowReviews();

    private async void Write_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ReviewEditorWindow(_game, null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            await ReloadAsync();
    }

    private static ReviewItem? Item(object sender) => (sender as FrameworkElement)?.DataContext as ReviewItem;

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Item(sender) is not { } review) return;
        var dlg = new ReviewEditorWindow(_game, review) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (Item(sender) is not { } review) return;
        try
        {
            await Repository.SetReviewStatusAsync(review.Id, (short)(review.IsPublished ? 3 : 2));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Item(sender) is not { } review) return;
        if (!Ui.Confirm("Удалить рецензию безвозвратно?")) return;
        try
        {
            await Repository.DeleteReviewAsync(review.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }
}
