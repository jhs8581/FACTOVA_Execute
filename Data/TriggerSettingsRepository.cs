using FACTOVA_Execute.Models;
using Microsoft.Data.Sqlite;

namespace FACTOVA_Execute.Data
{
    /// <summary>
    /// 트리거 설정 리포지토리
    /// </summary>
    public class TriggerSettingsRepository
    {
        /// <summary>
        /// 트리거 설정 조회
        /// </summary>
        public TriggerSettings GetSettings()
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, TargetProcesses, CheckIntervalSeconds, AutoStartPrograms 
                                    FROM TriggerSettings LIMIT 1";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new TriggerSettings
                {
                    Id = reader.GetInt32(0),
                    TargetProcesses = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    CheckIntervalSeconds = reader.GetInt32(2),
                    AutoStartPrograms = reader.GetInt32(3) == 1
                };
            }

            // 기본값 반환
            return new TriggerSettings
            {
                TargetProcesses = string.Empty,
                CheckIntervalSeconds = 5,
                AutoStartPrograms = true
            };
        }

        /// <summary>
        /// 트리거 설정 업데이트
        /// </summary>
        public void UpdateSettings(TriggerSettings settings)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE TriggerSettings 
                SET TargetProcesses = @targetProcesses,
                    CheckIntervalSeconds = @checkIntervalSeconds,
                    AutoStartPrograms = @autoStartPrograms
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", settings.Id);
            command.Parameters.AddWithValue("@targetProcesses", settings.TargetProcesses);
            command.Parameters.AddWithValue("@checkIntervalSeconds", settings.CheckIntervalSeconds);
            command.Parameters.AddWithValue("@autoStartPrograms", settings.AutoStartPrograms ? 1 : 0);

            command.ExecuteNonQuery();
        }
    }
}
