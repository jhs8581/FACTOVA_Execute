using System.Windows;
using System.Windows.Controls;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for TriggerSettingsView.xaml
    /// </summary>
    public partial class TriggerSettingsView : UserControl
    {
        private readonly TriggerSettingsRepository _repository;

        public TriggerSettingsView()
        {
            InitializeComponent();
            _repository = new TriggerSettingsRepository();
            LoadSettings();
        }

        /// <summary>
        /// 설정 로드
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = _repository.GetSettings();

                // 프로세스 목록 (줄바꿈으로 변환)
                var processList = settings.GetProcessList();
                TargetProcessesTextBox.Text = string.Join("\n", processList);

                // 체크 주기
                CheckIntervalTextBox.Text = settings.CheckIntervalSeconds.ToString();

                // 자동 실행
                AutoStartCheckBox.IsChecked = settings.AutoStartPrograms;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"설정 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = _repository.GetSettings();

                // 프로세스 목록 (줄바꿈을 세미콜론으로 변환)
                var processes = TargetProcessesTextBox.Text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                settings.SetProcessList(processes);

                // 체크 주기
                if (int.TryParse(CheckIntervalTextBox.Text, out int checkInterval) && checkInterval > 0)
                {
                    settings.CheckIntervalSeconds = checkInterval;
                }
                else
                {
                    MessageBox.Show(
                        "체크 주기는 1 이상의 정수여야 합니다.",
                        "입력 오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                // 자동 실행
                settings.AutoStartPrograms = AutoStartCheckBox.IsChecked == true;

                // 저장
                _repository.UpdateSettings(settings);

                MessageBox.Show(
                    "트리거 설정이 저장되었습니다.\n변경 사항을 적용하려면 모니터링을 재시작하세요.",
                    "저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"설정 저장 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
