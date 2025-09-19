using Microsoft.Win32;
using System;
using System.IO;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using PackageConsole;
using PackageConsole.Data;
using NLog;

namespace PackageConsole
{
    public partial class FeedbackPage : Page
    {
        private HashSet<string> AdminUsers;
        private ObservableCollection<FeedbackEntry> feedbackEntries = new ObservableCollection<FeedbackEntry>();
        private string screenshotPath = null;
        private readonly string feedbackDir = PathManager.FeedbackPath;
        private static readonly Logger Logger = LogManager.GetLogger(nameof(FeedbackPage));

        public FeedbackPage()
        {
            InitializeComponent();

            AdminUsers = AppUserRoles.AdminUsers;
            Logger.Info("Admin user list loaded from config.");

            LoadFeedbackEntries();
            Logger.Info($"FeedbackPage initialized for user: {Environment.UserName}");
        }

        private void SubmitYes_Checked(object sender, RoutedEventArgs e)
        {
            submissionPanel.Visibility = Visibility.Visible;
        }

        private void SubmitNo_Checked(object sender, RoutedEventArgs e)
        {
            //submissionPanel.Visibility = Visibility.Collapsed;
        }
        private void LoadFeedbackEntries()
        {
            feedbackEntries.Clear();
            if (IsCurrentUserAdmin() && chkShowAllFeedback?.IsChecked == true)
            {
                Logger.Info("Admin mode: loading all feedback from central dbs");
                foreach (var entry in SqliteAdminAggregator.LoadAllFeedbacks())
                    feedbackEntries.Add(entry);
            }
            else
            {
                foreach (var entry in SqliteHelper.GetLatestFeedbacks())
                    feedbackEntries.Add(entry);
            }

            feedbackGrid.ItemsSource = feedbackEntries;
            btnUpdateResponse.Visibility = IsCurrentUserAdmin() ? Visibility.Visible : Visibility.Collapsed;
            Logger.Info($"Loaded {feedbackEntries.Count} feedback entries.");
        }

        private bool IsCurrentUserAdmin()
        {
            if (AdminUsers == null)
            {
                Logger.Warn("AdminUsers list is null — falling back to non-admin.");
                return false;
            }

            string currentUser = Environment.UserName.ToLower();
            bool isAdmin = AdminUsers.Contains(currentUser);
            Logger.Info($"User '{currentUser}' is {(isAdmin ? "" : "not ")}an admin.");
            return isAdmin;
        }

        private void BtnAttachScreenshot_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg"
            };
            if (dlg.ShowDialog() == true)
            {
                screenshotPath = dlg.FileName;
                lblScreenshotPath.Text = Path.GetFileName(screenshotPath);
                Logger.Info($"Screenshot attached: {screenshotPath}");
            }
        }

        private void BtnSubmitFeedback_Click(object sender, RoutedEventArgs e)
        {
            string type = (cmbFeedbackType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string msg = txtFeedbackMessage.Text.Trim();
            string user = Environment.UserName;

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(msg))
            {
                Logger.Warn("Feedback submission blocked — missing type or message.");
                MessageBox.Show("Please select type and enter message.", "Validation");
                return;
            }

            try
            {
                var entry = new FeedbackEntry
                {
                    User = user,
                    Type = type,
                    Time = DateTime.Now,
                    Message = msg,
                    Response = GenerateResponse(msg),
                    Severity = "Normal",
                    ScreenshotPath = screenshotPath
                };

                SqliteHelper.InsertFeedback(entry);
                Logger.Info($"Feedback submitted by {user} - Type: {type} - Message: {msg}");

                screenshotPath = null;
                txtFeedbackMessage.Clear();
                lblScreenshotPath.Text = "";
                cmbFeedbackType.SelectedIndex = -1;
                submissionPanel.Visibility = Visibility.Collapsed;
                LoadFeedbackEntries();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error saving feedback from user {user}");
                MessageBox.Show($"Error saving feedback: {ex.Message}");
            }
        }

        private string GenerateResponse(string msg)
        {
            msg = msg.ToLower();
            if (msg.Contains("bug") || msg.Contains("crash")) return "We’ll look into this bug 🛠️";
            if (msg.Contains("feature") || msg.Contains("add")) return "Feature request noted! 📌";
            if (msg.Contains("idea") || msg.Contains("suggest")) return "Thanks for the idea 💡";
            return "Thank you for your feedback!";
        }

        private void chkShowAllFeedback_Checked(object sender, RoutedEventArgs e)
        {
            if (IsCurrentUserAdmin())
            {
                LoadFeedbackEntries();
            }
        }

        private void chkShowAllFeedback_Unchecked(object sender, RoutedEventArgs e)
        {
            if (IsCurrentUserAdmin())
            {
                LoadFeedbackEntries();
            }
        }
    }
}
