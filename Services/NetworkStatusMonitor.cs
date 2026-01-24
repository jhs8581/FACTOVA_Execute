using System.Net.NetworkInformation;
using System.Net.Http;
using System.Windows;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Views;

namespace FACTOVA_Execute.Services
{
    /// <summary>
    /// 네트워크 상태 모니터링 서비스 (일반 설정 기반)
    /// </summary>
    public class NetworkStatusMonitor
    {
        private readonly GeneralSettingsRepository _generalRepository;
        private readonly NetworkSettingsRepository _networkRepository;
        private readonly NetworkLogService _logService;
        private System.Timers.Timer? _monitorTimer;
        private bool _isNetworkConnected = true;
        private NetworkDisconnectedPopup? _popup;
        private bool _isMonitoring = false;

        public NetworkStatusMonitor()
        {
            _generalRepository = new GeneralSettingsRepository();
            _networkRepository = new NetworkSettingsRepository();
            _logService = new NetworkLogService();
        }

        /// <summary>
        /// 네트워크 상태 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            var settings = _generalRepository.GetSettings();
            
            if (!settings.EnableNetworkMonitoring)
                return;

            _isMonitoring = true;
            _logService.LogMonitoringStarted();

            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();

            _monitorTimer = new System.Timers.Timer(settings.NetworkCheckIntervalSeconds * 1000);
            _monitorTimer.Elapsed += async (s, e) => await CheckNetworkStatus();
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();

            // 즉시 한번 체크
            Task.Run(async () => await CheckNetworkStatus());
        }

        /// <summary>
        /// 네트워크 상태 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _logService.LogMonitoringStopped();

            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            // 열려있는 팝업 닫기
            ClosePopup();
        }

        /// <summary>
        /// 네트워크 상태 확인
        /// </summary>
        private async Task CheckNetworkStatus()
        {
            try
            {
                bool isConnected = await IsNetworkAvailable();

                System.Diagnostics.Debug.WriteLine($"[NetworkStatusMonitor] 체크 결과: isConnected={isConnected}, _isNetworkConnected={_isNetworkConnected}");

                // 연결 끊김 감지
                if (!isConnected && _isNetworkConnected)
                {
                    _isNetworkConnected = false;
                    _logService.LogDisconnected();
                    System.Diagnostics.Debug.WriteLine("[NetworkStatusMonitor] 연결 끊김 → 팝업 표시");
                    ShowDisconnectedPopup();
                }
                // 연결 복구 감지
                else if (isConnected && !_isNetworkConnected)
                {
                    _isNetworkConnected = true;
                    _logService.LogConnected();
                    System.Diagnostics.Debug.WriteLine("[NetworkStatusMonitor] 연결 복구 → 팝업 닫힘");
                    ClosePopup();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"네트워크 상태 확인 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 네트워크 연결 여부 확인 (네트워크 설정 탭의 주소 사용)
        /// </summary>
        private async Task<bool> IsNetworkAvailable()
        {
            try
            {
                var networkSettings = _networkRepository.GetSettings();
                var allAddresses = networkSettings.GetAllAddresses();

                // 디버그: 등록된 주소 확인
                var pingCount = allAddresses["Ping"].Count;
                var httpCount = allAddresses["HTTP"].Count;
                var tcpCount = allAddresses["TCP"].Count;
                System.Diagnostics.Debug.WriteLine($"[NetworkStatusMonitor] 등록된 주소: Ping={pingCount}, HTTP={httpCount}, TCP={tcpCount}");

                // 모든 주소가 비어있으면 연결 안됨으로 처리
                if (pingCount == 0 && httpCount == 0 && tcpCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[NetworkStatusMonitor] 등록된 주소가 없음 → 연결 안됨으로 처리");
                    return false;
                }

                // Ping → HTTP → TCP 순서로 확인
                foreach (var kvp in allAddresses)
                {
                    var checkType = kvp.Key;
                    var addresses = kvp.Value;

                    if (!addresses.Any())
                        continue;

                    foreach (var address in addresses)
                    {
                        bool result = false;
                        try
                        {
                            switch (checkType)
                            {
                                case "Ping":
                                    result = await TestPingAsync(address, networkSettings.TimeoutMs);
                                    break;
                                case "HTTP":
                                    result = await TestHttpAsync(address, networkSettings.TimeoutMs);
                                    break;
                                case "TCP":
                                    result = await TestTcpAsync(address, networkSettings.Port, networkSettings.TimeoutMs);
                                    break;
                            }
                            System.Diagnostics.Debug.WriteLine($"[NetworkStatusMonitor] {checkType} 테스트: {address} → {(result ? "성공" : "실패")}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[NetworkStatusMonitor] {checkType} 테스트 오류: {address} → {ex.Message}");
                            continue;
                        }

                        if (result)
                            return true; // 하나라도 성공하면 연결됨
                    }
                }

                return false; // 모두 실패
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ping 테스트
        /// </summary>
        private async Task<bool> TestPingAsync(string address, int timeout)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, timeout);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HTTP 테스트
        /// </summary>
        private async Task<bool> TestHttpAsync(string url, int timeout)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// TCP 테스트
        /// </summary>
        private async Task<bool> TestTcpAsync(string address, int port, int timeout)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(address, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 연결 끊김 팝업 표시
        /// </summary>
        private void ShowDisconnectedPopup()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 이미 팝업이 열려있으면 중복 생성하지 않음
                if (_popup != null)
                    return;

                _popup = new NetworkDisconnectedPopup();
                _popup.Closed += (s, e) => _popup = null;
                _popup.Show();
            });
        }

        /// <summary>
        /// 팝업 닫기
        /// </summary>
        private void ClosePopup()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_popup != null)
                {
                    _popup.Close();
                    _popup = null;
                }
            });
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
