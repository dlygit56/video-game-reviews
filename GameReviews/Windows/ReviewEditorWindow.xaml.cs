using System;
using System.Windows;
using GameReviews.Data;

namespace GameReviews;

public partial class ReviewEditorWindow : Window
{
    private readonly GameItem _game;
    private readonly ReviewItem? _review;

    public ReviewEditorWindow(GameItem game, ReviewItem? review)
    {
        InitializeComponent();
        _game = game;
        _review = review;

        HeaderText.Text = review is null ? $"Новая рецензия: {game.Title}" : $"Правка рецензии: {game.Title}";

        if (review is not null)
        {
            TitleBox.Text = review.Title ?? "";
            BodyBox.Text = review.Body;
            ScoreSlider.Value = review.Score ?? 7;
            DraftRadio.IsChecked = review.StatusId == 1;
            PublishedRadio.IsChecked = review.StatusId != 1;
        }

        UpdateScoreLabel();
    }

    private void ScoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateScoreLabel();

    private void UpdateScoreLabel()
    {
        if (ScoreText is null || ScoreEmoji is null) return; // событие приходит во время InitializeComponent
        var v = (int)ScoreSlider.Value;
        ScoreText.Text = $"{v}/10";
        ScoreEmoji.Text = RatingFormat.Emoji(v);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var me = Session.Current;
        if (me is null || !me.IsCritic)
        {
            Ui.Info("Писать рецензии могут только пользователи с ролью «Критик».", this);
            return;
        }

        var body = BodyBox.Text.Trim();
        if (body.Length == 0)
        {
            Ui.Info("Текст рецензии не может быть пустым.", this);
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            await Repository.SaveReviewAsync(
                _review?.Id, me.Id, _game.Id,
                TitleBox.Text, body,
                (short)(DraftRadio.IsChecked == true ? 1 : 2),
                (short)ScoreSlider.Value);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Ui.Error(ex, this);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }
}
