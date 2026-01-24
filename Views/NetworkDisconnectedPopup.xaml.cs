using System.Windows;
using System.Windows.Threading;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// 네트워크 끊김 알림 팝업
    /// </summary>
    public partial class NetworkDisconnectedPopup : Window
    {
        private DateTime _disconnectedTime;
        private DispatcherTimer _updateTimer;

        public NetworkDisconnectedPopup()
        {
            InitializeComponent();
            _disconnectedTime = DateTime.Now;
            
            // 1초마다 끊어진 시간 업데이트
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            UpdateDisconnectedTime();
        }

        /// <summary>
        /// 끊어진 시간 업데이트
        /// </summary>
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateDisconnectedTime();
        }

        /// <summary>
        /// 끊어진 시간 표시 업데이트
        /// </summary>
        private void UpdateDisconnectedTime()
        {
            var elapsed = DateTime.Now - _disconnectedTime;
            DisconnectedTimeTextBlock.Text = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
        }

        /// <summary>
        /// 창 닫을 때 타이머 정리
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer = null;
            base.OnClosed(e);
        }
    }
}
