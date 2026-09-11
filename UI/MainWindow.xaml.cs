using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Pontos.Models;
using Pontos.Network;
using Pontos.Services;

namespace Pontos
{
    public partial class MainWindow : Window
    {
        private PersistenceService _persistence;
        private PointsService _pointsService;
        private ImageService _imageService;
        private NotificationService _notificationService;
        private NetworkManager _networkManager;
        private DispatcherTimer _uiUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
        }

        private void InitializeServices()
        {
            try
            {
                _persistence = new PersistenceService();
                _pointsService = new PointsService(_persistence);
                _imageService = new ImageService(_persistence);
                _notificationService = new NotificationService();

                // Initialize network manager
                var progress = _pointsService.GetProgress();
                _networkManager = new NetworkManager(progress);
                _networkManager.PeersUpdated += NetworkManager_PeersUpdated;

                // Setup UI update timer
                _uiUpdateTimer = new DispatcherTimer();
                _uiUpdateTimer.Interval = TimeSpan.FromSeconds(1);
                _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

                // Wire up events
                _pointsService.PointEarned += PointsService_PointEarned;
                _pointsService.ProgressUpdated += PointsService_ProgressUpdated;

                // Start services
                _pointsService.Start();
                _uiUpdateTimer.Start();

                // Initial UI update
                UpdateUI();

                System.Diagnostics.Debug.WriteLine("[INFO] Services initialized successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao inicializar aplicação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[ERROR] Initialization failed: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            var progress = _pointsService.GetProgress();

            // Update points and level
            PointsDisplay.Text = progress.TotalPoints.ToString();
            LevelDisplay.Text = progress.CurrentLevel.ToString();

            // Update progress bar
            int pointsToNextLevel = PersistenceService.PointsToNextLevel(progress.TotalPoints);
            int pointsAtCurrentLevel = PersistenceService.CalculateLevel(progress.TotalPoints - 1) != progress.CurrentLevel
                ? progress.TotalPoints
                : progress.TotalPoints % 10;

            LevelProgressBar.Maximum = 100;
            LevelProgressBar.Value = Math.Min(100, (pointsAtCurrentLevel * 100) / 10);
            ProgressTextBlock.Text = $"{progress.TotalPoints} / {progress.TotalPoints + pointsToNextLevel} pontos";

            // Update timer
            int secondsRemaining = _pointsService.GetSecondsUntilNextPoint();
            int minutes = secondsRemaining / 60;
            int seconds = secondsRemaining % 60;
            TimerDisplay.Text = $"{minutes:D2}:{seconds:D2}";

            // Update history
            UpdatePointsHistory(progress);
        }

        private void UpdatePointsHistory(UserProgress progress)
        {
            PointsHistoryList.ItemsSource = progress.EarnedPoints
                .OrderByDescending(p => p.EarnedAt)
                .Take(10)
                .ToList();
        }

        private void PointsService_PointEarned(object sender, PointEarnedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _notificationService.NotifyPointEarned(e.Point, e.TotalPoints, e.CurrentLevel);
                UpdateUI();

                // Try to download image
                _ = DownloadPointImageAsync(e.Point);
            });
        }

        private async System.Threading.Tasks.Task DownloadPointImageAsync(Point point)
        {
            try
            {
                string imagePath = await _imageService.DownloadImageForPointAsync(point);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    // Save the updated progress with image path
                    var progress = _pointsService.GetProgress();
                    _persistence.SaveProgress(progress);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to download image: {ex.Message}");
            }
        }

        private void PointsService_ProgressUpdated(object sender, UserProgress e)
        {
            Dispatcher.Invoke(() => UpdateUI());
        }

        private void NetworkManager_PeersUpdated(object sender, ObservableCollection<NetworkPeer> peers)
        {
            Dispatcher.Invoke(() =>
            {
                PeersList.ItemsSource = peers;
            });
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void NetworkModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_networkManager.EnableNetworkMode())
                {
                    NetworkStatusText.Text = "Rede ativada - Descobrindo computadores...";
                    NetworkStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    NetworkModeToggle.IsChecked = false;
                    NetworkStatusText.Text = "Falha ao ativar rede";
                    NetworkStatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ativar modo rede: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                NetworkModeToggle.IsChecked = false;
            }
        }

        private void NetworkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _networkManager.DisableNetworkMode();
            NetworkStatusText.Text = "Rede desativada";
            NetworkStatusText.Foreground = System.Windows.Media.Brushes.Gray;
            PeersList.ItemsSource = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _uiUpdateTimer?.Stop();
                _pointsService?.Stop();
                _networkManager?.DisableNetworkMode();
                _imageService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error during shutdown: {ex.Message}");
            }

            base.OnClosed(e);
        }
    }
}
