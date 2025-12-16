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
        private ObservableCollection<ProgramInfo> _allPrograms; // 전체 목록 (필터링용)

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
            _allPrograms = _repository.GetAllPrograms();
            _programs = new ObservableCollection<ProgramInfo>(_allPrograms);
            ProgramsDataGrid.ItemsSource = _programs;
            
            // 필터 초기화
            if (FilterComboBox != null)
            {
                FilterComboBox.SelectedIndex = 0; // "전체" 선택
            }
        }

        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilter()
        {
            if (_allPrograms == null || FilterComboBox == null)
                return;

            var selectedItem = FilterComboBox.SelectedItem as ComboBoxItem;
            var filterTag = selectedItem?.Tag as string;

            if (filterTag == "All")
            {
                // 전체 표시
                _programs = new ObservableCollection<ProgramInfo>(_allPrograms);
            }
            else
            {
                // 실행모드별 필터링
                _programs = new ObservableCollection<ProgramInfo>(
                    _allPrograms.Where(p => p.ExecutionMode == filterTag));
            }

            ProgramsDataGrid.ItemsSource = _programs;
        }

        /// <summary>
        /// 필터 콤보박스 선택 변경
        /// </summary>
        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
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
        /// 아이콘 변경 버튼 클릭
        /// </summary>
        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // 버튼이 속한 행의 데이터 가져오기
                var dataContext = button.DataContext as ProgramInfo;
                if (dataContext == null)
                    return;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "아이콘 이미지 선택",
                    Filter = "이미지 파일 (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|아이콘 파일 (*.ico)|*.ico|PNG 파일 (*.png)|*.png|모든 파일 (*.*)|*.*",
                    InitialDirectory = string.IsNullOrEmpty(dataContext.IconPath)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                        : System.IO.Path.GetDirectoryName(dataContext.IconPath)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    dataContext.IconPath = openFileDialog.FileName;
                    ProgramsDataGrid.Items.Refresh();
                    
                    MessageBox.Show($"아이콘이 설정되었습니다.\n저장 버튼을 눌러 변경사항을 저장하세요.", "아이콘 설정", MessageBoxButton.OK, MessageBoxImage.Information);
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
                LoadPrograms(); // 재조회
                
                // 런처 새로고침
                MainWindow.Instance?.RefreshExecuteTabLauncher();
                
                MessageBox.Show("프로그램이 추가되었습니다.", "추가 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 폴더 추가 버튼 클릭
        /// </summary>
        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "런처에 추가할 폴더를 선택하세요",
                    ShowNewFolderButton = false,
                    SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedPath = dialog.SelectedPath;
                    var folderName = System.IO.Path.GetFileName(selectedPath);

                    var newFolder = new ProgramInfo
                    {
                        IsEnabled = true,
                        ProgramName = folderName,
                        ProgramPath = selectedPath,
                        ProcessName = string.Empty, // 폴더는 프로세스명 없음
                        ExecutionMode = "Launcher", // 폴더는 항상 런처 전용
                        ExecutionOrder = 1,
                        IsFolder = true
                    };

                    _repository.AddProgram(newFolder);
                    LoadPrograms(); // 재조회

                    // 런처 새로고침
                    MainWindow.Instance?.RefreshExecuteTabLauncher();

                    MessageBox.Show($"폴더 '{folderName}'이(가) 런처에 추가되었습니다.", "추가 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"폴더 추가 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    LoadPrograms(); // 재조회
                    
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
                // 현재 표시된 항목뿐만 아니라 전체 목록 저장
                foreach (var program in _allPrograms)
                {
                    _repository.UpdateProgram(program);
                }

                // 저장 후 재조회
                LoadPrograms();

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
