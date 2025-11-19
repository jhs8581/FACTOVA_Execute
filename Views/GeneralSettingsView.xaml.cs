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

                _repository.UpdateSettings(_currentSettings);

                MessageBox.Show("일반 설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
