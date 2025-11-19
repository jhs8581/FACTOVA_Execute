using System.Configuration;
using System.Data;
using System.Windows;
using FACTOVA_Execute.Data;

namespace FACTOVA_Execute
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 데이터베이스 초기화 (MainWindow 생성 전에 실행)
                DatabaseInitializer.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"데이터베이스 초기화 중 오류가 발생했습니다:\n{ex.Message}",
                    "초기화 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // 초기화 실패 시 프로그램 종료
                Shutdown();
            }
        }
    }
}
