using System.IO;

namespace FACTOVA_Execute.Services
{
    /// <summary>
    /// 네트워크 연결/끊김 로그 전용 서비스
    /// </summary>
    public class NetworkLogService
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string LogFileName = "NetworkStatus.log";
        private static readonly object _lockObject = new object();

        public NetworkLogService()
        {
            // 로그 디렉토리 생성
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        /// <summary>
        /// 로그 파일 경로
        /// </summary>
        public string GetLogFilePath()
        {
            return Path.Combine(LogDirectory, LogFileName);
        }

        /// <summary>
        /// 네트워크 연결 이벤트 기록
        /// </summary>
        public void LogConnected()
        {
            WriteLog("네트워크 연결됨");
        }

        /// <summary>
        /// 네트워크 끊김 이벤트 기록
        /// </summary>
        public void LogDisconnected()
        {
            WriteLog("네트워크 연결 끊김");
        }

        /// <summary>
        /// 네트워크 모니터링 시작 이벤트 기록
        /// </summary>
        public void LogMonitoringStarted()
        {
            WriteLog("네트워크 상태 감지 시작");
        }

        /// <summary>
        /// 네트워크 모니터링 중지 이벤트 기록
        /// </summary>
        public void LogMonitoringStopped()
        {
            WriteLog("네트워크 상태 감지 중지");
        }

        /// <summary>
        /// 로그 파일에 기록
        /// </summary>
        private void WriteLog(string message)
        {
            try
            {
                lock (_lockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logLine = $"[{timestamp}] {message}{Environment.NewLine}";
                    
                    File.AppendAllText(GetLogFilePath(), logLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"네트워크 로그 기록 실패: {ex.Message}");
            }
        }
    }
}
