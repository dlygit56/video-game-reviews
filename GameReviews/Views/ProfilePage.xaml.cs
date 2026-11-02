using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameReviews.Data;

namespace GameReviews.Views;

public partial class ProfilePage : UserControl
{
    public ProfilePage(MainWindow main)
    {
        InitializeComponent();

        var me = Session.Current;
        if (me is null) return;

        NameText.Text = me.Username;
        RoleText.Text = me.RoleName;
        EmailText.Text = me.Email;
        SinceText.Text = $"На сайте с {me.CreatedAt.ToLocalTime():dd.MM.yyyy}";

        Loaded += async (_, _) => await LoadAsync(me);
    }

    private async Task LoadAsync(AppUser me)
    {
        try
        {
            if (me.IsCritic)
            {
                var mine = await Repository.GetMyReviewsAsync(me.Id);
                MyReviewsPanel.Visibility = Visibility.Visible;
                MyReviewsList.ItemsSource = mine;
                MyReviewsEmpty.Visibility = mine.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            var viewed = await Repository.GetViewedAsync(me.Id);
            ViewedList.ItemsSource = viewed;
            ViewedEmpty.Visibility = viewed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }
}
