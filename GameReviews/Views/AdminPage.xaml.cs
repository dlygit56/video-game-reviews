using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameReviews.Data;

namespace GameReviews.Views;

public partial class AdminPage : UserControl
{
    public AdminPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            UsersList.ItemsSource = await Repository.GetUsersAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private async void Role_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: UserRow user, Tag: string tag }) return;
        var roleId = short.Parse(tag);
        if (roleId == user.RoleId) return;

        try
        {
            await Repository.SetUserRoleAsync(user.Id, roleId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }
}
