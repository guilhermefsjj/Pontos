using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pontos.Models;
using Pontos.Security;

namespace Pontos.Services
{
    /// <summary>
    /// Handles persistence of user progress and points data.
    /// Uses JSON files with HMAC signatures for integrity validation.
    /// </summary>
    public class PersistenceService
    {
        private readonly string _dataFolder;
        private readonly string _progressFile;
        private readonly string _pointsFile;
        private readonly IntegrityValidator _validator;

        /// <summary>
        /// Gets the Pontos data folder path (Desktop/Pontos).
        /// </summary>
        public string DataFolder => _dataFolder;

        public PersistenceService()
        {
            _validator = new IntegrityValidator();
            
            // Create Pontos folder on Desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _dataFolder = Path.Combine(desktopPath, "Pontos");
            
            if (!Directory.Exists(_dataFolder))
            {
                Directory.CreateDirectory(_dataFolder);
                Directory.CreateDirectory(Path.Combine(_dataFolder, "images"));
                Directory.CreateDirectory(Path.Combine(_dataFolder, "backups"));
            }

            _progressFile = Path.Combine(_dataFolder, "progress.json");
            _pointsFile = Path.Combine(_dataFolder, "points.json");
        }

        /// <summary>
        /// Loads user progress from disk, validating integrity.
        /// Returns a new progress object with default values if file doesn't exist.
        /// </summary>
        public UserProgress LoadProgress()
        {
            try
            {
                if (!File.Exists(_progressFile))
                {
                    return new UserProgress();
                }

                string json = File.ReadAllText(_progressFile);
                var progress = JsonConvert.DeserializeObject<UserProgress>(json);

                if (progress == null)
                {
                    return new UserProgress();
                }

                // Validate integrity
                if (!_validator.ValidateUserProgress(progress))
                {
                    // Try to recover from backup
                    var backup = LoadProgressFromBackup();
                    if (backup != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[WARNING] Progress file integrity validation failed. Recovered from backup.");
                        return backup;
                    }

                    // If no backup exists, start fresh
                    System.Diagnostics.Debug.WriteLine(
                        "[ERROR] Progress file is corrupted and no backup available. Starting fresh.");
                    return new UserProgress();
                }

                return progress;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load progress: {ex.Message}");
                return new UserProgress();
            }
        }

        /// <summary>
        /// Saves user progress to disk with integrity signature.
        /// Creates a backup of the previous version.
        /// </summary>
        public bool SaveProgress(UserProgress progress)
        {
            try
            {
                if (progress == null)
                    return false;

                // Create backup before saving
                if (File.Exists(_progressFile))
                {
                    string backupPath = Path.Combine(_dataFolder, "backups",
                        $"progress_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                    File.Copy(_progressFile, backupPath, true);
                }

                // Sign the progress before saving
                _validator.SignUserProgress(progress);

                // Serialize with formatting for readability (not encrypted, just signed)
                string json = JsonConvert.SerializeObject(progress, Formatting.Indented);
                File.WriteAllText(_progressFile, json);

                // Clean old backups (keep only last 10)
                CleanOldBackups();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to save progress: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds a new point to the progress and saves it.
        /// </summary>
        public bool AddPoint(UserProgress progress, string? imagePath = null)
        {
            try
            {
                if (progress == null)
                    return false;

                var point = new Point
                {
                    EarnedAt = DateTime.UtcNow,
                    ImagePath = imagePath
                };

                // Sign the point
                _validator.SignPoint(point);

                // Add to progress
                progress.EarnedPoints.Add(point);
                progress.TotalPoints++;
                progress.LastPointEarnedAt = DateTime.UtcNow;

                // Update level
                progress.CurrentLevel = CalculateLevel(progress.TotalPoints);

                // Save
                return SaveProgress(progress);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to add point: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates the user's level based on total points.
        /// Formula: Level = floor(1 + sqrt(TotalPoints / 10))
        /// This means:
        /// - Level 1: 0-9 points
        /// - Level 2: 10-39 points (30 points required)
        /// - Level 3: 40-89 points (50 points required)
        /// - Level 4: 90-159 points (70 points required)
        /// etc.
        /// </summary>
        public static int CalculateLevel(int totalPoints)
        {
            if (totalPoints < 0)
                return 1;

            // Each level requires approximately 10 * level^2 points total
            return (int)(1 + Math.Sqrt(totalPoints / 10.0));
        }

        /// <summary>
        /// Calculates points needed to reach the next level.
        /// </summary>
        public static int PointsToNextLevel(int currentPoints)
        {
            int currentLevel = CalculateLevel(currentPoints);
            int nextLevel = currentLevel + 1;

            // Points needed for next level
            int pointsForNextLevel = (int)(10 * nextLevel * nextLevel);
            int pointsNeeded = Math.Max(0, pointsForNextLevel - currentPoints);

            return pointsNeeded;
        }

        /// <summary>
        /// Gets the path for storing a point's image.
        /// </summary>
        public string GetPointImagePath(string pointUniqueId)
        {
            return Path.Combine(_dataFolder, "images", $"{pointUniqueId}.png");
        }

        private UserProgress? LoadProgressFromBackup()
        {
            try
            {
                string backupDir = Path.Combine(_dataFolder, "backups");
                if (!Directory.Exists(backupDir))
                    return null;

                var files = Directory.GetFiles(backupDir, "progress_backup_*.json");
                if (files.Length == 0)
                    return null;

                // Load the most recent backup
                Array.Sort(files);
                string latestBackup = files[^1];

                string json = File.ReadAllText(latestBackup);
                var progress = JsonConvert.DeserializeObject<UserProgress>(json);

                if (progress != null && _validator.ValidateUserProgress(progress))
                {
                    return progress;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void CleanOldBackups()
        {
            try
            {
                string backupDir = Path.Combine(_dataFolder, "backups");
                if (!Directory.Exists(backupDir))
                    return;

                var files = Directory.GetFiles(backupDir, "progress_backup_*.json");
                if (files.Length > 10)
                {
                    Array.Sort(files);
                    for (int i = 0; i < files.Length - 10; i++)
                    {
                        File.Delete(files[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to clean old backups: {ex.Message}");
            }
        }
    }
}
