using Microsoft.Win32;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Timers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NLog;
using Microsoft.Web.WebView2.Core;
using System.Windows.Media.Animation;
using System.IO;
using Timer = System.Timers.Timer;
using PackageConsole;


namespace PackageConsole
{
    public partial class MainWindow : Window
    {
        private Timer? _timer;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("MainWindow initialized.");

            // MVVM: set DataContext to Shell ViewModel
            this.DataContext = new PackageConsole.ViewModels.MainWindowViewModel();

            // Wire NavigationService -> Frame navigation
            var nav = PackageConsole.Services.NavigationService.Instance;
            nav.PropertyChanged += NavigationService_PropertyChanged;
            // Navigate to initial view if set by VM
            if (nav.CurrentView is Page pInit)
            {
                MainContentArea.Navigate(pInit);
            }
            else
            {
                MainContentArea.Content = nav.CurrentView;
            }


            LoadUserDetails();
            StartMarqueeUpdate();
            // Ensure marquee restarts once layout is ready and on resize/text changes
            this.Loaded += (_, __) => RestartMarquee();
            MainContentArea.SizeChanged += (_, __) => RestartMarquee();
            if (MarqueeText != null) MarqueeText.SizeChanged += (_, __) => RestartMarquee();
            StartMarquee();
        }


        private void StartMarqueeUpdate()
        {
            _timer = new Timer(5000); // Update every 5 seconds
            _timer.Elapsed += UpdateMarqueeText;
            _timer.Start();
        }

        private void UpdateMarqueeText(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string filePath = PathManager.UpdatesFilePath;
                if (File.Exists(filePath))
                {
                    MarqueeText.Text = File.ReadAllText(filePath);
                    RestartMarquee();
                }
            });
        }

        private void StartMarquee()
        {
            RestartMarquee();
        }

        private void RestartMarquee()
        {
            if (MarqueeText == null || MainContentArea == null) return;

            // stop any existing animation
            MarqueeText.BeginAnimation(Canvas.LeftProperty, null);

            // ensure layout is up to date
            MainContentArea.UpdateLayout();
            MarqueeText.UpdateLayout();

            double from = MainContentArea.ActualWidth;
            double to = -MarqueeText.ActualWidth;

            var marqueeAnimation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromSeconds(20)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            MarqueeText.BeginAnimation(Canvas.LeftProperty, marqueeAnimation);
        }
        private void LoadUserDetails()
        {
            // Simulate fetching the logged-in user's name
            string usernameWithDomain = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string[] parts = usernameWithDomain.Split('\\');
            string username = parts.Length > 1 ? parts[1] : usernameWithDomain;
            //string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name; // Replace with actual logic to get the username
            UserDetails.Text = $"Welcome, {username}";
            Logger.Info($"IniConsolePage initialized by the user : {usernameWithDomain} ");

        }

        private void NavigationService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null || e.PropertyName == "CurrentView")
            {
                var view = PackageConsole.Services.NavigationService.Instance.CurrentView;
                if (view is Page page)
                {
                    MainContentArea.Navigate(page);
                }
                else
                {
                    MainContentArea.Content = view;
                }
            }
        }


    }
}