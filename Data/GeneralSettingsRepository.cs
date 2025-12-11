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
            command.CommandText = "SELECT Id, AutoStartMonitoring, StartInTray, LauncherItemsPerRow FROM GeneralSettings LIMIT 1";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new GeneralSettings
                {
                    Id = reader.GetInt32(0),
                    AutoStartMonitoring = reader.GetInt32(1) == 1,
                    StartInTray = reader.GetInt32(2) == 1,
                    LauncherItemsPerRow = reader.GetInt32(3)
                };
            }

            // 기본값 반환
            return new GeneralSettings
            {
                AutoStartMonitoring = true,
                StartInTray = false,
                LauncherItemsPerRow = 5
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
                    LauncherItemsPerRow = @launcherItemsPerRow
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", settings.Id);
            command.Parameters.AddWithValue("@autoStartMonitoring", settings.AutoStartMonitoring ? 1 : 0);
            command.Parameters.AddWithValue("@startInTray", settings.StartInTray ? 1 : 0);
            command.Parameters.AddWithValue("@launcherItemsPerRow", settings.LauncherItemsPerRow);

            command.ExecuteNonQuery();
        }
    }
}
