using System.Diagnostics;
using System.IO;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Services
{
    /// <summary>
    /// 프로세스 모니터링 및 프로그램 자동 실행 서비스
    /// </summary>
    public class ProcessMonitorService
    {
        private readonly TriggerSettingsRepository _triggerRepository;
        private readonly ProgramRepository _programRepository;
        private System.Timers.Timer? _monitorTimer;
        private HashSet<string> _detectedProcesses = new HashSet<string>();
        private bool _isProgramsStarted = false;

        public event Action<string, LogLevel>? LogMessageReceived;
        public event Action? AllProgramsStarted;

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Success
        }

        public ProcessMonitorService()
        {
            _triggerRepository = new TriggerSettingsRepository();
            _programRepository = new ProgramRepository();
        }

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            var settings = _triggerRepository.GetSettings();

            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();

            _monitorTimer = new System.Timers.Timer(settings.CheckIntervalSeconds * 1000);
            _monitorTimer.Elapsed += async (s, e) => await CheckProcessAndStartPrograms();
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();

            LogMessage("프로세스 모니터링을 시작했습니다.", LogLevel.Info);

            // 즉시 한번 체크
            Task.Run(async () => await CheckProcessAndStartPrograms());
        }

        /// <summary>
        /// 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            LogMessage("프로세스 모니터링을 중지했습니다.", LogLevel.Warning);
        }

        /// <summary>
        /// 프로세스 확인 및 프로그램 실행
        /// </summary>
        private async Task CheckProcessAndStartPrograms()
        {
            try
            {
                var settings = _triggerRepository.GetSettings();

                if (string.IsNullOrWhiteSpace(settings.TargetProcesses))
                {
                    LogMessage("감시할 프로세스가 등록되지 않았습니다. 설정에서 프로세스를 추가해주세요.", LogLevel.Warning);
                    return;
                }

                var targetProcesses = settings.TargetProcesses
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (!targetProcesses.Any())
                {
                    LogMessage("감시할 프로세스가 등록되지 않았습니다.", LogLevel.Warning);
                    return;
                }

                LogMessage($"프로세스 확인 중... ({targetProcesses.Count}개 감시)", LogLevel.Info);

                bool processDetected = false;
                string? detectedProcessName = null;

                foreach (var processName in targetProcesses)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        
                        if (processes.Any())
                        {
                            processDetected = true;
                            detectedProcessName = processName;
                            
                            // 처음 감지된 경우에만 로그 출력
                            if (!_detectedProcesses.Contains(processName))
                            {
                                _detectedProcesses.Add(processName);
                                LogMessage($"  ✓ 프로세스 감지: {processName} (PID: {string.Join(", ", processes.Select(p => p.Id))})", LogLevel.Success);
                            }
                            
                            break;
                        }
                        else
                        {
                            // 이전에 감지되었던 프로세스가 종료된 경우
                            if (_detectedProcesses.Contains(processName))
                            {
                                _detectedProcesses.Remove(processName);
                                LogMessage($"  ✗ 프로세스 종료: {processName}", LogLevel.Warning);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"  ✗ 프로세스 확인 오류 ({processName}): {ex.Message}", LogLevel.Error);
                    }
                }

                // 프로세스 감지 및 아직 프로그램을 실행하지 않은 경우
                if (processDetected && !_isProgramsStarted)
                {
                    LogMessage($"트리거 프로세스 감지됨: {detectedProcessName}", LogLevel.Success);

                    // 자동 실행 옵션이 켜져있으면 프로그램 실행
                    if (settings.AutoStartPrograms)
                    {
                        await StartEnabledPrograms();
                        AllProgramsStarted?.Invoke();
                    }
                }
                else if (!processDetected && _isProgramsStarted)
                {
                    // 모든 감시 프로세스가 종료되면 상태 초기화
                    _isProgramsStarted = false;
                    _detectedProcesses.Clear();
                    LogMessage("모든 트리거 프로세스가 종료되었습니다.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"오류 발생: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 활성화된 프로그램 실행
        /// </summary>
        public async Task StartEnabledPrograms()
        {
            try
            {
                var programs = _programRepository.GetAllPrograms()
                    .Where(p => p.IsEnabled && p.ExecutionMode == "Trigger")
                    .OrderBy(p => p.ExecutionOrder)
                    .ToList();

                if (!programs.Any())
                {
                    LogMessage("실행할 프로그램 감지 실행 모드 프로그램이 없습니다.", LogLevel.Warning);
                    return;
                }

                LogMessage($"프로그램 감지 실행 시작 ({programs.Count}개)...", LogLevel.Info);

                foreach (var program in programs)
                {
                    await StartProgram(program);
                    
                    // 프로그램 간 1초 대기 (순차 실행)
                    await Task.Delay(1000);
                }

                _isProgramsStarted = true;
                LogMessage("모든 프로그램 감지 실행 모드 프로그램 실행 완료", LogLevel.Success);
            }
            catch (Exception ex)
            {
                LogMessage($"프로그램 실행 중 오류: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 개별 프로그램 실행
        /// </summary>
        public async Task StartProgram(ProgramInfo program)
        {
            try
            {
                // 프로세스명이 있으면 이미 실행 중인지 확인
                if (!string.IsNullOrWhiteSpace(program.ProcessName))
                {
                    var runningProcesses = Process.GetProcessesByName(program.ProcessName);
                    if (runningProcesses.Any())
                    {
                        LogMessage($"[{program.ProgramName}] 이미 실행 중입니다. 프로세스: {program.ProcessName}", LogLevel.Warning);
                        return;
                    }
                }

                if (!File.Exists(program.ProgramPath))
                {
                    LogMessage($"[{program.ProgramName}] 파일을 찾을 수 없습니다: {program.ProgramPath}", LogLevel.Error);
                    return;
                }

                var directory = Path.GetDirectoryName(program.ProgramPath);
                var fileName = Path.GetFileName(program.ProgramPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    WorkingDirectory = directory,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                LogMessage($"[{program.ProgramName}] 실행 성공: {program.ProgramPath}", LogLevel.Success);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogMessage($"[{program.ProgramName}] 실행 실패: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 로그 메시지 발생
        /// </summary>
        private void LogMessage(string message, LogLevel level)
        {
            LogMessageReceived?.Invoke(message, level);
        }

        public void Dispose()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
        }
    }
}
