using System.IO;
using FACTOVA_Execute.Data;

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
        private readonly NetworkSettingsRepository _networkRepository;
        private readonly GeneralSettingsRepository _generalRepository;
        private DateTime? _lastDisconnectedTime;

        public NetworkLogService()
        {
            _networkRepository = new NetworkSettingsRepository();
            _generalRepository = new GeneralSettingsRepository();
            
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
        public void LogConnected(string? connectedAddress = null, string? checkType = null)
        {
            var details = new List<string>();
            
            // 끊김 시간이 있으면 "복구", 없으면 "연결됨"
            if (_lastDisconnectedTime.HasValue)
            {
                var duration = DateTime.Now - _lastDisconnectedTime.Value;
                details.Add("네트워크 연결 복구");
                details.Add($"끊김 지속 시간: {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}");
                _lastDisconnectedTime = null;
            }
            else
            {
                details.Add("네트워크 연결됨");
            }
            
            if (!string.IsNullOrEmpty(connectedAddress))
            {
                details.Add($"연결 주소: {connectedAddress}");
            }
            
            if (!string.IsNullOrEmpty(checkType))
            {
                details.Add($"체크 방식: {checkType}");
            }
            
            WriteLog(string.Join(" | ", details));
            
            // 연결 복구 후 감지 재시작 로그
            WriteLog("네트워크 상태 감지 계속 진행 중");
        }

        /// <summary>
        /// 네트워크 끊김 이벤트 기록
        /// </summary>
        public void LogDisconnected()
        {
            _lastDisconnectedTime = DateTime.Now;
            
            var settings = _networkRepository.GetSettings();
            var allAddresses = settings.GetAllAddresses();
            
            var details = new List<string> { "네트워크 연결 끊김" };
            
            // 점검 중인 주소 목록 추가
            foreach (var kvp in allAddresses)
            {
                if (kvp.Value.Any())
                {
                    details.Add($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
                }
            }
            
            WriteLog(string.Join(" | ", details));
        }

        /// <summary>
        /// 네트워크 체크 결과 기록
        /// </summary>
        public void LogCheckResult(string checkType, string address, bool success, string? errorMessage = null)
        {
            var status = success ? "성공" : "실패";
            var message = $"[{checkType}] {address} - {status}";
            
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                message += $" ({errorMessage})";
            }
            
            WriteLog(message);
        }

        /// <summary>
        /// 네트워크 모니터링 시작 이벤트 기록
        /// </summary>
        public void LogMonitoringStarted()
        {
            var networkSettings = _networkRepository.GetSettings();
            var generalSettings = _generalRepository.GetSettings();
            var allAddresses = networkSettings.GetAllAddresses();
            
            var details = new List<string>
            {
                "네트워크 상태 감지 시작",
                $"감지 주기: {generalSettings.NetworkCheckIntervalSeconds}초",
                $"타임아웃: {networkSettings.TimeoutMs}ms"
            };
            
            // 등록된 주소 목록
            foreach (var kvp in allAddresses)
            {
                if (kvp.Value.Any())
                {
                    details.Add($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
                }
            }
            
            WriteLog(string.Join(" | ", details));
        }

        /// <summary>
        /// 네트워크 모니터링 중지 이벤트 기록
        /// </summary>
        public void LogMonitoringStopped()
        {
            WriteLog("네트워크 상태 감지 중지");
        }

        /// <summary>
        /// 프로그램 실행 기록
        /// </summary>
        public void LogProgramStarted(string programName, string programPath, bool success, string? errorMessage = null)
        {
            var status = success ? "실행 성공" : "실행 실패";
            var message = $"프로그램 {status}: {programName} ({programPath})";
            
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                message += $" - {errorMessage}";
            }
            
            WriteLog(message);
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
