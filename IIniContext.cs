using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackageConsole
{
    public interface IIniContext
    {
        List<string> GetSectionNames();
        Dictionary<string, string> GetKeyValues(string section);
        bool HasSection(string section);
        Dictionary<string, Dictionary<string, string>> IniSections { get; }
        void UpdateIniSection(string section, Dictionary<string, string> values);
        void UpdateIniSections(Dictionary<string, Dictionary<string, string>> allSections);
        void SaveIniFile();
        void RefreshIniContent();
    }
}

