namespace FACTOVA_Execute.Models
{
    /// <summary>
    /// 네트워크 설정 모델
    /// </summary>
    public class NetworkSettings
    {
        public int Id { get; set; }
        public string CheckType { get; set; } = "Ping"; // Ping, HTTP, TCP
        public string TargetAddresses { get; set; } = string.Empty; // 여러 주소를 ; 로 구분 또는 줄바꿈으로 구분
        public int Port { get; set; } = 80; // TCP 체크 시 사용할 포트
        public int TimeoutMs { get; set; } = 3000; // 타임아웃 (밀리초)
        public int CheckIntervalSeconds { get; set; } = 30; // 체크 주기 (초)
        public int RetryDelaySeconds { get; set; } = 10; // 재시도 대기 시간 (초)
        public bool AutoStartPrograms { get; set; } = true; // 연결 시 자동 프로그램 실행
    }
}
