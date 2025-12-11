using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Drawing;
using FACTOVA_Execute.Data;

namespace FACTOVA_Execute
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon? _notifyIcon;
        private readonly GeneralSettingsRepository _generalRepository;
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            _generalRepository = new GeneralSettingsRepository();
            
            // 트레이 아이콘 초기화
            InitializeTrayIcon();
            
            // 창 로드 이벤트
            Loaded += MainWindow_Loaded;
            
            // 창 닫기 이벤트
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// ExecuteTabView 런처 새로고침
        /// </summary>
        public void RefreshExecuteTabLauncher()
        {
            // ExecuteTab의 ExecuteTabView 찾기
            var executeTabView = FindVisualChild<Views.ExecuteTabView>(this);
            executeTabView?.RefreshLauncher();
        }

        /// <summary>
        /// 자식 컨트롤 찾기 (재귀)
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// 트레이 아이콘 초기화
        /// </summary>
        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            
            // 임베디드 리소스에서 아이콘 로드
            try
            {
                // WPF 리소스에서 아이콘 로드
                var iconUri = new Uri("pack://application:,,,/Icons/FACTOVA_Execute.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                
                if (streamInfo != null)
                {
                    _notifyIcon.Icon = new Icon(streamInfo.Stream);
                }
                else
                {
                    // 리소스 로드 실패 시 기본 아이콘 사용
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                // 아이콘 로드 실패 시 기본 아이콘 사용
                _notifyIcon.Icon = SystemIcons.Application;
            }
            
            _notifyIcon.Text = "FACTOVA Execute";
            _notifyIcon.Visible = false;

            // 더블클릭으로 창 표시
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                _notifyIcon.Visible = false;
            };

            // 우클릭 메뉴
            var contextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("열기");
            showMenuItem.Click += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                _notifyIcon.Visible = false;
            };
            contextMenu.Items.Add(showMenuItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitMenuItem = new ToolStripMenuItem("종료");
            exitMenuItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitMenuItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// 창 로드 이벤트
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // DB에서 설정 로드
                var settings = _generalRepository.GetSettings();
                
                // 트레이로 실행 옵션이 켜져있으면 창 숨기기
                if (settings.StartInTray)
                {
                    // 창을 즉시 숨기고 트레이로 이동
                    Hide();
                    _notifyIcon.Visible = true;
                    
                    // 트레이 아이콘 풍선 알림
                    _notifyIcon.ShowBalloonTip(
                        3000,
                        "FACTOVA Execute",
                        "프로그램이 트레이에서 실행 중입니다. 아이콘을 더블클릭하여 열 수 있습니다.",
                        ToolTipIcon.Info
                    );
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"설정 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 창 닫기 이벤트
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 트레이 아이콘 제거
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        /// <summary>
        /// 창 상태 변경 시 (최소화 시 트레이로)
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                try
                {
                    var settings = _generalRepository.GetSettings();
                    
                    // 트레이 모드가 활성화되어 있으면 최소화 시 트레이로
                    if (settings.StartInTray)
                    {
                        Hide();
                        _notifyIcon.Visible = true;
                        
                        _notifyIcon.ShowBalloonTip(
                            2000,
                            "FACTOVA Execute",
                            "트레이로 최소화되었습니다.",
                            ToolTipIcon.Info
                        );
                    }
                }
                catch
                {
                    // 설정 로드 실패 시 무시
                }
            }
        }
    }
}
