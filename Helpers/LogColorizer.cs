using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;

namespace FACTOVA_Execute.Helpers
{
    /// <summary>
    /// 로그 색상 처리를 위한 커스텀 컬러라이저
    /// </summary>
    public class LogColorizer : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.Length == 0)
                return;

            var lineText = CurrentContext.Document.GetText(line);

            // 성공 메시지 (녹색)
            if (lineText.Contains("성공") || lineText.Contains("완료") || lineText.Contains("✓"))
            {
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                {
                    var timestampEnd = lineText.IndexOf(']');
                    if (timestampEnd > 0 && timestampEnd < lineText.Length - 1)
                    {
                        var messageStart = line.Offset + timestampEnd + 2;
                        if (element.TextRunProperties.ForegroundBrush != null)
                        {
                            element.TextRunProperties.SetForegroundBrush(Brushes.LightGreen);
                        }
                    }
                });
            }
            // 경고 메시지 (노란색)
            else if (lineText.Contains("경고") || lineText.Contains("실패") || lineText.Contains("✗") || 
                     lineText.Contains("재시도") || lineText.Contains("중지") || lineText.Contains("끊김"))
            {
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                {
                    var timestampEnd = lineText.IndexOf(']');
                    if (timestampEnd > 0 && timestampEnd < lineText.Length - 1)
                    {
                        element.TextRunProperties.SetForegroundBrush(Brushes.Yellow);
                    }
                });
            }
            // 오류 메시지 (빨간색)
            else if (lineText.Contains("오류") || lineText.Contains("에러") || lineText.Contains("Error") ||
                     lineText.Contains("없습니다") && lineText.Contains("파일"))
            {
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                {
                    var timestampEnd = lineText.IndexOf(']');
                    if (timestampEnd > 0 && timestampEnd < lineText.Length - 1)
                    {
                        element.TextRunProperties.SetForegroundBrush(Brushes.Red);
                    }
                });
            }
        }
    }
}
