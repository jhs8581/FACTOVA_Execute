using System;
using System.Globalization;
using System.Windows.Data;
using FACTOVA_Execute.Services;

namespace FACTOVA_Execute.Converters
{
    /// <summary>
    /// ExecutionMode 값을 표시명으로 변환하는 컨버터 (다국어 지원)
    /// Network → 네트워크 연결 실행 / Network Run
    /// Trigger → 프로그램 감지 실행 / Process Trigger Run
    /// Launcher → 런처 전용 / Launcher Only
    /// </summary>
    public class ExecutionModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string mode)
            {
                var L = LocalizationService.Instance;
                return mode switch
                {
                    "Network" => L["ProgramSettings_FilterNetwork"],
                    "Trigger" => L["ProgramSettings_FilterTrigger"],
                    "Launcher" => L["ProgramSettings_FilterLauncher"],
                    _ => value
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string displayName)
            {
                var L = LocalizationService.Instance;
                
                if (displayName == L["ProgramSettings_FilterNetwork"])
                    return "Network";
                if (displayName == L["ProgramSettings_FilterTrigger"])
                    return "Trigger";
                if (displayName == L["ProgramSettings_FilterLauncher"])
                    return "Launcher";
            }
            return value;
        }
    }
}
