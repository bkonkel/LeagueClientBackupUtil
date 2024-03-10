using System;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Windows;

using System.Windows.Shapes;
using Path = System.IO.Path;
using Microsoft.Win32;

namespace LeagueClientBackupUtil
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly string backupFolderPath;
        private readonly string leagueClientPath = @"C:\Riot Games\League of Legends";
        private readonly string leagueConfigPath = @"C:\Riot Games\League of Legends\Config";
        private readonly string leagueDataCfgPath = @"C:\Riot Games\League of Legends\DATA\CFG";

        public MainWindow()
        {
            InitializeComponent();
            backupFolderPath = Path.Combine(appDataPath, "Kemukujara Technologies", "Backups");
            Directory.CreateDirectory(backupFolderPath); // Ensure the backup folder exists
        }

        private void Backup_Button_Click(object sender, RoutedEventArgs e)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupFileName = $"LoLBackup_{timestamp}.zip";
            string destinationZipPath = Path.Combine(backupFolderPath, backupFileName);

            BackupLeagueData(destinationZipPath);
        }

        private void BackupLeagueData(string destinationZipPath)
        {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(tempDirectoryPath);
                Debug.WriteLine($"Temporary directory created at: {tempDirectoryPath}");

                // Copy Config and CFG directories to the temporary directory
                CopyDirectory(leagueConfigPath, Path.Combine(tempDirectoryPath, "Config"));
                CopyDirectory(leagueDataCfgPath, Path.Combine(tempDirectoryPath, "CFG"));

                // Create the ZIP file
                ZipFile.CreateFromDirectory(tempDirectoryPath, destinationZipPath);
                Debug.WriteLine($"Backup completed successfully. File saved to: {destinationZipPath}");

                MessageBox.Show($"Backup completed successfully.\nFile saved to: {destinationZipPath}", "Backup Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during backup: {ex.Message}");
                MessageBox.Show($"Error during backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Directory.Exists(tempDirectoryPath))
                {
                    Directory.Delete(tempDirectoryPath, true);
                    Debug.WriteLine("Temporary directory cleaned up.");
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string tempPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(tempPath, false);
            }

            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destinationDir, subdir.Name);
                CopyDirectory(subdir.FullName, tempPath);
            }
        }

        private bool CheckAndCloseLeagueClient()
        {
            foreach (var process in Process.GetProcessesByName("LeagueClient"))
            {
                var response = MessageBox.Show("League of Legends client is running. It needs to be closed before restoring. Close now?", "Close League Client", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (response == MessageBoxResult.Yes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(); // Wait for the process to close
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to close League of Legends client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return true; // Process is not running or was successfully closed
        }
        private string GetLatestBackupOrLetUserChoose()
        {
            var backupFiles = Directory.GetFiles(backupFolderPath, "LoLBackup_*.zip").OrderByDescending(f => f).ToList();
            if (!backupFiles.Any())
            {
                MessageBox.Show("No backups found. Please create a backup first.", "No Backups", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            var latestBackup = backupFiles.First();
            var latestBackupTime = File.GetCreationTime(latestBackup);
            var userFriendlyDate = latestBackupTime.ToString("dd/MM/yyyy 'at' h:mm tt");

            var result = MessageBox.Show($"Your latest backup is from {userFriendlyDate}. Do you want to use this backup?", "Use Latest Backup?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                return latestBackup;
            }
            else
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.InitialDirectory = backupFolderPath;
                openFileDialog.Filter = "Zip files (*.zip)|*.zip";
                if (openFileDialog.ShowDialog() == true)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }

        private void CreateRecoveryBackup()
        {
            string recoveryBackupPath = Path.Combine(backupFolderPath, "LoLRecoveryBackup.zip");
            // Delete existing recovery backup if it exists
            if (File.Exists(recoveryBackupPath))
            {
                File.Delete(recoveryBackupPath);
            }
            // Use your existing backup logic here, focusing on the relevant League directories
            BackupLeagueData(recoveryBackupPath);
        }

        private void RestoreBackup(string backupFilePath)
        {
            try
            {
                string leagueInstallPath = leagueClientPath.Substring(0, leagueClientPath.LastIndexOf('\\')); // Assuming leagueClientPath includes the "League of Legends" directory
                ZipFile.ExtractToDirectory(backupFilePath, leagueInstallPath, true);
                MessageBox.Show("Restoration completed successfully.", "Restoration Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during restoration: {ex.Message}");
                MessageBox.Show($"Error during restoration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Restore_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAndCloseLeagueClient())
            {
                MessageBox.Show("Operation cancelled. League of Legends client is still running.", "Operation Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a recovery backup before proceeding with restoration
            CreateRecoveryBackup();

            var backupFilePath = GetLatestBackupOrLetUserChoose();
            if (string.IsNullOrEmpty(backupFilePath))
            {
                // User cancelled operation or no backups exist
                return;
            }

            RestoreBackup(backupFilePath);
        }
    }
}

