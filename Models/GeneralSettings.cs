namespace FACTOVA_Execute.Models
{
    /// <summary>
    /// 일반 설정 모델
    /// </summary>
    public class GeneralSettings
    {
        public int Id { get; set; }
        public bool AutoStartMonitoring { get; set; } = true; // 연결 시 자동 실행 (네트워크 모니터링 자동 시작)
        public bool StartInTray { get; set; } = false; // 트레이로 실행
        public int LauncherItemsPerRow { get; set; } = 5; // 런처 행별 개수
        
        /// <summary>
        /// 런처 보기 모드: "Grid" (기본 그리드) 또는 "Group" (타입별 그룹)
        /// </summary>
        public string LauncherViewMode { get; set; } = "Grid";
        
        /// <summary>
        /// 네트워크 상태 감지 활성화
        /// </summary>
        public bool EnableNetworkMonitoring { get; set; } = false;
        
        /// <summary>
        /// 네트워크 상태 감지 주기 (초)
        /// </summary>
        public int NetworkCheckIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 언어 설정: "ko-KR" (한국어) 또는 "en-US" (영어)
        /// </summary>
        public string Language { get; set; } = "ko-KR";
    }
}
