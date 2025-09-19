using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PackageConsole.Models
{
    public class PackageInfo
    {
        public string AppKeyID { get; set; }          // Primary Key
        public string AppName { get; set; }
        public string AppVersion { get; set; }
        public string ProductCode { get; set; }
        public string Vendor { get; set; }
        public string DRMBuild { get; set; }
        public string RebootOption { get; set; }
        public string InstallerType { get; set; }      // MSI / EXE
        public string InstallerFile { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
        public string PackageIniText { get; set; }     // Full INI contents from IniConsolePage
    }
    public class IniEntry : INotifyPropertyChanged
    {
        private string key;
        private string value;

        public string Key
        {
            get => key;
            set { key = value; OnPropertyChanged(nameof(Key)); }
        }

        public string Value
        {
            get => value;
            set { this.value = value; OnPropertyChanged(nameof(Value)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class IniSection : INotifyPropertyChanged
    {
        private string name;
        private ObservableCollection<IniEntry> entries;

        public string Name
        {
            get => name;
            set { name = value; OnPropertyChanged(nameof(Name)); }
        }

        public ObservableCollection<IniEntry> Entries
        {
            get => entries;
            set { entries = value; OnPropertyChanged(nameof(Entries)); }
        }

        public IniSection(string name)
        {
            Name = name;
            Entries = new ObservableCollection<IniEntry>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
