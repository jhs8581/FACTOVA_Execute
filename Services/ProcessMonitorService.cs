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
        private CancellationTokenSource? _cancellationTokenSource;

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

        private string L(string key) => LocalizationService.Instance.GetString(key);

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            var settings = _triggerRepository.GetSettings();

            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _monitorTimer = new System.Timers.Timer(settings.CheckIntervalSeconds * 1000);
            _monitorTimer.Elapsed += async (s, e) => await CheckProcessAndStartPrograms(_cancellationTokenSource.Token);
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();

            LogMessage(L("Log_ProcessMonitorStarted"), LogLevel.Info);
        }

        /// <summary>
        /// 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            // 실행 중인 작업 즉시 취소
            _cancellationTokenSource?.Cancel();

            LogMessage(L("Log_ProcessMonitorStopped"), LogLevel.Warning);
        }

        /// <summary>
        /// 프로세스 확인 및 프로그램 실행
        /// </summary>
        private async Task CheckProcessAndStartPrograms(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var settings = _triggerRepository.GetSettings();

                if (string.IsNullOrWhiteSpace(settings.TargetProcesses))
                {
                    LogMessage(L("Log_NoProcessToWatch"), LogLevel.Warning);
                    return;
                }

                var targetProcesses = settings.TargetProcesses
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (!targetProcesses.Any())
                {
                    LogMessage(L("Log_NoProcessRegistered"), LogLevel.Warning);
                    return;
                }

                LogMessage($"{L("Log_CheckingProcess")} ({targetProcesses.Count} {L("Log_Watching")})", LogLevel.Info);

                bool processDetected = false;
                string? detectedProcessName = null;

                foreach (var processName in targetProcesses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
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
                                LogMessage($"  ✓ {L("Log_ProcessDetected")}: {processName} (PID: {string.Join(", ", processes.Select(p => p.Id))})", LogLevel.Success);
                            }
                            
                            break;
                        }
                        else
                        {
                            // 이전에 감지되었던 프로세스가 종료된 경우
                            if (_detectedProcesses.Contains(processName))
                            {
                                _detectedProcesses.Remove(processName);
                                LogMessage($"  ✗ {L("Log_ProcessTerminated")}: {processName}", LogLevel.Warning);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"  ✗ {L("Log_ProcessCheckError")} ({processName}): {ex.Message}", LogLevel.Error);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 프로세스 감지 및 아직 프로그램을 실행하지 않은 경우
                if (processDetected && !_isProgramsStarted)
                {
                    LogMessage($"{L("Log_TriggerDetected")}: {detectedProcessName}", LogLevel.Success);

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
                    LogMessage(L("Log_AllTriggerTerminated"), LogLevel.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                LogMessage(L("Log_ProcessCheckCancelled"), LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"{L("Error")}: {ex.Message}", LogLevel.Error);
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
                    LogMessage(L("Log_NoTriggerProgramsToRun"), LogLevel.Warning);
                    return;
                }

                LogMessage($"{L("Log_StartingTriggerPrograms")} ({programs.Count})...", LogLevel.Info);

                foreach (var program in programs)
                {
                    await StartProgram(program);
                    
                    // 프로그램 간 1초 대기 (순차 실행)
                    await Task.Delay(1000);
                }

                _isProgramsStarted = true;
                LogMessage(L("Log_AllTriggerProgramsComplete"), LogLevel.Success);
            }
            catch (Exception ex)
            {
                LogMessage($"{L("Log_ProgramStartError")}: {ex.Message}", LogLevel.Error);
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
                        LogMessage($"[{program.ProgramName}] {L("Log_AlreadyRunning")}: {program.ProcessName}", LogLevel.Warning);
                        return;
                    }
                }

                if (!File.Exists(program.ProgramPath))
                {
                    LogMessage($"[{program.ProgramName}] {L("Log_FileNotFound")}: {program.ProgramPath}", LogLevel.Error);
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
                LogMessage($"[{program.ProgramName}] {L("Log_ProgramSuccess")}: {program.ProgramPath}", LogLevel.Success);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogMessage($"[{program.ProgramName}] {L("Log_ProgramFailed")}: {ex.Message}", LogLevel.Error);
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
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
