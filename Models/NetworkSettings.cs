namespace FACTOVA_Execute.Models
{
    /// <summary>
    /// 네트워크 설정 모델
    /// </summary>
    public class NetworkSettings
    {
        public int Id { get; set; }
        public string CheckType { get; set; } = "Ping"; // UI에서 현재 선택된 타입 (저장용)
        
        // 각 체크 타입별 주소 저장
        public string PingAddresses { get; set; } = string.Empty;
        public string HttpAddresses { get; set; } = string.Empty;
        public string TcpAddresses { get; set; } = string.Empty;
        
        [Obsolete("Use PingAddresses, HttpAddresses, or TcpAddresses instead")]
        public string TargetAddresses { get; set; } = string.Empty; // 하위 호환성을 위해 유지
        
        public int Port { get; set; } = 80; // TCP 체크 시 사용할 포트
        public int TimeoutMs { get; set; } = 3000; // 타임아웃 (밀리초)
        public int CheckIntervalSeconds { get; set; } = 30; // 체크 주기 (초)
        public int RetryDelaySeconds { get; set; } = 10; // 재시도 대기 시간 (초)
        public bool AutoStartPrograms { get; set; } = true; // 연결 시 자동 프로그램 실행
        public bool UseSequentialCheck { get; set; } = true; // Ping → HTTP → TCP 순차 체크 활성화
        
        /// <summary>
        /// 현재 선택된 체크 타입에 맞는 주소 반환 (UI용)
        /// </summary>
        public string GetCurrentAddresses()
        {
            return CheckType switch
            {
                "Ping" => PingAddresses,
                "HTTP" => HttpAddresses,
                "TCP" => TcpAddresses,
                _ => PingAddresses
            };
        }
        
        /// <summary>
        /// 현재 선택된 체크 타입에 주소 설정 (UI용)
        /// </summary>
        public void SetCurrentAddresses(string addresses)
        {
            switch (CheckType)
            {
                case "Ping":
                    PingAddresses = addresses;
                    break;
                case "HTTP":
                    HttpAddresses = addresses;
                    break;
                case "TCP":
                    TcpAddresses = addresses;
                    break;
            }
        }

        /// <summary>
        /// 모든 체크 타입의 주소를 가져옴 (실행용)
        /// </summary>
        public Dictionary<string, List<string>> GetAllAddresses()
        {
            return new Dictionary<string, List<string>>
            {
                ["Ping"] = ParseAddresses(PingAddresses),
                ["HTTP"] = ParseAddresses(HttpAddresses),
                ["TCP"] = ParseAddresses(TcpAddresses)
            };
        }

        private List<string> ParseAddresses(string addresses)
        {
            return addresses
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
}
