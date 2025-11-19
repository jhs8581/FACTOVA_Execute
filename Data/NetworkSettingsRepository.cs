using FACTOVA_Execute.Models;
using Microsoft.Data.Sqlite;

namespace FACTOVA_Execute.Data
{
    /// <summary>
    /// 네트워크 설정 리포지토리
    /// </summary>
    public class NetworkSettingsRepository
    {
        /// <summary>
        /// 네트워크 설정 조회
        /// </summary>
        public NetworkSettings GetSettings()
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, CheckType, PingAddresses, HttpAddresses, TcpAddresses, 
                                    Port, TimeoutMs, CheckIntervalSeconds, RetryDelaySeconds, AutoStartPrograms 
                                    FROM NetworkSettings LIMIT 1";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new NetworkSettings
                {
                    Id = reader.GetInt32(0),
                    CheckType = reader.GetString(1),
                    PingAddresses = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    HttpAddresses = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    TcpAddresses = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Port = reader.GetInt32(5),
                    TimeoutMs = reader.GetInt32(6),
                    CheckIntervalSeconds = reader.GetInt32(7),
                    RetryDelaySeconds = reader.GetInt32(8),
                    AutoStartPrograms = reader.GetInt32(9) == 1
                };
            }

            // 기본값 반환
            return new NetworkSettings
            {
                CheckType = "Ping",
                PingAddresses = "165.186.55.129;10.162.190.1;165.186.47.1;10.162.35.1",
                HttpAddresses = "http://google.com;http://naver.com",
                TcpAddresses = "165.186.55.129;10.162.190.1",
                Port = 80,
                TimeoutMs = 3000,
                CheckIntervalSeconds = 30,
                RetryDelaySeconds = 10,
                AutoStartPrograms = true
            };
        }

        /// <summary>
        /// 네트워크 설정 업데이트
        /// </summary>
        public void UpdateSettings(NetworkSettings settings)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE NetworkSettings 
                SET CheckType = @checkType, 
                    PingAddresses = @pingAddresses,
                    HttpAddresses = @httpAddresses,
                    TcpAddresses = @tcpAddresses,
                    Port = @port, 
                    TimeoutMs = @timeoutMs, 
                    CheckIntervalSeconds = @checkIntervalSeconds,
                    RetryDelaySeconds = @retryDelaySeconds,
                    AutoStartPrograms = @autoStartPrograms
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", settings.Id);
            command.Parameters.AddWithValue("@checkType", settings.CheckType);
            command.Parameters.AddWithValue("@pingAddresses", settings.PingAddresses);
            command.Parameters.AddWithValue("@httpAddresses", settings.HttpAddresses);
            command.Parameters.AddWithValue("@tcpAddresses", settings.TcpAddresses);
            command.Parameters.AddWithValue("@port", settings.Port);
            command.Parameters.AddWithValue("@timeoutMs", settings.TimeoutMs);
            command.Parameters.AddWithValue("@checkIntervalSeconds", settings.CheckIntervalSeconds);
            command.Parameters.AddWithValue("@retryDelaySeconds", settings.RetryDelaySeconds);
            command.Parameters.AddWithValue("@autoStartPrograms", settings.AutoStartPrograms ? 1 : 0);

            command.ExecuteNonQuery();
        }
    }
}
