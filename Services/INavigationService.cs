using System;
using System.ComponentModel;

namespace PackageConsole.Services
{
    public interface INavigationService : INotifyPropertyChanged
    {
        object? CurrentView { get; }
        void Navigate(object view);
    }
}

