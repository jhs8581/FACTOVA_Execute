using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using FACTOVA_Execute.Data;
using FACTOVA_Execute.Models;

namespace FACTOVA_Execute.Services
{
    /// <summary>
    /// 네트워크 모니터링 및 프로그램 자동 실행 서비스
    /// </summary>
    public class NetworkMonitorService
    {
        private readonly NetworkSettingsRepository _networkRepository;
        private readonly ProgramRepository _programRepository;
        private System.Timers.Timer? _monitorTimer;
        private bool _isNetworkConnected = false;
        private bool _isProgramsStarted = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<string, LogLevel>? LogMessageReceived;
        public event Action? AllProgramsStarted; // 모든 프로그램 실행 완료 이벤트

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Success
        }

        public NetworkMonitorService()
        {
            _networkRepository = new NetworkSettingsRepository();
            _programRepository = new ProgramRepository();
        }

        private string L(string key) => LocalizationService.Instance.GetString(key);

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            var settings = _networkRepository.GetSettings();
            
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _monitorTimer = new System.Timers.Timer(settings.CheckIntervalSeconds * 1000);
            _monitorTimer.Elapsed += async (s, e) => await CheckNetworkAndStartPrograms(_cancellationTokenSource.Token);
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();

            LogMessage(L("Log_MonitoringStarted"), LogLevel.Info);
            
            // 즉시 한번 체크
            Task.Run(async () => await CheckNetworkAndStartPrograms(_cancellationTokenSource.Token));
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
            
            LogMessage(L("Log_MonitoringStopped"), LogLevel.Warning);
        }

        /// <summary>
        /// 네트워크 확인 및 프로그램 실행
        /// </summary>
        private async Task CheckNetworkAndStartPrograms(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var settings = _networkRepository.GetSettings();
                
                // 모든 체크 타입의 주소 가져오기
                var allAddresses = settings.GetAllAddresses();

                // 하나라도 주소가 있는지 확인
                var hasAnyAddress = allAddresses.Values.Any(list => list.Any());
                if (!hasAnyAddress)
                {
                    LogMessage(L("Log_NoAddress"), LogLevel.Warning);
                    return;
                }

                LogMessage(L("Log_CheckingNetwork"), LogLevel.Info);

                bool isConnected = false;
                string? connectedAddress = null;
                string? connectedMethod = null;

                // Ping, HTTP, TCP 순서로 체크
                foreach (var kvp in allAddresses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var checkType = kvp.Key;
                    var addresses = kvp.Value;

                    if (!addresses.Any())
                    {
                        LogMessage($"  [{checkType}] {L("Log_NoAddressForType")}", LogLevel.Info);
                        continue;
                    }

                    LogMessage($"  [{checkType}] {L("Log_CheckStart")} ({addresses.Count})", LogLevel.Info);

                    foreach (var address in addresses)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        LogMessage($"    → {address} {L("Log_Checking")}...", LogLevel.Info);

                        bool result = false;
                        try
                        {
                            switch (checkType)
                            {
                                case "Ping":
                                    result = await TestPingAsync(address, settings.TimeoutMs, cancellationToken);
                                    break;
                                case "HTTP":
                                    result = await TestHttpAsync(address, settings.TimeoutMs, cancellationToken);
                                    break;
                                case "TCP":
                                    result = await TestTcpAsync(address, settings.Port, settings.TimeoutMs, cancellationToken);
                                    break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // 취소 예외는 상위로 전파
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"    ✗ {address} {L("Error")}: {ex.Message}", LogLevel.Warning);
                            continue;
                        }

                        if (result)
                        {
                            isConnected = true;
                            connectedAddress = address;
                            connectedMethod = checkType;
                            LogMessage($"    ✓ {address} {L("Log_ConnectionSuccess")}", LogLevel.Success);
                            break; // 하나 성공하면 해당 타입은 중단
                        }
                        else
                        {
                            LogMessage($"    ✗ {address} {L("Log_ConnectionFailed")}", LogLevel.Warning);
                        }
                    }

                    // 하나라도 성공하면 전체 체크 중단
                    if (isConnected)
                    {
                        break;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (isConnected && !_isNetworkConnected)
                {
                    _isNetworkConnected = true;
                    LogMessage($"{L("Log_NetworkConnected")}: {connectedAddress} ({connectedMethod})", LogLevel.Success);

                    // 자동 실행 옵션이 켜져있고 아직 실행하지 않았으면 프로그램 실행
                    if (settings.AutoStartPrograms && !_isProgramsStarted)
                    {
                        await StartEnabledPrograms();
                        
                        // 모든 프로그램 실행 완료 이벤트 발생
                        AllProgramsStarted?.Invoke();
                    }
                }
                else if (!isConnected && _isNetworkConnected)
                {
                    _isNetworkConnected = false;
                    _isProgramsStarted = false;
                    LogMessage(L("Log_NetworkDisconnected"), LogLevel.Error);
                    
                    // 재시도 대기
                    LogMessage($"{settings.RetryDelaySeconds}{L("Log_RetryAfter")}", LogLevel.Warning);
                    await Task.Delay(settings.RetryDelaySeconds * 1000, cancellationToken);
                }
                else if (!isConnected)
                {
                    LogMessage($"{L("Log_AllFailed")} {settings.RetryDelaySeconds}{L("Log_RetryAfter")}", LogLevel.Warning);
                    await Task.Delay(settings.RetryDelaySeconds * 1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                LogMessage(L("Log_CheckCancelled"), LogLevel.Info);
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
                    .Where(p => p.IsEnabled && p.ExecutionMode == "Network")
                    .OrderBy(p => p.ExecutionOrder)
                    .ToList();

                if (!programs.Any())
                {
                    LogMessage(L("Log_NoProgramsToRun"), LogLevel.Warning);
                    return;
                }

                LogMessage($"{L("Log_StartingPrograms")} ({programs.Count})...", LogLevel.Info);

                foreach (var program in programs)
                {
                    await StartProgram(program);
                    
                    // 프로그램 간 1초 대기 (순차 실행)
                    await Task.Delay(1000);
                }

                _isProgramsStarted = true;
                LogMessage(L("Log_AllProgramsComplete"), LogLevel.Success);
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
        /// Ping 테스트
        /// </summary>
        private async Task<bool> TestPingAsync(string address, int timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, timeout);
                cancellationToken.ThrowIfCancellationRequested();
                return reply.Status == IPStatus.Success;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HTTP 테스트
        /// </summary>
        private async Task<bool> TestHttpAsync(string url, int timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
                var response = await httpClient.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// TCP 테스트
        /// </summary>
        private async Task<bool> TestTcpAsync(string address, int port, int timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(address, port);
                var timeoutTask = Task.Delay(timeout, cancellationToken);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                cancellationToken.ThrowIfCancellationRequested();
                return completedTask == connectTask && client.Connected;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
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
