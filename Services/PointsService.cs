using System;
using System.Threading;
using System.Threading.Tasks;
using Pontos.Models;

namespace Pontos.Services
{
    /// <summary>
    /// Monitors user activity and awards points when usage time threshold is reached.
    /// Tracks usage time across sessions and handles offline scenarios.
    /// </summary>
    public class PointsService
    {
        // Configuration: 1 point every 10 minutes
        private const int POINTS_INTERVAL_MINUTES = 10;
        private const int CHECK_INTERVAL_MILLISECONDS = 30000; // Check every 30 seconds

        private readonly PersistenceService _persistence;
        private readonly UserActivity _activityMonitor;
        private UserProgress _currentProgress;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _pointCalculationTask;

        /// <summary>
        /// Raised when a new point is earned.
        /// </summary>
        public event EventHandler<PointEarnedEventArgs>? PointEarned;

        /// <summary>
        /// Raised when user progress is updated.
        /// </summary>
        public event EventHandler<UserProgress>? ProgressUpdated;

        public PointsService(PersistenceService persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _activityMonitor = new UserActivity();
            _currentProgress = _persistence.LoadProgress();
        }

        /// <summary>
        /// Gets the current user progress.
        /// </summary>
        public UserProgress GetProgress()
        {
            return _currentProgress;
        }

        /// <summary>
        /// Starts monitoring user activity and awarding points.
        /// </summary>
        public void Start()
        {
            if (_cancellationTokenSource != null)
                return; // Already running

            _activityMonitor.Start();
            _cancellationTokenSource = new CancellationTokenSource();
            _pointCalculationTask = PointCalculationLoop(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops monitoring user activity.
        /// </summary>
        public void Stop()
        {
            if (_cancellationTokenSource == null)
                return;

            _cancellationTokenSource.Cancel();
            _activityMonitor.Stop();
            
            try
            {
                _pointCalculationTask?.Wait(5000);
            }
            catch { /* Ignore timeout */ }
        }

        /// <summary>
        /// Main loop that checks if enough time has passed to award a point.
        /// </summary>
        private async Task PointCalculationLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check if enough time has passed since the last point
                    TimeSpan timeSinceLastPoint = DateTime.UtcNow - _currentProgress.LastPointEarnedAt;
                    
                    if (timeSinceLastPoint.TotalMinutes >= POINTS_INTERVAL_MINUTES)
                    {
                        // Award a point
                        await AwardPoint();
                    }

                    // Wait before checking again
                    await Task.Delay(CHECK_INTERVAL_MILLISECONDS, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Point calculation loop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Awards a new point to the user.
        /// </summary>
        private async Task AwardPoint()
        {
            try
            {
                // Add the point
                _persistence.AddPoint(_currentProgress);
                
                // Reload progress to ensure consistency
                _currentProgress = _persistence.LoadProgress();

                // Notify listeners
                var lastPoint = _currentProgress.EarnedPoints[^1];
                PointEarned?.Invoke(this, new PointEarnedEventArgs
                {
                    Point = lastPoint,
                    TotalPoints = _currentProgress.TotalPoints,
                    CurrentLevel = _currentProgress.CurrentLevel
                });

                ProgressUpdated?.Invoke(this, _currentProgress);

                System.Diagnostics.Debug.WriteLine(
                    $"[INFO] Point awarded! Total: {_currentProgress.TotalPoints}, Level: {_currentProgress.CurrentLevel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to award point: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets minutes until the next point is awarded.
        /// </summary>
        public int GetMinutesUntilNextPoint()
        {
            TimeSpan elapsed = DateTime.UtcNow - _currentProgress.LastPointEarnedAt;
            int minutesRemaining = Math.Max(0, POINTS_INTERVAL_MINUTES - (int)elapsed.TotalMinutes);
            return minutesRemaining;
        }

        /// <summary>
        /// Gets seconds until the next point (for UI timer display).
        /// </summary>
        public int GetSecondsUntilNextPoint()
        {
            TimeSpan elapsed = DateTime.UtcNow - _currentProgress.LastPointEarnedAt;
            int secondsRemaining = Math.Max(0, (POINTS_INTERVAL_MINUTES * 60) - (int)elapsed.TotalSeconds);
            return secondsRemaining;
        }
    }

    /// <summary>
    /// Event arguments for when a point is earned.
    /// </summary>
    public class PointEarnedEventArgs : EventArgs
    {
        public Point? Point { get; set; }
        public int TotalPoints { get; set; }
        public int CurrentLevel { get; set; }
    }
}
