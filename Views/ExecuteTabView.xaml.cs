using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using FACTOVA_Execute.Services;
using FACTOVA_Execute.Helpers;
using FACTOVA_Execute.Data;
using ICSharpCode.AvalonEdit.Document;
using System.Diagnostics;
using Microsoft.Win32;

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

        // 드래그 앤 드롭 관련 필드
        private Button? _draggedButton;
        private Point _dragStartPoint;
        private bool _isDragging = false;

        public ExecuteTabView()
        {
            InitializeComponent();
            _generalRepository = new GeneralSettingsRepository();
            _triggerRepository = new TriggerSettingsRepository();
            _programRepository = new ProgramRepository();
            InitializeLog();
            Unloaded += ExecuteTabView_Unloaded;
            
            // programas 로드 후 자동 실행 옵션 확인 (한 번만)
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
                var viewMode = settings.LauncherViewMode;
                
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

                // 여백 계산
                var totalMargin = 20 + (itemsPerRow - 1) * 10;
                var buttonWidth = (availableWidth - totalMargin - 20) / itemsPerRow;
                
                if (buttonWidth < 100) 
                {
                    buttonWidth = 100;
                }

                // 보기 모드에 따라 다르게 렌더링
                if (viewMode == "Group")
                {
                    LoadLauncherGroupView(programs, buttonWidth, availableWidth, itemsPerRow);
                }
                else
                {
                    LoadLauncherGridView(programs, buttonWidth, availableWidth, itemsPerRow);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"런처 로드 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        /// <summary>
        /// 그리드 보기 (기본)
        /// </summary>
        private void LoadLauncherGridView(List<Models.ProgramInfo> programs, double buttonWidth, double availableWidth, int itemsPerRow)
        {
            for (int i = 0; i < programs.Count; i++)
            {
                var program = programs[i];
                var button = CreateLauncherButton(program, buttonWidth);
                LauncherPanel.Children.Add(button);

                // 행별로 줄바꿈
                if ((i + 1) % itemsPerRow == 0 && i < programs.Count - 1)
                {
                    LauncherPanel.Children.Add(new Border { Width = availableWidth, Height = 0 });
                }
            }
        }

        /// <summary>
        /// 그룹별 보기 (타입별 Expander)
        /// </summary>
        private void LoadLauncherGroupView(List<Models.ProgramInfo> programs, double buttonWidth, double availableWidth, int itemsPerRow)
        {
            // ExecutionMode별로 그룹화
            var groups = programs.GroupBy(p => p.ExecutionMode).OrderBy(g => GetGroupOrder(g.Key));

            foreach (var group in groups)
            {
                var groupName = GetGroupDisplayName(group.Key);
                var groupPrograms = group.OrderBy(p => p.ExecutionOrder).ToList();

                // Expander 생성
                var expander = new Expander
                {
                    Header = $"{groupName} ({groupPrograms.Count}개)",
                    IsExpanded = true,
                    Margin = new Thickness(0, 0, 0, 15),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Width = availableWidth - 20,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                };

                // Expander 내용 (WrapPanel)
                // WrapPanel의 너비를 정확하게 계산: (buttonWidth + margin) * itemsPerRow
                var wrapPanelWidth = (buttonWidth + 10) * itemsPerRow;
                
                var wrapPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Width = wrapPanelWidth
                };

                for (int i = 0; i < groupPrograms.Count; i++)
                {
                    var program = groupPrograms[i];
                    var button = CreateLauncherButton(program, buttonWidth);
                    wrapPanel.Children.Add(button);
                }

                expander.Content = wrapPanel;
                
                // Expander를 감싸는 Border 추가 (배경 및 테두리)
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = expander
                };

                LauncherPanel.Children.Add(border);
            }
        }

        /// <summary>
        /// 런처 버튼 생성
        /// </summary>
        private Button CreateLauncherButton(Models.ProgramInfo program, double buttonWidth)
        {
            // 버튼 내용: Grid로 아이콘과 텍스트 영역 분리
            var grid = new Grid
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };

            // 컬럼 정의: 아이콘(고정 50px) + 텍스트(나머지)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 아이콘 가져오기 (커스텀 아이콘 우선, 없으면 프로그램에서 추출)
            var iconSource = GetProgramIcon(program);
            if (iconSource != null)
            {
                var iconImage = new System.Windows.Controls.Image
                {
                    Source = iconSource,
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconImage, 0);
                grid.Children.Add(iconImage);
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
                Margin = new Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            // 컨텍스트 메뉴 생성
            var contextMenu = CreateButtonContextMenu(program);

            // 버튼 생성
            var button = new Button
            {
                Content = grid,
                Style = (Style)FindResource("LauncherButtonStyle"),
                Width = buttonWidth,
                Tag = program,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Padding = new Thickness(5),
                ContextMenu = contextMenu
            };
            button.Click += LauncherButton_Click;

            // 드래그 앤 드롭 이벤트 연결
            button.PreviewMouseLeftButtonDown += LauncherButton_PreviewMouseLeftButtonDown;
            button.PreviewMouseMove += LauncherButton_PreviewMouseMove;
            button.PreviewMouseLeftButtonUp += LauncherButton_PreviewMouseLeftButtonUp;

            return button;
        }

        /// <summary>
        /// 프로그램 아이콘 가져오기 (커스텀 아이콘 우선)
        /// </summary>
        private ImageSource? GetProgramIcon(Models.ProgramInfo program)
        {
            // 커스텀 아이콘 경로가 있으면 먼저 시도
            if (!string.IsNullOrWhiteSpace(program.IconPath) && System.IO.File.Exists(program.IconPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(program.IconPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    // 커스텀 아이콘 로드 실패 시 기본 아이콘으로 폴백
                }
            }

            // 기본: 프로그램 파일에서 아이콘 추출
            return Helpers.IconExtractor.ExtractIconFromFile(program.ProgramPath);
        }

        /// <summary>
        /// 버튼 컨텍스트 메뉴 생성
        /// </summary>
        private ContextMenu CreateButtonContextMenu(Models.ProgramInfo program)
        {
            var contextMenu = new ContextMenu();

            // 경로 열기 메뉴
            var openFolderItem = new MenuItem
            {
                Header = "📂 파일 위치 열기",
                Tag = program
            };
            openFolderItem.Click += ContextMenu_OpenFolder_Click;
            contextMenu.Items.Add(openFolderItem);

            // 경로 복사 메뉴
            var copyPathItem = new MenuItem
            {
                Header = "📋 경로 복사",
                Tag = program
            };
            copyPathItem.Click += ContextMenu_CopyPath_Click;
            contextMenu.Items.Add(copyPathItem);

            // 구분선
            contextMenu.Items.Add(new Separator());

            // 아이콘 변경 메뉴
            var changeIconItem = new MenuItem
            {
                Header = "🖼 아이콘 변경",
                Tag = program
            };
            changeIconItem.Click += ContextMenu_ChangeIcon_Click;
            contextMenu.Items.Add(changeIconItem);

            // 아이콘 초기화 메뉴 (커스텀 아이콘이 설정된 경우에만 표시)
            if (!string.IsNullOrWhiteSpace(program.IconPath))
            {
                var resetIconItem = new MenuItem
                {
                    Header = "🔄 아이콘 초기화",
                    Tag = program
                };
                resetIconItem.Click += ContextMenu_ResetIcon_Click;
                contextMenu.Items.Add(resetIconItem);
            }

            return contextMenu;
        }

        /// <summary>
        /// 컨텍스트 메뉴 - 파일 위치 열기
        /// </summary>
        private void ContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Models.ProgramInfo program)
            {
                try
                {
                    var path = program.ProgramPath;
                    
                    if (program.IsFolder)
                    {
                        // 폴더인 경우 해당 폴더 열기
                        if (System.IO.Directory.Exists(path))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = path,
                                UseShellExecute = true
                            });
                            AddLogMessage($"폴더 열기: {path}", NetworkMonitorService.LogLevel.Info);
                        }
                        else
                        {
                            AddLogMessage($"폴더를 찾을 수 없습니다: {path}", NetworkMonitorService.LogLevel.Error);
                        }
                    }
                    else
                    {
                        // 파일인 경우 파일이 있는 폴더를 열고 파일 선택
                        if (System.IO.File.Exists(path))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{path}\"",
                                UseShellExecute = true
                            });
                            AddLogMessage($"파일 위치 열기: {path}", NetworkMonitorService.LogLevel.Info);
                        }
                        else
                        {
                            AddLogMessage($"파일을 찾을 수 없습니다: {path}", NetworkMonitorService.LogLevel.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"경로 열기 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// 컨텍스트 메뉴 - 경로 복사
        /// </summary>
        private void ContextMenu_CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Models.ProgramInfo program)
            {
                try
                {
                    Clipboard.SetText(program.ProgramPath);
                    AddLogMessage($"경로가 클립보드에 복사되었습니다: {program.ProgramPath}", NetworkMonitorService.LogLevel.Info);
                }
                catch (Exception ex)
                {
                    AddLogMessage($"경로 복사 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// 컨텍스트 메뉴 - 아이콘 변경
        /// </summary>
        private void ContextMenu_ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Models.ProgramInfo program)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "아이콘 이미지 선택",
                    Filter = "이미지 파일 (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|아이콘 파일 (*.ico)|*.ico|PNG 파일 (*.png)|*.png|모든 파일 (*.*)|*.*",
                    InitialDirectory = string.IsNullOrEmpty(program.IconPath)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                        : System.IO.Path.GetDirectoryName(program.IconPath)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        program.IconPath = openFileDialog.FileName;
                        _programRepository.UpdateProgram(program);
                        
                        AddLogMessage($"'{program.ProgramName}' 아이콘이 변경되었습니다.", NetworkMonitorService.LogLevel.Info);
                        
                        // 런처 새로고침
                        LoadLauncher();
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"아이콘 변경 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 컨텍스트 메뉴 - 아이콘 초기화
        /// </summary>
        private void ContextMenu_ResetIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Models.ProgramInfo program)
            {
                try
                {
                    program.IconPath = string.Empty;
                    _programRepository.UpdateProgram(program);
                    
                    AddLogMessage($"'{program.ProgramName}' 아이콘이 초기화되었습니다.", NetworkMonitorService.LogLevel.Info);
                    
                    // 런처 새로고침
                    LoadLauncher();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"아이콘 초기화 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
                }
            }
        }

        #region 드래그 앤 드롭 이벤트 핸들러

        /// <summary>
        /// 버튼 마우스 왼쪽 버튼 누름
        /// </summary>
        private void LauncherButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedButton = button;
            }
        }

        /// <summary>
        /// 버튼 마우스 이동
        /// </summary>
        private void LauncherButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedButton != null && !_isDragging)
            {
                var currentPosition = e.GetPosition(null);
                var diff = _dragStartPoint - currentPosition;

                // 최소 이동 거리 확인
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    // 드래그 시작 시 시각적 피드백
                    _draggedButton.Opacity = 0.5;

                    // 드래그 데이터 설정
                    var data = new DataObject("LauncherButton", _draggedButton);
                    DragDrop.DoDragDrop(_draggedButton, data, DragDropEffects.Move);

                    // 드래그 완료 후 복원
                    if (_draggedButton != null)
                    {
                        _draggedButton.Opacity = 1.0;
                    }
                    _isDragging = false;
                    _draggedButton = null;
                }
            }
        }

        /// <summary>
        /// 버튼 마우스 왼쪽 버튼 뗌
        /// </summary>
        private void LauncherButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedButton = null;
            _isDragging = false;
        }

        /// <summary>
        /// 런처 패널 드래그 오버
        /// </summary>
        private void LauncherPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherButton"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 런처 패널 드롭
        /// </summary>
        private void LauncherPanel_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("LauncherButton"))
                return;

            var draggedButton = e.Data.GetData("LauncherButton") as Button;
            if (draggedButton == null)
                return;

            var draggedProgram = draggedButton.Tag as Models.ProgramInfo;
            if (draggedProgram == null)
                return;

            // 드롭 위치에서 타겟 버튼 찾기
            var dropPosition = e.GetPosition(LauncherPanel);
            Button? targetButton = null;
            int targetIndex = -1;

            // 현재 보기 모드 확인
            var settings = _generalRepository.GetSettings();
            var viewMode = settings.LauncherViewMode;

            if (viewMode == "Group")
            {
                // 그룹 보기 모드: 같은 그룹 내에서만 이동
                targetButton = FindTargetButtonInGroupView(dropPosition, draggedProgram.ExecutionMode, out targetIndex);
            }
            else
            {
                // 그리드 보기 모드
                targetButton = FindTargetButtonInGridView(dropPosition, out targetIndex);
            }

            if (targetButton == null || targetButton == draggedButton)
                return;

            var targetProgram = targetButton.Tag as Models.ProgramInfo;
            if (targetProgram == null)
                return;

            // 그룹 보기 모드에서 다른 그룹으로 이동 방지
            if (viewMode == "Group" && draggedProgram.ExecutionMode != targetProgram.ExecutionMode)
            {
                AddLogMessage("같은 그룹 내에서만 순서를 변경할 수 있습니다.", NetworkMonitorService.LogLevel.Warning);
                return;
            }

            // 순서 업데이트
            UpdateProgramOrders(draggedProgram, targetProgram);

            e.Handled = true;
        }

        /// <summary>
        /// 그리드 보기에서 타겟 버튼 찾기
        /// </summary>
        private Button? FindTargetButtonInGridView(Point dropPosition, out int targetIndex)
        {
            targetIndex = -1;
            
            for (int i = 0; i < LauncherPanel.Children.Count; i++)
            {
                if (LauncherPanel.Children[i] is Button button)
                {
                    var buttonPosition = button.TranslatePoint(new Point(0, 0), LauncherPanel);
                    var buttonRect = new Rect(buttonPosition, new Size(button.ActualWidth, button.ActualHeight));

                    if (buttonRect.Contains(dropPosition))
                    {
                        targetIndex = i;
                        return button;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 그룹 보기에서 타겟 버튼 찾기
        /// </summary>
        private Button? FindTargetButtonInGroupView(Point dropPosition, string executionMode, out int targetIndex)
        {
            targetIndex = -1;

            // 모든 Expander 내의 WrapPanel 탐색
            foreach (var child in LauncherPanel.Children)
            {
                if (child is Border border && border.Child is Expander expander && expander.Content is WrapPanel wrapPanel)
                {
                    for (int i = 0; i < wrapPanel.Children.Count; i++)
                    {
                        if (wrapPanel.Children[i] is Button button && button.Tag is Models.ProgramInfo program)
                        {
                            if (program.ExecutionMode == executionMode)
                            {
                                var buttonPosition = button.TranslatePoint(new Point(0, 0), LauncherPanel);
                                var buttonRect = new Rect(buttonPosition, new Size(button.ActualWidth, button.ActualHeight));

                                if (buttonRect.Contains(dropPosition))
                                {
                                    targetIndex = i;
                                    return button;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 프로그램 순서 업데이트 (스왑 방식)
        /// </summary>
        private void UpdateProgramOrders(Models.ProgramInfo draggedProgram, Models.ProgramInfo targetProgram)
        {
            try
            {
                // 두 프로그램의 ExecutionOrder를 서로 스왑
                var tempOrder = draggedProgram.ExecutionOrder;
                draggedProgram.ExecutionOrder = targetProgram.ExecutionOrder;
                targetProgram.ExecutionOrder = tempOrder;

                // DB 업데이트
                _programRepository.UpdateProgramOrders(new List<Models.ProgramInfo> { draggedProgram, targetProgram });

                AddLogMessage($"'{draggedProgram.ProgramName}' ↔ '{targetProgram.ProgramName}' 순서가 교환되었습니다.", NetworkMonitorService.LogLevel.Info);

                // 런처 새로고침
                LoadLauncher();
            }
            catch (Exception ex)
            {
                AddLogMessage($"순서 변경 오류: {ex.Message}", NetworkMonitorService.LogLevel.Error);
            }
        }

        #endregion

        /// <summary>
        /// 그룹 표시명 가져오기
        /// </summary>
        private string GetGroupDisplayName(string executionMode)
        {
            return executionMode switch
            {
                "Launcher" => "🚀 런처 전용",
                "Network" => "🌐 네트워크 연결 실행",
                "Trigger" => "🔔 프로그램 감지 실행",
                _ => executionMode
            };
        }

        /// <summary>
        /// 그룹 정렬 순서
        /// </summary>
        private int GetGroupOrder(string executionMode)
        {
            return executionMode switch
            {
                "Launcher" => 1,  // 런처 전용이 맨 위
                "Network" => 2,
                "Trigger" => 3,
                _ => 99
            };
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
                    // 폴더인 경우 탐색기로 열기
                    if (program.IsFolder)
                    {
                        AddLogMessage($"폴더 열기: {program.ProgramName}", NetworkMonitorService.LogLevel.Info);
                        
                        if (System.IO.Directory.Exists(program.ProgramPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = program.ProgramPath,
                                UseShellExecute = true
                            });
                            
                            AddLogMessage($"폴더 열기 완료: {program.ProgramName}", NetworkMonitorService.LogLevel.Success);
                        }
                        else
                        {
                            AddLogMessage($"폴더를 찾을 수 없습니다: {program.ProgramPath}", NetworkMonitorService.LogLevel.Error);
                        }
                        return;
                    }

                    // 프로그램인 경우 기존 로직
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
        /// 로그인 초기화
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
                AddLogMessage("모니터링을 중지합니다...", NetworkMonitorService.LogLevel.Warning);
                
                _networkMonitorService?.StopMonitoring();
                _networkMonitorService?.Dispose();
                _networkMonitorService = null;

                _processMonitorService?.StopMonitoring();
                _processMonitorService?.Dispose();
                _processMonitorService = null;

                StartMonitorButton.IsEnabled = true;
                StopMonitorButton.IsEnabled = false;
                
                AddLogMessage("모니터링이 중지되었습니다.", NetworkMonitorService.LogLevel.Info);
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
