using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NitroxLauncher.Models;
using NitroxModel.Server;
using NitroxServer.Serialization;
using NitroxServer.Serialization.World;

namespace NitroxLauncher.Pages
{
    public partial class ServerPage : PageBase
    {
        public string LauncherVersion => $"{LauncherLogic.RELEASE_PHASE} v{LauncherLogic.Version}";     // May need to change "RELEASE_PHASE" to "ReleasePhase" with Clement's new PR
        public bool IsServerExternal
        {
            get => LauncherLogic.Config.IsExternalServer;
            set => LauncherLogic.Config.IsExternalServer = value;
        }
        
        public string PathToSubnautica => LauncherLogic.Config.SubnauticaPath;

        // World settings variables (TODO: REMOVE THESE)
        //private readonly ServerConfig serverConfig = ServerConfig.Load();

        public string SelectedWorldName { get; set; }
        public string SelectedWorldSeed { get; set; }
        public string SelectedWorldVersion { get; set; }
        public ServerGameMode SelectedWorldGamemode { get; set; }
        public bool EnableCheatsValue { get; set; }
        public bool EnableAutoPortForwardValue { get; set; }
        public bool EnableFullEntityCacheValue { get; set; }
        public bool EnableLanDiscoveryValue { get; set; }
        public int ServerPort { get; set; }

        public bool IsNewWorld { get; set; }

        //public int SelectedWorldIndex { get; set; }
        public string SelectedWorldDirectory { get; set; }

        public ServerPage()
        {
            InitializeComponent();
            InitializeWorldListing();

            // If the "Display Server Console Externally" Checkbox is checked, set value to true - (Is this needed anymore?)
            if (CBIsExternal.IsChecked == true)
            {
                CBIsExternal.IsChecked = IsServerExternal;
            }
        }
        
        public void InitializeWorldListing()
        {
            WorldManager.Refresh();
            // Set the background of the WorldSelectionContainer to be the "No Worlds found" message and the server image if the listing has no entries
            NoWorldsBackground.Opacity = WorldManager.GetSaves().Any() ? 1 : 0;
            // Bind the list data to be used in XAML
            WorldListingContainer.ItemsSource = null;
            WorldListingContainer.ItemsSource = WorldManager.GetSaves();
        }

        public void SaveConfigSettings()
        {
            string dest = Path.Combine(Path.GetDirectoryName(SelectedWorldDirectory) ?? throw new Exception("Selected world is empty"), SelectedWorldName);
            if (SelectedWorldDirectory != dest)
            {
                if (Directory.Exists(dest))
                {
                    LauncherNotifier.Error($"World name '{SelectedWorldName}' already exists");
                    return;
                }
                
                Directory.Move(SelectedWorldDirectory, dest);
                SelectedWorldDirectory = dest;
            }
            
            ServerConfig serverConfig = ServerConfig.Load(SelectedWorldDirectory);
            serverConfig.Update(SelectedWorldDirectory, c =>
            {
                c.SaveName = SelectedWorldName;
            
                if (IsNewWorld) { c.Seed = SelectedWorldSeed; }
            
                if (RBFreedom.IsChecked == true) { c.GameMode = ServerGameMode.FREEDOM; }
                else if (RBSurvival.IsChecked == true) { c.GameMode = ServerGameMode.SURVIVAL; }
                else if (RBCreative.IsChecked == true) { c.GameMode = ServerGameMode.CREATIVE; }
            
                c.DisableConsole = !EnableCheatsValue;
                c.AutoPortForward = EnableAutoPortForwardValue;
                c.CreateFullEntityCache = EnableFullEntityCacheValue;
                c.LANDiscoveryEnabled = EnableLanDiscoveryValue;
                c.ServerPort = ServerPort;
            
            });
            Log.Info($"Server Config updated");
        }

        public void UpdateVisualWorldSettings()
        {
            //string saveDir = Path.Combine(SavesFolderDir, "save" + saveFileNum);
            ServerConfig serverConfig = ServerConfig.Load(SelectedWorldDirectory);

            // Get config file values
            SelectedWorldName = Path.GetFileName(SelectedWorldDirectory);
            SelectedWorldSeed = serverConfig.Seed;
            SelectedWorldGamemode = serverConfig.GameMode;
            EnableCheatsValue = !serverConfig.DisableConsole;
            EnableAutoPortForwardValue = serverConfig.AutoPortForward;
            EnableFullEntityCacheValue = serverConfig.CreateFullEntityCache;
            EnableLanDiscoveryValue = serverConfig.LANDiscoveryEnabled;
            ServerPort = serverConfig.ServerPort;

            // Set the world settings values to the server.cfg values
            TBWorldName.Text = SelectedWorldName;
            TBWorldSeed.Text = SelectedWorldSeed;
            if (SelectedWorldGamemode == ServerGameMode.FREEDOM) { RBFreedom.IsChecked = true; }
            else if (SelectedWorldGamemode == ServerGameMode.SURVIVAL) { RBSurvival.IsChecked = true; }
            else if (SelectedWorldGamemode == ServerGameMode.CREATIVE) { RBCreative.IsChecked = true; }
            CBCheats.IsChecked = EnableCheatsValue;
            CBAutoPortForward.IsChecked = EnableAutoPortForwardValue;
            CBCreateFullEntityCache.IsChecked = EnableFullEntityCacheValue;
            CBLanDiscovery.IsChecked = EnableLanDiscoveryValue;
            TBWorldServerPort.Text = Convert.ToString(ServerPort);
        }

        // Pane Buttons
        public void AddWorld_Click(object sender, RoutedEventArgs e)
        {
            Log.Info($"Adding new world");
            IsNewWorld = true;
            TBWorldSeed.IsEnabled = true;

            //SelectedWorldIndex = WorldListing.Count + 1;
            //SelectedWorldDirectory = Path.Combine(SavesFolderDir, "save" + SelectedWorldIndex);
            SelectedWorldDirectory = WorldManager.CreateEmptySave($"save{WorldManager.GetSaves().Count()}");
            UpdateVisualWorldSettings();
            
            Storyboard WorldSelectedAnimationStoryboard = (Storyboard)FindResource("WorldSelectedAnimation");
            WorldSelectedAnimationStoryboard.Begin();

        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigSettings();
            InitializeWorldListing();

            Storyboard GoBackAnimationStoryboard = (Storyboard)FindResource("GoBackAnimation");
            GoBackAnimationStoryboard.Begin();
        }

        // World management
        private void SelectWorld_Click(object sender, RoutedEventArgs e)
        {
            IsNewWorld = false;
            TBWorldSeed.IsEnabled = false;

            Log.Info($"World index {WorldListingContainer.SelectedIndex} selected");

            SelectedWorldDirectory = WorldManager.GetSaves().ElementAtOrDefault(WorldListingContainer.SelectedIndex)?.WorldSaveDir ?? "";

            UpdateVisualWorldSettings();

            Storyboard WorldSelectedAnimationStoryboard = (Storyboard)FindResource("WorldSelectedAnimation");
            WorldSelectedAnimationStoryboard.Begin();

        }

        private void DeleteWorld_Click(object sender, RoutedEventArgs e)
        {
            ConfirmationBox.Opacity = 1;
            ConfirmationBox.IsHitTestVisible = true;
        }

        private void YesConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectedWorldDirectory = WorldManager.GetSaves().ElementAtOrDefault(WorldListingContainer.SelectedIndex)?.WorldSaveDir ?? "";
            Directory.Delete(SelectedWorldDirectory, true);
            Log.Info($"Deleting world index {WorldListingContainer.SelectedIndex}");

            ConfirmationBox.Opacity = 0;
            ConfirmationBox.IsHitTestVisible = false;
            InitializeWorldListing();
        }

        private void NoConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmationBox.Opacity = 0;
            ConfirmationBox.IsHitTestVisible = false;
            InitializeWorldListing();
        }

        // World settings management (MAY REMOVE THESE)
        private void TBWorldName_Changed(object sender, TextChangedEventArgs e)
        {
            // CHECK THAT THE STRING ISN'T EMPTY, OR THE LAUNCHER WILL CRASH HERE
            SelectedWorldName = TBWorldName.Text;
            Log.Info($"World name set to {SelectedWorldName}");
        }

        private void TBWorldSeed_Changed(object sender, TextChangedEventArgs e)
        {
            SelectedWorldSeed = TBWorldSeed.Text;
            Log.Info($"World seed set to {SelectedWorldSeed}");
        }

        private void RBGamemode_Clicked(object sender, RoutedEventArgs e)
        {
            
        }

        private void CBCheats_Clicked(object sender, RoutedEventArgs e)
        {
            EnableCheatsValue = (bool)CBCheats.IsChecked;
            Log.Info($"DisableConsole set to {EnableCheatsValue}");
        }

        private void CBLanDiscovery_Clicked(object sender, RoutedEventArgs e)
        {
            EnableLanDiscoveryValue = (bool)CBLanDiscovery.IsChecked;
            Log.Info($"LanDiscovery set to {EnableLanDiscoveryValue}");
        }

        private void CBAutoPortForward_Clicked(object sender, RoutedEventArgs e)
        {
            EnableAutoPortForwardValue = (bool)CBAutoPortForward.IsChecked;
            Log.Info($"AutoPortForward set to {EnableAutoPortForwardValue}");
        }

        private void CBCreateFullEntityCache_Clicked(object sender, RoutedEventArgs e)
        {
            EnableFullEntityCacheValue = (bool)CBCreateFullEntityCache.IsChecked;
            Log.Info($"CreateFullEntityCache set to {EnableFullEntityCacheValue}");
        }

        // RUNS EVEN IF TEXT IS CHANGED DURING STARTUP - MUST CHANGE!!! (?)
        private void TBWorldServerPort_Changed(object sender, TextChangedEventArgs e)
        {
            int ServerPortNum = 11000;
            try
            {
                ServerPortNum = Convert.ToInt32(TBWorldServerPort.Text);
            }
            catch
            {
                Log.Info($"ServerPort input not valid");
            }
            
            ServerPort = ServerPortNum;
        }

        private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        // Start server button management
        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            // If the "Start Server button" is clicked and not the "Display Server Console Externally" Checkbox, then start the server
            if (!(e.OriginalSource is CheckBox))
            {
                SaveConfigSettings();
                InitializeWorldListing();

                try
                {
                    LauncherLogic.Server.StartServer(CBIsExternal.IsChecked == true, SelectedWorldDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Storyboard GoBackAnimationStoryboard = (Storyboard)FindResource("GoBackAnimation");
                GoBackAnimationStoryboard.Begin();
            }

        }
    }
}
