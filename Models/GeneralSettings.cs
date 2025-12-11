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
    }
}
