using System.Windows;
using System.Windows.Controls;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;
using FACTOVA_Execute.Services;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for GeneralSettingsView.xaml
    /// </summary>
    public partial class GeneralSettingsView : UserControl
    {
        private readonly GeneralSettingsRepository _repository;
        private GeneralSettings _currentSettings;
        private bool _isLoading = false;

        public GeneralSettingsView()
        {
            InitializeComponent();
            _repository = new GeneralSettingsRepository();
            LoadSettings();
        }

        /// <summary>
        /// 설정 로드
        /// </summary>
        private void LoadSettings()
        {
            _isLoading = true;
            
            _currentSettings = _repository.GetSettings();
            AutoStartMonitoringCheckBox.IsChecked = _currentSettings.AutoStartMonitoring;
            StartInTrayCheckBox.IsChecked = _currentSettings.StartInTray;
            LauncherItemsPerRowTextBox.Text = _currentSettings.LauncherItemsPerRow.ToString();
            
            // 언어 설정
            if (_currentSettings.Language == "en-US")
            {
                EnglishRadio.IsChecked = true;
            }
            else
            {
                KoreanRadio.IsChecked = true;
            }
            
            // 런처 보기 모드
            if (_currentSettings.LauncherViewMode == "Group")
            {
                GroupViewRadio.IsChecked = true;
            }
            else
            {
                GridViewRadio.IsChecked = true;
            }
            
            // 네트워크 상태 감지 설정
            EnableNetworkMonitoringCheckBox.IsChecked = _currentSettings.EnableNetworkMonitoring;
            NetworkCheckIntervalTextBox.Text = _currentSettings.NetworkCheckIntervalSeconds.ToString();
            NetworkCheckIntervalTextBox.IsEnabled = _currentSettings.EnableNetworkMonitoring;
            
            _isLoading = false;
        }

        /// <summary>
        /// 언어 변경 이벤트
        /// </summary>
        private void LanguageRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _currentSettings == null)
                return;
                
            var newLanguage = KoreanRadio.IsChecked == true ? "ko-KR" : "en-US";
            
            if (_currentSettings.Language != newLanguage)
            {
                _currentSettings.Language = newLanguage;
                
                // 즉시 언어 변경 적용
                LocalizationService.Instance.SetLanguage(newLanguage);
                
                // 설정 저장
                _repository.UpdateSettings(_currentSettings);
            }
        }

        /// <summary>
        /// 네트워크 모니터링 체크박스 변경 이벤트
        /// </summary>
        private void EnableNetworkMonitoringCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (NetworkCheckIntervalTextBox != null)
            {
                NetworkCheckIntervalTextBox.IsEnabled = EnableNetworkMonitoringCheckBox.IsChecked ?? false;
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var L = LocalizationService.Instance;
                
                _currentSettings.AutoStartMonitoring = AutoStartMonitoringCheckBox.IsChecked ?? false;
                _currentSettings.StartInTray = StartInTrayCheckBox.IsChecked ?? false;
                
                // LauncherViewMode
                _currentSettings.LauncherViewMode = GroupViewRadio.IsChecked == true ? "Group" : "Grid";
                
                // LauncherItemsPerRow 유효성 검사
                if (int.TryParse(LauncherItemsPerRowTextBox.Text, out int itemsPerRow) && itemsPerRow > 0 && itemsPerRow <= 20)
                {
                    _currentSettings.LauncherItemsPerRow = itemsPerRow;
                }
                else
                {
                    MessageBox.Show(L["Validation_ItemsPerRow"], L["Validation_Error"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 네트워크 상태 감지 설정
                _currentSettings.EnableNetworkMonitoring = EnableNetworkMonitoringCheckBox.IsChecked ?? false;
                
                if (int.TryParse(NetworkCheckIntervalTextBox.Text, out int checkInterval) && checkInterval > 0 && checkInterval <= 60)
                {
                    _currentSettings.NetworkCheckIntervalSeconds = checkInterval;
                }
                else
                {
                    MessageBox.Show(L["Validation_CheckInterval"], L["Validation_Error"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _repository.UpdateSettings(_currentSettings);

                // 런처 새로고침 (행별 개수 및 보기 모드 변경 반영)
                MainWindow.Instance?.RefreshExecuteTabLauncher();

                MessageBox.Show(L["GeneralSettings_SaveSuccess"], L["GeneralSettings_SaveComplete"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationService.Instance["GeneralSettings_SaveError"]} {ex.Message}", LocalizationService.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
