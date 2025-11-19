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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
