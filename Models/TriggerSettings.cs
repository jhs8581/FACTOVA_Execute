namespace FACTOVA_Execute.Models
{
    /// <summary>
    /// 트리거 설정 모델 (프로세스 감지)
    /// </summary>
    public class TriggerSettings
    {
        public int Id { get; set; }
        
        /// <summary>
        /// 감시할 프로세스 이름 목록 (세미콜론으로 구분)
        /// 예: "FACTOVA.SFC.MainFrame;notepad;calc"
        /// </summary>
        public string TargetProcesses { get; set; } = string.Empty;
        
        /// <summary>
        /// 프로세스 체크 주기 (초)
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 프로세스 감지 시 자동 프로그램 실행 여부
        /// </summary>
        public bool AutoStartPrograms { get; set; } = true;
        
        /// <summary>
        /// 프로세스 목록을 리스트로 반환
        /// </summary>
        public List<string> GetProcessList()
        {
            return TargetProcesses
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }
        
        /// <summary>
        /// 프로세스 리스트를 세미콜론 문자열로 설정
        /// </summary>
        public void SetProcessList(List<string> processes)
        {
            TargetProcesses = string.Join(";", processes.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}
