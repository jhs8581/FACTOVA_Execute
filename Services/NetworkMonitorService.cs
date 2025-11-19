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

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            var settings = _networkRepository.GetSettings();
            
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();

            _monitorTimer = new System.Timers.Timer(settings.CheckIntervalSeconds * 1000);
            _monitorTimer.Elapsed += async (s, e) => await CheckNetworkAndStartPrograms();
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();

            LogMessage("네트워크 모니터링을 시작했습니다.", LogLevel.Info);
            
            // 즉시 한번 체크
            Task.Run(async () => await CheckNetworkAndStartPrograms());
        }

        /// <summary>
        /// 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _monitorTimer = null;
            
            LogMessage("네트워크 모니터링을 중지했습니다.", LogLevel.Warning);
        }

        /// <summary>
        /// 네트워크 확인 및 프로그램 실행
        /// </summary>
        private async Task CheckNetworkAndStartPrograms()
        {
            try
            {
                var settings = _networkRepository.GetSettings();
                var addresses = settings.TargetAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries);

                if (!addresses.Any())
                {
                    LogMessage("등록된 게이트웨이 주소가 없습니다.", LogLevel.Warning);
                    return;
                }

                LogMessage($"네트워크 연결 확인 중... ({settings.CheckType})", LogLevel.Info);

                bool isConnected = false;
                string? connectedAddress = null;

                foreach (var address in addresses)
                {
                    var trimmedAddress = address.Trim();
                    LogMessage($"  → {trimmedAddress} 확인 중...", LogLevel.Info);

                    bool result = false;
                    switch (settings.CheckType)
                    {
                        case "Ping":
                            result = await TestPingAsync(trimmedAddress, settings.TimeoutMs);
                            break;
                        case "HTTP":
                            result = await TestHttpAsync(trimmedAddress, settings.TimeoutMs);
                            break;
                        case "TCP":
                            result = await TestTcpAsync(trimmedAddress, settings.Port, settings.TimeoutMs);
                            break;
                    }

                    if (result)
                    {
                        isConnected = true;
                        connectedAddress = trimmedAddress;
                        LogMessage($"  ✓ {trimmedAddress} 연결 성공!", LogLevel.Success);
                        break;
                    }
                    else
                    {
                        LogMessage($"  ✗ {trimmedAddress} 연결 실패", LogLevel.Warning);
                    }
                }

                if (isConnected && !_isNetworkConnected)
                {
                    _isNetworkConnected = true;
                    LogMessage($"네트워크 연결됨: {connectedAddress}", LogLevel.Success);

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
                    LogMessage("네트워크 연결 끊김", LogLevel.Error);
                    
                    // 재시도 대기
                    LogMessage($"{settings.RetryDelaySeconds}초 후 재시도합니다...", LogLevel.Warning);
                    await Task.Delay(settings.RetryDelaySeconds * 1000);
                }
                else if (!isConnected)
                {
                    LogMessage($"모든 게이트웨이 연결 실패. {settings.RetryDelaySeconds}초 후 재시도...", LogLevel.Warning);
                    await Task.Delay(settings.RetryDelaySeconds * 1000);
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
                    .Where(p => p.IsEnabled)
                    .ToList();

                if (!programs.Any())
                {
                    LogMessage("실행할 프로그램이 없습니다.", LogLevel.Warning);
                    return;
                }

                LogMessage($"프로그램 실행 시작 ({programs.Count}개)...", LogLevel.Info);

                foreach (var program in programs)
                {
                    await StartProgram(program);
                    await Task.Delay(1000); // 프로그램 간 1초 대기
                }

                _isProgramsStarted = true;
                LogMessage("모든 프로그램 실행 완료", LogLevel.Success);
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
        /// Ping 테스트
        /// </summary>
        private async Task<bool> TestPingAsync(string address, int timeout)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, timeout);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HTTP 테스트
        /// </summary>
        private async Task<bool> TestHttpAsync(string url, int timeout)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// TCP 테스트
        /// </summary>
        private async Task<bool> TestTcpAsync(string address, int port, int timeout)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(address, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
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
        }
    }
}
