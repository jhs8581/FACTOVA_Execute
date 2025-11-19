using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for NetworkSettingsView.xaml
    /// </summary>
    public partial class NetworkSettingsView : UserControl
    {
        private readonly NetworkSettingsRepository _repository;
        private NetworkSettings _currentSettings;

        public NetworkSettingsView()
        {
            InitializeComponent();
            _repository = new NetworkSettingsRepository();
            LoadSettings();
        }

        /// <summary>
        /// 설정 로드
        /// </summary>
        private void LoadSettings()
        {
            _currentSettings = _repository.GetSettings();

            CheckTypeComboBox.SelectedItem = CheckTypeComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == _currentSettings.CheckType);

            // 세미콜론으로 구분된 주소를 줄바꿈으로 변경
            TargetAddressesTextBox.Text = _currentSettings.TargetAddresses.Replace(";", Environment.NewLine);
            PortTextBox.Text = _currentSettings.Port.ToString();
            TimeoutTextBox.Text = _currentSettings.TimeoutMs.ToString();
            RetryDelayTextBox.Text = _currentSettings.RetryDelaySeconds.ToString();
            CheckIntervalTextBox.Text = _currentSettings.CheckIntervalSeconds.ToString();
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentSettings.CheckType = ((ComboBoxItem)CheckTypeComboBox.SelectedItem).Content.ToString()!;
                
                // 줄바꿈으로 구분된 주소를 세미콜론으로 변경
                var addresses = TargetAddressesTextBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                _currentSettings.TargetAddresses = string.Join(";", addresses);
                
                _currentSettings.Port = int.Parse(PortTextBox.Text);
                _currentSettings.TimeoutMs = int.Parse(TimeoutTextBox.Text);
                _currentSettings.RetryDelaySeconds = int.Parse(RetryDelayTextBox.Text);
                _currentSettings.CheckIntervalSeconds = int.Parse(CheckIntervalTextBox.Text);

                _repository.UpdateSettings(_currentSettings);

                MessageBox.Show("네트워크 설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 연결 테스트 버튼 클릭
        /// </summary>
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            TestButton.IsEnabled = false;
            StatusTextBlock.Text = "테스트 중...";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
            DetailStatusTextBlock.Text = "";

            try
            {
                var checkType = ((ComboBoxItem)CheckTypeComboBox.SelectedItem).Content.ToString();
                var timeout = int.Parse(TimeoutTextBox.Text);
                var port = int.Parse(PortTextBox.Text);
                
                // 주소 목록 가져오기
                var addresses = TargetAddressesTextBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (!addresses.Any())
                {
                    MessageBox.Show("대상 주소를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool anySuccess = false;
                var results = new System.Text.StringBuilder();

                foreach (var address in addresses)
                {
                    results.AppendLine($"테스트 중: {address}");
                    bool isConnected = false;

                    switch (checkType)
                    {
                        case "Ping":
                            isConnected = await TestPingAsync(address, timeout);
                            break;
                        case "HTTP":
                            isConnected = await TestHttpAsync(address, timeout);
                            break;
                        case "TCP":
                            isConnected = await TestTcpAsync(address, port, timeout);
                            break;
                    }

                    results.AppendLine($"  → {(isConnected ? "✓ 성공" : "✗ 실패")}");
                    
                    if (isConnected)
                    {
                        anySuccess = true;
                        break; // 하나라도 성공하면 중단
                    }
                }

                if (anySuccess)
                {
                    StatusTextBlock.Text = "연결 성공";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    DetailStatusTextBlock.Text = results.ToString();
                    MessageBox.Show("네트워크 연결 테스트 성공!\n\n" + results.ToString(), "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = "모든 주소 연결 실패";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    DetailStatusTextBlock.Text = results.ToString();
                    MessageBox.Show("네트워크 연결 테스트 실패\n\n" + results.ToString(), "실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "오류 발생";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show($"테스트 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestButton.IsEnabled = true;
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
        /// TCP 연결 테스트
        /// </summary>
        private async Task<bool> TestTcpAsync(string address, int port, int timeout)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(address, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == connectTask && client.Connected)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
