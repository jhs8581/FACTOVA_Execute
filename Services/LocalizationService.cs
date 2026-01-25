using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace FACTOVA_Execute.Services
{
    /// <summary>
    /// 다국어 지원 서비스
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        private static readonly object _lock = new object();
        
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LocalizationService();
                    }
                }
                return _instance;
            }
        }

        private ResourceDictionary? _currentDictionary;
        private string _currentLanguage = "ko-KR";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? LanguageChanged;

        /// <summary>
        /// 현재 언어 코드
        /// </summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged(nameof(CurrentLanguage));
                }
            }
        }

        /// <summary>
        /// 언어 변경
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            try
            {
                var dictionaryPath = languageCode switch
                {
                    "en-US" => "Resources/Strings.en-US.xaml",
                    "ko-KR" => "Resources/Strings.ko-KR.xaml",
                    _ => "Resources/Strings.ko-KR.xaml"
                };

                var newDictionary = new ResourceDictionary
                {
                    Source = new Uri(dictionaryPath, UriKind.Relative)
                };

                // 기존 언어 딕셔너리 제거
                if (_currentDictionary != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(_currentDictionary);
                }

                // 새 언어 딕셔너리 추가
                Application.Current.Resources.MergedDictionaries.Add(newDictionary);
                _currentDictionary = newDictionary;
                CurrentLanguage = languageCode;

                // 문화권 설정
                var culture = new CultureInfo(languageCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                // 언어 변경 이벤트 발생
                LanguageChanged?.Invoke();

                System.Diagnostics.Debug.WriteLine($"언어 변경됨: {languageCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"언어 변경 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 리소스 문자열 가져오기
        /// </summary>
        public string GetString(string key)
        {
            try
            {
                if (Application.Current.Resources[key] is string value)
                {
                    return value;
                }
                return $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        /// <summary>
        /// 인덱서로 리소스 문자열 접근
        /// </summary>
        public string this[string key] => GetString(key);

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
