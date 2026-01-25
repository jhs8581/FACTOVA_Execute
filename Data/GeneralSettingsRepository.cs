using FACTOVA_Execute.Models;
using Microsoft.Data.Sqlite;

namespace FACTOVA_Execute.Data
{
    /// <summary>
    /// 일반 설정 리포지토리
    /// </summary>
    public class GeneralSettingsRepository
    {
        /// <summary>
        /// 일반 설정 조회
        /// </summary>
        public GeneralSettings GetSettings()
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, AutoStartMonitoring, StartInTray, LauncherItemsPerRow, LauncherViewMode, EnableNetworkMonitoring, NetworkCheckIntervalSeconds, Language FROM GeneralSettings LIMIT 1";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new GeneralSettings
                {
                    Id = reader.GetInt32(0),
                    AutoStartMonitoring = reader.GetInt32(1) == 1,
                    StartInTray = reader.GetInt32(2) == 1,
                    LauncherItemsPerRow = reader.GetInt32(3),
                    LauncherViewMode = reader.IsDBNull(4) ? "Grid" : reader.GetString(4),
                    EnableNetworkMonitoring = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                    NetworkCheckIntervalSeconds = reader.IsDBNull(6) ? 5 : reader.GetInt32(6),
                    Language = reader.IsDBNull(7) ? "ko-KR" : reader.GetString(7)
                };
            }

            // 기본값 반환
            return new GeneralSettings
            {
                AutoStartMonitoring = true,
                StartInTray = false,
                LauncherItemsPerRow = 5,
                LauncherViewMode = "Grid",
                EnableNetworkMonitoring = false,
                NetworkCheckIntervalSeconds = 5,
                Language = "ko-KR"
            };
        }

        /// <summary>
        /// 일반 설정 업데이트
        /// </summary>
        public void UpdateSettings(GeneralSettings settings)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE GeneralSettings 
                SET AutoStartMonitoring = @autoStartMonitoring,
                    StartInTray = @startInTray,
                    LauncherItemsPerRow = @launcherItemsPerRow,
                    LauncherViewMode = @launcherViewMode,
                    EnableNetworkMonitoring = @enableNetworkMonitoring,
                    NetworkCheckIntervalSeconds = @networkCheckIntervalSeconds,
                    Language = @language
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", settings.Id);
            command.Parameters.AddWithValue("@autoStartMonitoring", settings.AutoStartMonitoring ? 1 : 0);
            command.Parameters.AddWithValue("@startInTray", settings.StartInTray ? 1 : 0);
            command.Parameters.AddWithValue("@launcherItemsPerRow", settings.LauncherItemsPerRow);
            command.Parameters.AddWithValue("@launcherViewMode", settings.LauncherViewMode);
            command.Parameters.AddWithValue("@enableNetworkMonitoring", settings.EnableNetworkMonitoring ? 1 : 0);
            command.Parameters.AddWithValue("@networkCheckIntervalSeconds", settings.NetworkCheckIntervalSeconds);
            command.Parameters.AddWithValue("@language", settings.Language);

            command.ExecuteNonQuery();
        }
    }
}
