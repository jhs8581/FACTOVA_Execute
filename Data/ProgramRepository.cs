using FACTOVA_Execute.Models;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;

namespace FACTOVA_Execute.Data
{
    /// <summary>
    /// 프로그램 정보 리포지토리
    /// </summary>
    public class ProgramRepository
    {
        /// <summary>
        /// 모든 프로그램 목록 조회
        /// </summary>
        public ObservableCollection<ProgramInfo> GetAllPrograms()
        {
            var programs = new ObservableCollection<ProgramInfo>();

            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, IsEnabled, ProgramName, ProgramPath, ProcessName FROM Programs ORDER BY Id";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                programs.Add(new ProgramInfo
                {
                    Id = reader.GetInt32(0),
                    IsEnabled = reader.GetInt32(1) == 1,
                    ProgramName = reader.GetString(2),
                    ProgramPath = reader.GetString(3),
                    ProcessName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                });
            }

            return programs;
        }

        /// <summary>
        /// 프로그램 추가
        /// </summary>
        public void AddProgram(ProgramInfo program)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Programs (IsEnabled, ProgramName, ProgramPath, ProcessName) 
                VALUES (@isEnabled, @programName, @programPath, @processName)";
            command.Parameters.AddWithValue("@isEnabled", program.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@programName", program.ProgramName);
            command.Parameters.AddWithValue("@programPath", program.ProgramPath);
            command.Parameters.AddWithValue("@processName", program.ProcessName ?? string.Empty);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 프로그램 업데이트
        /// </summary>
        public void UpdateProgram(ProgramInfo program)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Programs 
                SET IsEnabled = @isEnabled, 
                    ProgramName = @programName, 
                    ProgramPath = @programPath,
                    ProcessName = @processName
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", program.Id);
            command.Parameters.AddWithValue("@isEnabled", program.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@programName", program.ProgramName);
            command.Parameters.AddWithValue("@programPath", program.ProgramPath);
            command.Parameters.AddWithValue("@processName", program.ProcessName ?? string.Empty);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 프로그램 삭제
        /// </summary>
        public void DeleteProgram(int id)
        {
            using var connection = new SqliteConnection(DatabaseInitializer.ConnectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Programs WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            command.ExecuteNonQuery();
        }
    }
}
