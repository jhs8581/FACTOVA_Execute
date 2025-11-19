using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FACTOVA_Execute.Services;
using FACTOVA_Execute.Helpers;
using FACTOVA_Execute.Data;
using ICSharpCode.AvalonEdit.Document;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for ExecuteTabView.xaml
    /// </summary>
    public partial class ExecuteTabView : UserControl
    {
        private NetworkMonitorService? _monitorService;
        private readonly GeneralSettingsRepository _generalRepository;
        private bool _isInitialized = false; // 초기화 여부 플래그

        public ExecuteTabView()
        {
            InitializeComponent();
            _generalRepository = new GeneralSettingsRepository();
            InitializeLog();
            Unloaded += ExecuteTabView_Unloaded;
            
            // 프로그램 로드 후 자동 실행 옵션 확인 (한 번만)
            Loaded += ExecuteTabView_Loaded;
        }

        /// <summary>
        /// UserControl 로드 시 (한 번만 실행)
        /// </summary>
        private void ExecuteTabView_Loaded(object sender, RoutedEventArgs e)
        {
            // 이미 초기화되었으면 실행하지 않음
            if (_isInitialized)
                return;

            _isInitialized = true;

            try
            {
                var settings = _generalRepository.GetSettings();
                
                if (settings.AutoStartMonitoring)
                {
                    AddLogMessage("자동 실행 옵션이 활성화되어 있습니다.", NetworkMonitorService.LogLevel.Info);
                    AddLogMessage("자동으로 모니터링을 시작합니다...", NetworkMonitorService.LogLevel.Info);
                    
                    // 자동으로 모니터링 시작
                    StartMonitoring();
                }
                else
                {
                    AddLogMessage("자동 실행 옵션이 비활성화되어 있습니다.", NetworkMonitorService.LogLevel.Info);
                    AddLogMessage("수동으로 모니터링을 시작하려면 '모니터링 시작' 버튼을 클릭하세요.", NetworkMonitorService.LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"초기화 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 로그 초기화
        /// </summary>
        private void InitializeLog()
        {
            LogEditor.Document = new TextDocument();
            LogEditor.Options.EnableHyperlinks = false;
            LogEditor.Options.EnableEmailHyperlinks = false;
            
            // 커스텀 컬러라이저 적용
            LogEditor.TextArea.TextView.LineTransformers.Add(new LogColorizer());
            
            AddLogMessage("프로그램 준비 완료", NetworkMonitorService.LogLevel.Success);
        }

        /// <summary>
        /// 모니터링 시작 (공통 메서드)
        /// </summary>
        private void StartMonitoring()
        {
            try
            {
                _monitorService?.Dispose();
                _monitorService = new NetworkMonitorService();
                _monitorService.LogMessageReceived += OnLogMessageReceived;
                _monitorService.AllProgramsStarted += OnAllProgramsStarted;
                _monitorService.StartMonitoring();

                StartMonitorButton.IsEnabled = false;
                StopMonitorButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AddLogMessage($"모니터링 시작 실패: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 모니터링 시작 버튼 클릭
        /// </summary>
        private void StartMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            StartMonitoring();
        }

        /// <summary>
        /// 모니터링 중지 버튼 클릭
        /// </summary>
        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _monitorService?.StopMonitoring();
                _monitorService?.Dispose();
                _monitorService = null;

                StartMonitorButton.IsEnabled = true;
                StopMonitorButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                AddLogMessage($"모니터링 중지 실패: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 수동 실행 버튼 클릭
        /// </summary>
        private async void ManualStartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("수동으로 프로그램을 실행합니다...", NetworkMonitorService.LogLevel.Info);
                
                var service = new NetworkMonitorService();
                service.LogMessageReceived += OnLogMessageReceived;
                await service.StartEnabledPrograms();
            }
            catch (Exception ex)
            {
                AddLogMessage($"수동 실행 실패: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 로그 지우기 버튼 클릭
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogEditor.Document.Text = string.Empty;
            AddLogMessage("로그를 지웠습니다", NetworkMonitorService.LogLevel.Info);
        }

        /// <summary>
        /// 로그 메시지 수신 이벤트 핸들러
        /// </summary>
        private void OnLogMessageReceived(string message, NetworkMonitorService.LogLevel level)
        {
            Dispatcher.Invoke(() => AddLogMessage(message, level));
        }

        /// <summary>
        /// 모든 프로그램 시작 완료 이벤트 핸들러
        /// </summary>
        private void OnAllProgramsStarted()
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", NetworkMonitorService.LogLevel.Success);
                AddLogMessage("모든 프로그램 실행 완료!", NetworkMonitorService.LogLevel.Success);
                AddLogMessage("모니터링을 자동으로 중지합니다.", NetworkMonitorService.LogLevel.Info);
                AddLogMessage("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", NetworkMonitorService.LogLevel.Success);
                
                // 모니터링 중지
                _monitorService?.StopMonitoring();
                _monitorService?.Dispose();
                _monitorService = null;

                StartMonitorButton.IsEnabled = true;
                StopMonitorButton.IsEnabled = false;
            });
        }

        /// <summary>
        /// 로그 메시지 추가
        /// </summary>
        private void AddLogMessage(string message, NetworkMonitorService.LogLevel level)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            var logLine = $"[{timestamp}] {message}\n";

            var document = LogEditor.Document;
            document.Insert(document.TextLength, logLine);

            // 자동 스크롤
            LogEditor.ScrollToEnd();
        }

        /// <summary>
        /// UserControl 언로드 시
        /// </summary>
        private void ExecuteTabView_Unloaded(object sender, RoutedEventArgs e)
        {
            _monitorService?.Dispose();
        }
    }
}
