using System.ComponentModel;

namespace FACTOVA_Execute.Models
{
    /// <summary>
    /// 프로그램 정보 모델
    /// </summary>
    public class ProgramInfo : INotifyPropertyChanged
    {
        private int _id;
        private bool _isEnabled;
        private string _programName = string.Empty;
        private string _programPath = string.Empty;
        private string _processName = string.Empty; // 모니터링할 프로세스명
        private string _executionMode = "Network"; // 실행 모드: Network 또는 Trigger
        private int _executionOrder = 1; // 실행 순서
        private bool _isFolder = false; // 폴더 여부

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public string ProgramName
        {
            get => _programName;
            set
            {
                _programName = value;
                OnPropertyChanged(nameof(ProgramName));
            }
        }

        public string ProgramPath
        {
            get => _programPath;
            set
            {
                _programPath = value;
                OnPropertyChanged(nameof(ProgramPath));
            }
        }

        /// <summary>
        /// 모니터링할 프로세스명 (확장자 제외)
        /// 예: FACTOVA.Updater.exe → FACTOVA.Updater
        /// </summary>
        public string ProcessName
        {
            get => _processName;
            set
            {
                _processName = value;
                OnPropertyChanged(nameof(ProcessName));
            }
        }

        /// <summary>
        /// 실행 모드: "Network" (네트워크 연결 시) 또는 "Trigger" (프로세스 감지 시)
        /// </summary>
        public string ExecutionMode
        {
            get => _executionMode;
            set
            {
                _executionMode = value;
                OnPropertyChanged(nameof(ExecutionMode));
            }
        }

        /// <summary>
        /// 실행 순서 (숫자가 작을수록 먼저 실행)
        /// </summary>
        public int ExecutionOrder
        {
            get => _executionOrder;
            set
            {
                _executionOrder = value;
                OnPropertyChanged(nameof(ExecutionOrder));
            }
        }

        /// <summary>
        /// 폴더 여부 (true: 폴더, false: 프로그램)
        /// </summary>
        public bool IsFolder
        {
            get => _isFolder;
            set
            {
                _isFolder = value;
                OnPropertyChanged(nameof(IsFolder));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
