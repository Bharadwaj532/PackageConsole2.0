using NLog;
using PackageConsole.Helpers;
using PackageConsole.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PackageConsole.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly INavigationService _nav;

        private bool _isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set { _isSidebarOpen = value; OnPropertyChanged(); }
        }

        public bool IsDevUser { get; }
        public bool IsAdminUser { get; }


        private string _currentRoute = "Home";
        public string CurrentRoute
        {
            get => _currentRoute;
            set { _currentRoute = value; OnPropertyChanged(); }
        }

        public object? CurrentView => _nav.CurrentView;

        public ICommand ToggleSidebarCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand AddPackageCommand { get; }
        public ICommand CopyToolkitCommand { get; }
        public ICommand TestingCommand { get; }
        public ICommand PreviousAppsCommand { get; }
        public ICommand ViewPackagesCommand { get; }
        public ICommand FeedbackCommand { get; }
        public ICommand IniConsoleCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand AdminSettingsCommand { get; }

        public MainWindowViewModel()
        {
            _nav = NavigationService.Instance;
            _nav.PropertyChanged += (_, __) => OnPropertyChanged(nameof(CurrentView));

            // Init start view
            CurrentRoute = "Home";
            _nav.Navigate(new HomePage());

            var currentUser = Environment.UserName.ToLowerInvariant();
            IsDevUser = AppUserRoles.DevUsers.Contains(currentUser);
            IsAdminUser = AppUserRoles.IsCurrentUserAdmin();

            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarOpen = !IsSidebarOpen);
            HomeCommand = new RelayCommand(_ => { Logger.Info("Navigating to Home page..."); CurrentRoute = "Home"; _nav.Navigate(new HomePage()); IsSidebarOpen = false; });
            AddPackageCommand = new RelayCommand(_ => { Logger.Info("Navigating to AddPackagePage."); CurrentRoute = "AddPackage"; _nav.Navigate(new AddPackagePage()); IsSidebarOpen = false; });
            CopyToolkitCommand = new RelayCommand(_ => { Logger.Info("Navigating to Copy Toolkit page..."); CurrentRoute = "CopyToolkit"; _nav.Navigate(new CopyPackage()); IsSidebarOpen = false; });
            TestingCommand = new RelayCommand(_ => { Logger.Info("Navigating to Testing page..."); CurrentRoute = "Testing"; _nav.Navigate(new AppDeploymentTestPage()); IsSidebarOpen = false; }, _ => IsDevUser);
            PreviousAppsCommand = new RelayCommand(_ => { Logger.Info("Navigating to Previous Apps page..."); CurrentRoute = "PreviousApps"; _nav.Navigate(new PreviousApps()); IsSidebarOpen = false; });
            ViewPackagesCommand = new RelayCommand(_ => { Logger.Info("Navigating to PackageViewerPage"); CurrentRoute = "PackagesDashboard"; _nav.Navigate(new PackageConsole.Pages.PackageViewerPage()); IsSidebarOpen = false; });
            FeedbackCommand = new RelayCommand(_ => { Logger.Info("Navigating to Feedback page..."); CurrentRoute = "Feedback"; _nav.Navigate(new FeedbackPage()); IsSidebarOpen = false; });
            IniConsoleCommand = new RelayCommand(_ => NavigateToIniConsole());
            HelpCommand = new RelayCommand(_ => { var w = new HelpWindow(); w.ShowDialog(); });
            LogoutCommand = new RelayCommand(_ => { MessageBox.Show("Logged out successfully!"); Application.Current.MainWindow?.Close(); });
            AdminSettingsCommand = new RelayCommand(_ => { Logger.Info("Navigating to Admin Settings page..."); CurrentRoute = "AdminSettings"; _nav.Navigate(new AdminSettingsPage()); IsSidebarOpen = false; }, _ => IsAdminUser);
        }

        private void NavigateToIniConsole()
        {
            Logger.Info("Selected INIconsole Page");
            IsSidebarOpen = false;

            if (_nav.CurrentView is not AddPackagePage addPage)
            {
                MessageBox.Show("Please open the Add Package Page and fill required fields before proceeding.", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Warn("INI Console access attempted without active AddPackagePage context.");
                return;
            }

            string productName = addPage.productNameTextBox.Text.Trim();
            string productVersion = addPage.fileVersionTextBox.Text.Trim();
            string drmBuild = addPage.drmBuildTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(productVersion))
            {
                MessageBox.Show("First, add the package before clicking on the INI Console Page.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Warn("Attempted to access INI Console Page without setting required fields.");
                return;
            }

            string basePath = PathManager.BasePackagePath;
            string productFolder = System.IO.Path.Combine(basePath, productName, productVersion, "Altiris");
            string supportFilesFolder = System.IO.Path.Combine(productFolder, drmBuild, "SupportFiles");

            Directory.CreateDirectory(productFolder);
            Directory.CreateDirectory(supportFilesFolder);

            Logger.Info($"Navigating to IniConsolePage for {productName} {productVersion}.");
            CurrentRoute = "IniConsole";
            _nav.Navigate(new IniConsolePage(productFolder, supportFilesFolder));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

