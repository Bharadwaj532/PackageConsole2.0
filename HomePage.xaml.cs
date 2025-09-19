using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
//using IWshRuntimeLibrary;

namespace PackageConsole
{
    public class TileItem
    {
        public string Title { get; set; } = string.Empty;
        public Action ClickAction { get; set; } = static () => { }; // Trigger when clicked
    }

    public partial class HomePage : Page
    {
        private List<TileItem> tiles = new();

        public HomePage()
        {
            InitializeComponent();
            LoadTiles();
            TilePanel.ItemsSource = tiles;
            
        }

        private void LoadTiles()
        {
            tiles = new List<TileItem>

        {
            new TileItem { Title = "PowerBI Report", ClickAction = () => OpenLink("https://app.powerbi.com/groups/me/apps/198932e2-58ea-4b47-80da-275db44ef160/reports/c85d060e-73a2-48fc-bc67-a1294a1e25c1/ReportSectionc31fcaf1099fa7ff9a48?ctid=db05faca-c82a-4b9d-b9c5-0f64b6755421&experience=power-bi") },
            new TileItem { Title = "Service Now", ClickAction = () => OpenLink("https://optum.service-now.com/now/nav/ui/classic/params/target/%24vtb.do%3Fsysparm_board%3Da01ca2e0dbb8185462066693ca961938") },
            new TileItem { Title = "GPS Sharepoint", ClickAction = () => OpenLink("https://uhgazure.sharepoint.com/sites/euts-autoprovisioning-sp/PKG/Internal1/SitePages/Home.aspx") },
            new TileItem { Title = "Peer Review Portal", ClickAction = () => OpenLink("https://apps.powerapps.com/play/e/12d07f87-273a-4d38-9810-871dabc8f435/a/eadba983-be54-4199-b2d2-13a48c84623c?tenantId=db05faca-c82a-4b9d-b9c5-0f64b6755421&hint=2e30585f-c036-478d-ae84-9a4b9ecaaae0&sourcetime=1717080620797") },
            new TileItem { Title = "ARP Report", ClickAction = () => OpenLink("https://orbit-ssrs-prod-int.optum.com/Reports/report/Optum%20ET/EUTS%20Dev%20Ops/Inventory/Software/Add%20Remove%20Programs/Uninstall_String_Arp_Search_MECM") },
            new TileItem { Title = "QC Tool", ClickAction = () => LaunchApp(@"Tools\QC_Tool.EXE") },
            new TileItem { Title = "CMTrace", ClickAction = () => LaunchApp(@"c:\Windows\ccm\CMTrace.exe") },
            new TileItem { Title = "QPC Template", ClickAction = () => LaunchApp(@"Tools\QCPeerReviewChecklist_v1.3.0.rqct") },
            new TileItem { Title = "Quick Package Creator", ClickAction = () => LaunchApp(@"Tools\QuickPackageCreator.exe") },
            new TileItem { Title = "Package Dev Manager", ClickAction = () => LaunchApp(@"Tools\PckgDevMngr_1.5.4.exe") },
            new TileItem { Title = "UPI Tool", ClickAction = () => LaunchApp(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "UHG Package Info.appref-ms")) },
            new TileItem { Title = "ARP Scanner", ClickAction = () => LaunchApp(@"C:\Windows\System32\wscript.exe", @"Tools\ARPScanner3.1.vbs")  },
            new TileItem { Title = "RUN DSAgent Script", ClickAction = () =>  {
             var result = MessageBox.Show(  "Do you want to run the DSAgent Script?", "Confirmation", MessageBoxButton.YesNo,  MessageBoxImage.Question  );
             if (result == MessageBoxResult.Yes)
             {             LaunchApp(@"C:\Windows\System32\wscript.exe", @"Tools\DagentandSMAgent_Update.vbs");    }   } } ,
            new TileItem { Title = "Add New Request", ClickAction = () => PackageConsole.Services.NavigationService.Instance.Navigate(new AddPackagePage()) }
        };
        }

        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TileItem tile && tile.ClickAction != null)
                tile.ClickAction.Invoke();
        }

        private void OpenLink(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show("Unable to open link."); }
        }

        private void LaunchApp(string path, string arguments = "")
        {
            try
            {
                string fullPath = path;

                // Case 1: Relative path inside app
                if (!System.IO.Path.IsPathRooted(path))
                {
                    fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                }

                // Case 3: If it's a shortcut (.lnk), resolve the target (optional)
                if (System.IO.Path.GetExtension(fullPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    // fullPath = ResolveShortcut(fullPath); // Uncomment if you implement this
                }

                if (File.Exists(fullPath))
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = fullPath,
                        Arguments = arguments,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(startInfo);
                }
                else
                {
                    MessageBox.Show($"Executable not found:\n{fullPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Launch failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // private string ResolveShortcut(string shortcutPath)
        //{
        //  try            {
        //       var shell = new IWshRuntimeLibrary.WshShell()
        //       var link = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
        //        return link.TargetPath;            }
        //    catch            {
        //        MessageBox.Show("Failed to resolve shortcut target.");
        //         return shortcutPath;            }
        //}


    }

}
