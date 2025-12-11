using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;
using Microsoft.Win32;

namespace FACTOVA_Execute.Views
{
    /// <summary>
    /// Interaction logic for ProgramSettingsView.xaml
    /// </summary>
    public partial class ProgramSettingsView : UserControl
    {
        private readonly ProgramRepository _repository;
        private ObservableCollection<ProgramInfo> _programs;

        public ProgramSettingsView()
        {
            InitializeComponent();
            _repository = new ProgramRepository();
            LoadPrograms();
        }

        /// <summary>
        /// 프로그램 목록 로드
        /// </summary>
        private void LoadPrograms()
        {
            _programs = _repository.GetAllPrograms();
            ProgramsDataGrid.ItemsSource = _programs;
        }

        /// <summary>
        /// 찾아보기 버튼 클릭
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramInfo selectedProgram)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "프로그램 선택",
                    Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
                    InitialDirectory = string.IsNullOrEmpty(selectedProgram.ProgramPath) 
                        ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) 
                        : System.IO.Path.GetDirectoryName(selectedProgram.ProgramPath)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedProgram.ProgramPath = openFileDialog.FileName;
                    
                    // 프로세스명 자동 설정 (확장자 제외)
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                    if (string.IsNullOrWhiteSpace(selectedProgram.ProcessName))
                    {
                        selectedProgram.ProcessName = fileName;
                    }
                    
                    ProgramsDataGrid.Items.Refresh();
                }
            }
        }

        /// <summary>
        /// 프로그램 추가 버튼 클릭
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "추가할 프로그램 선택",
                Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                var newProgram = new ProgramInfo
                {
                    IsEnabled = true,
                    ProgramName = fileName,
                    ProgramPath = openFileDialog.FileName,
                    ProcessName = fileName, // 프로세스명 자동 설정
                    ExecutionMode = "Network", // 기본값: Network
                    ExecutionOrder = 1 // 기본 순서: 1
                };

                _repository.AddProgram(newProgram);
                LoadPrograms();
                
                // 런처 새로고침
                MainWindow.Instance?.RefreshExecuteTabLauncher();
                
                MessageBox.Show("프로그램이 추가되었습니다.", "추가 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 삭제 버튼 클릭
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramInfo selectedProgram)
            {
                var result = MessageBox.Show(
                    $"'{selectedProgram.ProgramName}'을(를) 삭제하시겠습니까?",
                    "삭제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _repository.DeleteProgram(selectedProgram.Id);
                    LoadPrograms();
                    
                    // 런처 새로고침
                    MainWindow.Instance?.RefreshExecuteTabLauncher();
                    
                    MessageBox.Show("프로그램이 삭제되었습니다.", "삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("삭제할 프로그램을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var program in _programs)
                {
                    _repository.UpdateProgram(program);
                }

                // 런처 새로고침
                MainWindow.Instance?.RefreshExecuteTabLauncher();

                MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
