using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PackageConsole.Services
{
    public class NavigationService : INavigationService
    {
        private static NavigationService? _instance;
        public static NavigationService Instance => _instance ??= new NavigationService();

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            private set
            {
                if (!Equals(_currentView, value))
                {
                    _currentView = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Navigate(object view)
        {
            CurrentView = view;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // private ctor for singleton
        private NavigationService() { }
    }
}

