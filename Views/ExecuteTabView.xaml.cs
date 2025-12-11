using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FACTOVA_Execute.Services;
using FACTOVA_Execute.Helpers;
using FACTOVA_Execute.Data;
using ICSharpCode.AvalonEdit.Document;
using System.Diagnostics;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for ExecuteTabView.xaml
    /// </summary>
    public partial class ExecuteTabView : UserControl
    {
        private NetworkMonitorService? _networkMonitorService;
        private ProcessMonitorService? _processMonitorService;
        private readonly GeneralSettingsRepository _generalRepository;
        private readonly TriggerSettingsRepository _triggerRepository;
        private readonly ProgramRepository _programRepository;
        private bool _isInitialized = false; // 초기화 여부 플래그

        public ExecuteTabView()
        {
            InitializeComponent();
            _generalRepository = new GeneralSettingsRepository();
            _triggerRepository = new TriggerSettingsRepository();
            _programRepository = new ProgramRepository();
            InitializeLog();
            Unloaded += ExecuteTabView_Unloaded;
            
            // 프로그램 로드 후 자동 실행 옵션 확인 (한 번만)
            Loaded += ExecuteTabView_Loaded;
            
            // LauncherPanel이 크기 변경될 때마다 런처 다시 로드
            LauncherPanel.SizeChanged += (s, e) =>
            {
                if (e.WidthChanged && LauncherPanel.ActualWidth > 0)
                {
                    LoadLauncher();
                }
            };
        }

        /// <summary>
        /// UserControl 로드 시 (한 번만 실행)
        /// </summary>
        private void ExecuteTabView_Loaded(object sender, RoutedEventArgs e)
        {
            // 런처 로드 (크기가 확정된 후)
            LoadLauncher();
            
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
        /// 런처 새로고침 (외부에서 호출 가능)
        /// </summary>
        public void RefreshLauncher()
        {
            Dispatcher.Invoke(() => LoadLauncher());
        }

        /// <summary>
        /// 런처 로드
        /// </summary>
        private void LoadLauncher()
        {
            try
            {
                LauncherPanel.Children.Clear();
                
                var settings = _generalRepository.GetSettings();
                var itemsPerRow = settings.LauncherItemsPerRow;
                
                var programs = _programRepository.GetAllPrograms()
                    .Where(p => p.IsEnabled)
                    .ToList();

                if (programs.Count == 0)
                {
                    var noItemsText = new TextBlock
                    {
                        Text = "등록된 프로그램이 없습니다.\n'설정 > 프로그램 설정'에서 프로그램을 추가하고 '사용'을 체크하세요.",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20)
                    };
                    LauncherPanel.Children.Add(noItemsText);
                    return;
                }

                // 실제 사용 가능한 너비 계산
                var scrollViewer = FindParent<ScrollViewer>(LauncherPanel);
                var availableWidth = scrollViewer?.ActualWidth ?? LauncherPanel.ActualWidth;
                
                if (availableWidth <= 0)
                {
                    availableWidth = 800; // 기본값
                }

                // 여백 계산 (양쪽 10px + 버튼 간격)
                var totalMargin = 20 + (itemsPerRow - 1) * 10;
                var buttonWidth = (availableWidth - totalMargin - 20) / itemsPerRow; // 스크롤바 여유 20px
                
                if (buttonWidth < 100) 
                {
                    buttonWidth = 100; // 최소 너비
                }

                // 버튼 생성 및 추가
                for (int i = 0; i < programs.Count; i++)
                {
                    var program = programs[i];
                    
                    // 버튼 내용: 아이콘 + 텍스트 (가로 배치)
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // 아이콘 추출 및 추가
                    var iconSource = Helpers.IconExtractor.ExtractIconFromFile(program.ProgramPath);
                    if (iconSource != null)
                    {
                        var iconImage = new System.Windows.Controls.Image
                        {
                            Source = iconSource,
                            Width = 32,
                            Height = 32,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        stackPanel.Children.Add(iconImage);
                    }

                    // 프로그램명 텍스트
                    var textBlock = new TextBlock
                    {
                        Text = program.ProgramName,
                        TextAlignment = TextAlignment.Left,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        MaxWidth = buttonWidth - 60 // 아이콘과 여백 제외
                    };
                    stackPanel.Children.Add(textBlock);

                    // 버튼 생성
                    var button = new Button
                    {
                        Content = stackPanel,
                        Style = (Style)FindResource("LauncherButtonStyle"),
                        Width = buttonWidth,
                        Tag = program
                    };
                    button.Click += LauncherButton_Click;
                    LauncherPanel.Children.Add(button);

                    // 행별로 줄바꿈 강제 (itemsPerRow 개수마다)
                    if ((i + 1) % itemsPerRow == 0 && i < programs.Count - 1)
                    {
                        // 줄바꿈을 위한 더미 요소 추가
                        LauncherPanel.Children.Add(new Border { Width = availableWidth, Height = 0 });
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"런처 로드 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 부모 컨트롤 찾기
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T typedParent) return typedParent;
            return FindParent<T>(parent);
        }

        /// <summary>
        /// 런처 버튼 클릭 이벤트
        /// </summary>
        private async void LauncherButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Models.ProgramInfo program)
            {
                try
                {
                    AddLogMessage($"프로그램 실행: {program.ProgramName}", NetworkMonitorService.LogLevel.Info);

                    // 프로세스 중복 확인
                    if (!string.IsNullOrWhiteSpace(program.ProcessName))
                    {
                        var existingProcess = Process.GetProcessesByName(program.ProcessName);
                        if (existingProcess.Length > 0)
                        {
                            AddLogMessage($"이미 실행 중입니다: {program.ProgramName} (프로세스: {program.ProcessName})", NetworkMonitorService.LogLevel.Warning);
                            return;
                        }
                    }

                    // 프로그램 실행
                    if (System.IO.File.Exists(program.ProgramPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = program.ProgramPath,
                            UseShellExecute = true
                        });
                        
                        await Task.Delay(1000); // 1초 대기
                        AddLogMessage($"실행 완료: {program.ProgramName}", NetworkMonitorService.LogLevel.Success);
                    }
                    else
                    {
                        AddLogMessage($"프로그램 파일을 찾을 수 없습니다: {program.ProgramPath}", NetworkMonitorService.LogLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"실행 오류 ({program.ProgramName}): {ex.Message}", NetworkMonitorService.LogLevel.Error);
                }
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
                // 기존 서비스 정리
                _networkMonitorService?.Dispose();
                _processMonitorService?.Dispose();
                _networkMonitorService = null;
                _processMonitorService = null;

                // 항상 네트워크 모니터링부터 시작
                _networkMonitorService = new NetworkMonitorService();
                _networkMonitorService.LogMessageReceived += OnLogMessageReceived;
                _networkMonitorService.AllProgramsStarted += OnNetworkProgramsCompleted; // Network 완료 후 Trigger 시작
                _networkMonitorService.StartMonitoring();

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
                _networkMonitorService?.StopMonitoring();
                _networkMonitorService?.Dispose();
                _networkMonitorService = null;

                _processMonitorService?.StopMonitoring();
                _processMonitorService?.Dispose();
                _processMonitorService = null;

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
        /// 로그 메시지 수신 이벤트 핸들러 (NetworkMonitorService용)
        /// </summary>
        private void OnLogMessageReceived(string message, NetworkMonitorService.LogLevel level)
        {
            Dispatcher.Invoke(() => AddLogMessage(message, level));
        }

        /// <summary>
        /// 로그 메시지 수신 이벤트 핸들러 (ProcessMonitorService용)
        /// </summary>
        private void OnProcessLogMessageReceived(string message, ProcessMonitorService.LogLevel level)
        {
            // ProcessMonitorService.LogLevel을 NetworkMonitorService.LogLevel로 변환
            var networkLevel = level switch
            {
                ProcessMonitorService.LogLevel.Info => NetworkMonitorService.LogLevel.Info,
                ProcessMonitorService.LogLevel.Warning => NetworkMonitorService.LogLevel.Warning,
                ProcessMonitorService.LogLevel.Error => NetworkMonitorService.LogLevel.Error,
                ProcessMonitorService.LogLevel.Success => NetworkMonitorService.LogLevel.Success,
                _ => NetworkMonitorService.LogLevel.Info
            };
            Dispatcher.Invoke(() => AddLogMessage(message, networkLevel));
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
                _networkMonitorService?.StopMonitoring();
                _networkMonitorService?.Dispose();
                _networkMonitorService = null;

                _processMonitorService?.StopMonitoring();
                _processMonitorService?.Dispose();
                _processMonitorService = null;

                StartMonitorButton.IsEnabled = true;
                StopMonitorButton.IsEnabled = false;
            });
        }

        /// <summary>
        /// Network 모드 프로그램 시작 완료 이벤트 핸들러
        /// </summary>
        private void OnNetworkProgramsCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", NetworkMonitorService.LogLevel.Success);
                AddLogMessage("네트워크 연결 실행 완료!", NetworkMonitorService.LogLevel.Success);
                AddLogMessage("프로그램 감지 모니터링을 시작합니다...", NetworkMonitorService.LogLevel.Info);
                AddLogMessage("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", NetworkMonitorService.LogLevel.Success);
                
                // 네트워크 모니터링 중지
                _networkMonitorService?.StopMonitoring();
                _networkMonitorService?.Dispose();
                _networkMonitorService = null;

                // 프로세스 모니터링 시작
                try
                {
                    _processMonitorService = new ProcessMonitorService();
                    _processMonitorService.LogMessageReceived += OnProcessLogMessageReceived;
                    _processMonitorService.AllProgramsStarted += OnAllProgramsStarted;
                    _processMonitorService.StartMonitoring();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"프로그램 감지 모니터링 시작 실패: {ex.Message}", NetworkMonitorService.LogLevel.Error);
                }
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
            _networkMonitorService?.Dispose();
            _processMonitorService?.Dispose();
        }
    }
}
