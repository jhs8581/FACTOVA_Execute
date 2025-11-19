using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Globalization;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Boolean 값을 반전시키는 Converter
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }
    }

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

            // 현재 선택된 체크 타입에 맞는 주소 로드
            UpdateAddressesForCheckType();

            PortTextBox.Text = _currentSettings.Port.ToString();
            TimeoutTextBox.Text = _currentSettings.TimeoutMs.ToString();
            RetryDelayTextBox.Text = _currentSettings.RetryDelaySeconds.ToString();
            CheckIntervalTextBox.Text = _currentSettings.CheckIntervalSeconds.ToString();
        }

        /// <summary>
        /// 체크 타입 변경 이벤트
        /// </summary>
        private void CheckTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentSettings == null || TargetAddressesTextBox == null)
                return;

            // 현재 입력된 주소를 이전 체크 타입에 저장
            SaveCurrentAddresses();

            // 새로운 체크 타입으로 변경
            _currentSettings.CheckType = ((ComboBoxItem)CheckTypeComboBox.SelectedItem).Content.ToString()!;

            // UI 업데이트
            UpdateAddressesForCheckType();
            UpdateUIForCheckType();
        }

        /// <summary>
        /// 현재 입력된 주소를 설정에 저장
        /// </summary>
        private void SaveCurrentAddresses()
        {
            var addresses = TargetAddressesTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            
            _currentSettings.SetCurrentAddresses(string.Join(";", addresses));
        }

        /// <summary>
        /// 체크 타입에 맞는 주소를 UI에 표시
        /// </summary>
        private void UpdateAddressesForCheckType()
        {
            var addresses = _currentSettings.GetCurrentAddresses();
            TargetAddressesTextBox.Text = addresses.Replace(";", Environment.NewLine);
        }

        /// <summary>
        /// 체크 타입에 따라 UI 업데이트 (레이블, 힌트, 예시)
        /// </summary>
        private void UpdateUIForCheckType()
        {
            if (AddressLabelTextBlock == null || AddressHintTextBlock == null || ExampleTextBlock == null)
                return;

            switch (_currentSettings.CheckType)
            {
                case "Ping":
                    AddressLabelTextBlock.Text = "Ping 주소 (우선순위):";
                    AddressHintTextBlock.Text = "* 한 줄에 하나씩 IP 주소 입력 (우선순위 순서대로)";
                    ExampleTextBlock.Text = "• 여러 게이트웨이를 등록하면 순서대로 확인합니다.\n" +
                                           "• 하나라도 연결되면 네트워크 연결 성공으로 판단합니다.\n" +
                                           "• 모두 실패하면 지정한 시간(재시도 대기시간) 후 다시 시도합니다.\n\n" +
                                           "예시 Ping 주소:\n" +
                                           "165.186.55.129\n" +
                                           "10.162.190.1\n" +
                                           "165.186.47.1\n" +
                                           "8.8.8.8";
                    PortTextBox.IsEnabled = false;
                    break;

                case "HTTP":
                    AddressLabelTextBlock.Text = "HTTP URL (우선순위):";
                    AddressHintTextBlock.Text = "* 한 줄에 하나씩 URL 입력 (http:// 또는 https:// 포함)";
                    ExampleTextBlock.Text = "• 여러 URL을 등록하면 순서대로 확인합니다.\n" +
                                           "• 하나라도 연결되면 네트워크 연결 성공으로 판단합니다.\n" +
                                           "• HTTP 200 응답을 받으면 성공으로 판단합니다.\n\n" +
                                           "예시 HTTP URL:\n" +
                                           "http://165.186.55.129\n" +
                                           "http://google.com\n" +
                                           "https://www.naver.com\n" +
                                           "http://example.com";
                    PortTextBox.IsEnabled = false;
                    break;

                case "TCP":
                    AddressLabelTextBlock.Text = "TCP 주소 (우선순위):";
                    AddressHintTextBlock.Text = "* 한 줄에 하나씩 IP 주소 입력 + 아래 포트 설정";
                    ExampleTextBlock.Text = "• 여러 주소를 등록하면 순서대로 확인합니다.\n" +
                                           "• 지정한 포트로 TCP 연결을 시도합니다.\n" +
                                           "• 연결이 성공하면 네트워크 연결 성공으로 판단합니다.\n\n" +
                                           "예시 TCP 주소 (포트 80):\n" +
                                           "165.186.55.129\n" +
                                           "10.162.190.1\n" +
                                           "192.168.0.1\n" +
                                           "* 포트는 아래 '포트 (TCP용)' 필드에서 설정";
                    PortTextBox.IsEnabled = true;
                    break;
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 입력된 주소 저장
                SaveCurrentAddresses();
                
                _currentSettings.CheckType = ((ComboBoxItem)CheckTypeComboBox.SelectedItem).Content.ToString()!;
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
                    MessageBox.Show($"{AddressLabelTextBlock.Text}을(를) 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    results.AppendLine($"  → {(isConnected ? $"✓ 성공 ({checkType})" : "✗ 실패")}");
                    
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

        /// <summary>
        /// 순차적 테스트 (Ping → HTTP → TCP)
        /// </summary>
        private async Task<(bool success, string? method)> TestSequentialAsync(string address, int timeout, int port, System.Text.StringBuilder results)
        {
            // 더 이상 사용하지 않음 - 삭제 가능
            return (false, null);
        }
    }
}
