using System.Windows;
using System.Windows.Controls;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for GeneralSettingsView.xaml
    /// </summary>
    public partial class GeneralSettingsView : UserControl
    {
        private readonly GeneralSettingsRepository _repository;
        private GeneralSettings _currentSettings;

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
            _currentSettings = _repository.GetSettings();
            AutoStartMonitoringCheckBox.IsChecked = _currentSettings.AutoStartMonitoring;
            StartInTrayCheckBox.IsChecked = _currentSettings.StartInTray;
            LauncherItemsPerRowTextBox.Text = _currentSettings.LauncherItemsPerRow.ToString();
            
            // 런처 보기 모드
            if (_currentSettings.LauncherViewMode == "Group")
            {
                GroupViewRadio.IsChecked = true;
            }
            else
            {
                GridViewRadio.IsChecked = true;
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                    MessageBox.Show("런처 행별 개수는 1~20 사이의 숫자여야 합니다.", "유효성 검사 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _repository.UpdateSettings(_currentSettings);

                // 런처 새로고침 (행별 개수 및 보기 모드 변경 반영)
                MainWindow.Instance?.RefreshExecuteTabLauncher();

                MessageBox.Show("일반 설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
