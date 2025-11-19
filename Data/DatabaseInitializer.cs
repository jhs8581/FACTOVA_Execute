using Microsoft.Data.Sqlite;
using System.IO;

namespace FACTOVA_Execute.Data
{
    /// <summary>
    /// 데이터베이스 초기화 및 관리
    /// </summary>
    public static class DatabaseInitializer
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FACTOVA_Execute.db");
        public static string ConnectionString => $"Data Source={DbPath}";

        /// <summary>
        /// 데이터베이스 초기화
        /// </summary>
        public static void Initialize()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // Programs 테이블 생성
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Programs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    ProgramName TEXT NOT NULL,
                    ProgramPath TEXT NOT NULL,
                    ProcessName TEXT NOT NULL DEFAULT ''
                );
            ";
            command.ExecuteNonQuery();

            // NetworkSettings 테이블 생성
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS NetworkSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CheckType TEXT NOT NULL DEFAULT 'Ping',
                    TargetAddresses TEXT NOT NULL,
                    Port INTEGER NOT NULL DEFAULT 80,
                    TimeoutMs INTEGER NOT NULL DEFAULT 3000,
                    CheckIntervalSeconds INTEGER NOT NULL DEFAULT 30,
                    RetryDelaySeconds INTEGER NOT NULL DEFAULT 10,
                    AutoStartPrograms INTEGER NOT NULL DEFAULT 1
                );
            ";
            command.ExecuteNonQuery();

            // GeneralSettings 테이블 생성
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS GeneralSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AutoStartMonitoring INTEGER NOT NULL DEFAULT 1,
                    StartInTray INTEGER NOT NULL DEFAULT 0
                );
            ";
            command.ExecuteNonQuery();

            // 기존 테이블 마이그레이션
            MigrateProgramsTable(connection);
            MigrateNetworkSettings(connection);

            // 기본 프로그램 데이터 삽입 (없을 경우에만)
            InitializeDefaultPrograms(connection);
            
            // 기본 네트워크 설정 삽입 (없을 경우에만)
            InitializeDefaultNetworkSettings(connection);
            
            // 기본 일반 설정 삽입 (없을 경우에만)
            InitializeDefaultGeneralSettings(connection);
        }

        /// <summary>
        /// Programs 테이블 마이그레이션
        /// </summary>
        private static void MigrateProgramsTable(SqliteConnection connection)
        {
            try
            {
                // 기존 컬럼 확인
                var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(Programs)";
                var reader = command.ExecuteReader();
                
                var columns = new List<string>();
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1)); // 컬럼명
                }
                reader.Close();

                // ProcessName 컬럼이 없으면 추가
                if (!columns.Contains("ProcessName"))
                {
                    command.CommandText = "ALTER TABLE Programs ADD COLUMN ProcessName TEXT NOT NULL DEFAULT ''";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Programs 테이블 마이그레이션 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 네트워크 설정 테이블 마이그레이션
        /// </summary>
        private static void MigrateNetworkSettings(SqliteConnection connection)
        {
            try
            {
                // 기존 컬럼 확인
                var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(NetworkSettings)";
                var reader = command.ExecuteReader();
                
                var columns = new List<string>();
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1)); // 컬럼명
                }
                reader.Close();

                // TargetAddresses 컬럼이 없으면 마이그레이션 필요
                if (!columns.Contains("TargetAddresses"))
                {
                    // 기존 데이터 백업
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS NetworkSettings_backup AS 
                        SELECT * FROM NetworkSettings";
                    command.ExecuteNonQuery();

                    // 기존 테이블 삭제
                    command.CommandText = "DROP TABLE IF EXISTS NetworkSettings";
                    command.ExecuteNonQuery();

                    // 새 구조로 테이블 재생성
                    command.CommandText = @"
                        CREATE TABLE NetworkSettings (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            CheckType TEXT NOT NULL DEFAULT 'Ping',
                            TargetAddresses TEXT NOT NULL,
                            Port INTEGER NOT NULL DEFAULT 80,
                            TimeoutMs INTEGER NOT NULL DEFAULT 3000,
                            CheckIntervalSeconds INTEGER NOT NULL DEFAULT 30,
                            RetryDelaySeconds INTEGER NOT NULL DEFAULT 10,
                            AutoStartPrograms INTEGER NOT NULL DEFAULT 1
                        );
                    ";
                    command.ExecuteNonQuery();

                    // 백업에서 데이터 복원 시도
                    try
                    {
                        command.CommandText = @"
                            INSERT INTO NetworkSettings 
                            (CheckType, TargetAddresses, Port, TimeoutMs, CheckIntervalSeconds, RetryDelaySeconds, AutoStartPrograms)
                            SELECT 
                                CheckType, 
                                COALESCE(TargetAddress, '165.186.55.129;10.162.190.1;165.186.47.1;10.162.35.1'), 
                                Port, 
                                TimeoutMs, 
                                CheckIntervalSeconds,
                                10,
                                1
                            FROM NetworkSettings_backup";
                        command.ExecuteNonQuery();
                    }
                    catch
                    {
                        // 복원 실패시 무시 (InitializeDefaultNetworkSettings에서 처리)
                    }

                    // 백업 테이블 삭제
                    command.CommandText = "DROP TABLE IF EXISTS NetworkSettings_backup";
                    command.ExecuteNonQuery();
                }
                // RetryDelaySeconds 또는 AutoStartPrograms 컬럼이 없으면 추가
                else if (!columns.Contains("RetryDelaySeconds") || !columns.Contains("AutoStartPrograms"))
                {
                    if (!columns.Contains("RetryDelaySeconds"))
                    {
                        command.CommandText = "ALTER TABLE NetworkSettings ADD COLUMN RetryDelaySeconds INTEGER NOT NULL DEFAULT 10";
                        command.ExecuteNonQuery();
                    }
                    if (!columns.Contains("AutoStartPrograms"))
                    {
                        command.CommandText = "ALTER TABLE NetworkSettings ADD COLUMN AutoStartPrograms INTEGER NOT NULL DEFAULT 1";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"마이그레이션 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 기본 프로그램 4개를 초기화
        /// </summary>
        private static void InitializeDefaultPrograms(SqliteConnection connection)
        {
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Programs";
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (count == 0)
            {
                // 개별 INSERT로 변경하여 안정성 향상
                InsertProgram(connection, "SFC Update", @"C:\Program Files (x86)\GMES Shop Floor Control for LGE\FACTOVA.Updater.exe", "FACTOVA.Updater");
                InsertProgram(connection, "SFC MainFrame", @"C:\Program Files (x86)\GMES Shop Floor Control for LGE\FACTOVA.SFC.MainFrame.exe", "FACTOVA.SFC.MainFrame");
                InsertProgram(connection, "EIF", @"C:\LGCNS.ezControl\BIN_2.0\LGE.FactoryLync2.0.exe", "LGE.FactoryLync2.0");
                InsertProgram(connection, "EIF Agent", @"C:\LGE.EA2.0\LGE.GMES2.EA.Executor.exe", "LGE.GMES2.EA.Executor");
            }
        }

        /// <summary>
        /// 개별 프로그램 삽입
        /// </summary>
        private static void InsertProgram(SqliteConnection connection, string programName, string programPath, string processName)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Programs (IsEnabled, ProgramName, ProgramPath, ProcessName) VALUES (@isEnabled, @programName, @programPath, @processName)";
            command.Parameters.AddWithValue("@isEnabled", 1);
            command.Parameters.AddWithValue("@programName", programName);
            command.Parameters.AddWithValue("@programPath", programPath);
            command.Parameters.AddWithValue("@processName", processName);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 기본 네트워크 설정 초기화
        /// </summary>
        private static void InitializeDefaultNetworkSettings(SqliteConnection connection)
        {
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM NetworkSettings";
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (count == 0)
            {
                // 배치파일과 동일한 게이트웨이 순서
                var defaultAddresses = "165.186.55.129;10.162.190.1;165.186.47.1;10.162.35.1";
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO NetworkSettings (CheckType, TargetAddresses, Port, TimeoutMs, CheckIntervalSeconds, RetryDelaySeconds, AutoStartPrograms) 
                    VALUES (@checkType, @targetAddresses, @port, @timeoutMs, @checkIntervalSeconds, @retryDelaySeconds, @autoStartPrograms)";
                command.Parameters.AddWithValue("@checkType", "Ping");
                command.Parameters.AddWithValue("@targetAddresses", defaultAddresses);
                command.Parameters.AddWithValue("@port", 80);
                command.Parameters.AddWithValue("@timeoutMs", 3000);
                command.Parameters.AddWithValue("@checkIntervalSeconds", 30);
                command.Parameters.AddWithValue("@retryDelaySeconds", 10);
                command.Parameters.AddWithValue("@autoStartPrograms", 1);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 기본 일반 설정 초기화
        /// </summary>
        private static void InitializeDefaultGeneralSettings(SqliteConnection connection)
        {
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM GeneralSettings";
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (count == 0)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO GeneralSettings (AutoStartMonitoring, StartInTray) 
                    VALUES (@autoStartMonitoring, @startInTray)";
                command.Parameters.AddWithValue("@autoStartMonitoring", 1);
                command.Parameters.AddWithValue("@startInTray", 0);
                command.ExecuteNonQuery();
            }
        }
    }
}
