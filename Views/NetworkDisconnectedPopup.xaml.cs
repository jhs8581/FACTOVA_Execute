using System.Windows;
using System.Windows.Threading;
using FACTOVA_Execute.Data;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// 네트워크 끊김 알림 팝업
    /// </summary>
    public partial class NetworkDisconnectedPopup : Window
    {
        private DateTime _disconnectedTime;
        private DispatcherTimer _updateTimer;
        private readonly NetworkSettingsRepository _networkRepository;
        private string _currentCheckingAddress = "";

        public NetworkDisconnectedPopup()
        {
            InitializeComponent();
            _disconnectedTime = DateTime.Now;
            _networkRepository = new NetworkSettingsRepository();
            
            // 시작 시간 표시
            StartTimeTextBlock.Text = _disconnectedTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            // 점검 중인 네트워크 주소 표시
            UpdateCheckingAddresses();
            
            // 1초마다 끊어진 시간 업데이트
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            UpdateDisconnectedTime();
        }

        /// <summary>
        /// 점검 중인 네트워크 주소 업데이트
        /// </summary>
        private void UpdateCheckingAddresses()
        {
            try
            {
                var settings = _networkRepository.GetSettings();
                var allAddresses = settings.GetAllAddresses();
                
                var addressList = new List<string>();
                
                foreach (var kvp in allAddresses)
                {
                    if (kvp.Value.Any())
                    {
                        addressList.Add($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
                    }
                }
                
                if (addressList.Any())
                {
                    CheckingAddressTextBlock.Text = string.Join("\n", addressList);
                }
                else
                {
                    CheckingAddressTextBlock.Text = Services.LocalizationService.Instance.GetString("NetworkPopup_NoAddress");
                }
            }
            catch
            {
                CheckingAddressTextBlock.Text = Services.LocalizationService.Instance.GetString("NetworkPopup_AddressFailed");
            }
        }

        /// <summary>
        /// 현재 확인 중인 주소 업데이트 (외부에서 호출)
        /// </summary>
        public void UpdateCurrentCheckingAddress(string address)
        {
            _currentCheckingAddress = address;
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(address))
                {
                    CheckingAddressTextBlock.Text = $"▶ {address} 확인 중...";
                }
            });
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
            DisconnectedTimeTextBlock.Text = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
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
