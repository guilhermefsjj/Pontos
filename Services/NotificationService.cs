using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Pontos.Models;

namespace Pontos.Services
{
    /// <summary>
    /// Sends native Windows notifications when points are earned.
    /// </summary>
    public class NotificationService
    {
        private readonly string _appId = "Pontos.Application";

        public void NotifyPointEarned(Point point, int totalPoints, int currentLevel)
        {
            try
            {
                // Try to send Windows notification
                SendWindowsNotification(point, totalPoints, currentLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WARNING] Failed to send notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a notification using Windows' native notification system.
        /// Falls back to MessageBox if Windows notifications are not available.
        /// </summary>
        private void SendWindowsNotification(Point point, int totalPoints, int currentLevel)
        {
            try
            {
                // Create notification content
                string title = "Ponto Ganho!";
                string message = $"Você ganhou um ponto!\n" +
                                $"Total: {totalPoints} pontos\n" +
                                $"Nível: {currentLevel}";

                // For now, we use a simple WPF MessageBox
                // A production app would use Windows.UI.Notifications
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

                System.Diagnostics.Debug.WriteLine($"[INFO] Notification sent: {title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ERROR] Failed to send Windows notification: {ex.Message}");
            }
        }
    }
}
