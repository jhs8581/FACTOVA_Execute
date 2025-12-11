using System;
using System.Globalization;
using System.Windows.Data;

namespace FACTOVA_Execute.Converters
{
    /// <summary>
    /// ExecutionMode 값을 한글 표시명으로 변환하는 컨버터
    /// Network → 네트워크 연결 실행
    /// Trigger → 프로그램 감지 실행
    /// </summary>
    public class ExecutionModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string mode)
            {
                return mode switch
                {
                    "Network" => "네트워크 연결 실행",
                    "Trigger" => "프로그램 감지 실행",
                    _ => value
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string displayName)
            {
                return displayName switch
                {
                    "네트워크 연결 실행" => "Network",
                    "프로그램 감지 실행" => "Trigger",
                    _ => value
                };
            }
            return value;
        }
    }
}
