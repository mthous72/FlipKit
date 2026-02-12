using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.Services
{
    public class ServerManagementService : IServerManagementService, IDisposable
    {
        private readonly ILogger<ServerManagementService> _logger;
        private readonly HttpClient _httpClient;

        private Process? _webProcess;
        private Process? _apiProcess;

        private int _webPort;
        private int _apiPort;

        private DateTime? _webStartTime;
        private DateTime? _apiStartTime;

        private readonly List<string> _webLogs = new();
        private readonly List<string> _apiLogs = new();
        private readonly int _maxLogLines = 100;

        private Timer? _healthCheckTimer;
        private bool _disposed;

        private readonly object _lockObject = new();

        public ServerManagementService(ILogger<ServerManagementService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(2);

            // Start health check timer
            _healthCheckTimer = new Timer(HealthCheckCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public async Task<ServerStartResult> StartWebServerAsync(int port)
        {
            try
            {
                _logger.LogInformation("Starting Web server on port {Port}", port);

                // Check if already running
                if (_webProcess != null && !_webProcess.HasExited)
                {
                    _logger.LogWarning("Web server is already running");
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = _webPort,
                        ErrorMessage = "Web server is already running"
                    };
                }

                // Find available port if requested port is in use
                var availablePort = await FindAvailablePortAsync(port, "Web");
                if (availablePort == -1)
                {
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = 0,
                        ErrorMessage = $"Could not find available port starting from {port}"
                    };
                }

                // Get path to Web server executable
                var serverPath = GetServerExecutablePath("FlipKit.Web");
                if (!File.Exists(serverPath))
                {
                    _logger.LogError("Web server executable not found at {Path}", serverPath);
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = 0,
                        ErrorMessage = $"Server executable not found: {serverPath}"
                    };
                }

                // Start the process
                var startInfo = new ProcessStartInfo
                {
                    FileName = serverPath,
                    Arguments = $"--urls http://0.0.0.0:{availablePort}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(serverPath)
                };

                _webProcess = new Process { StartInfo = startInfo };

                // Capture output
                _webProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AddLogLine(_webLogs, $"[OUT] {args.Data}");
                    }
                };

                _webProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AddLogLine(_webLogs, $"[ERR] {args.Data}");
                    }
                };

                _webProcess.Start();
                _webProcess.BeginOutputReadLine();
                _webProcess.BeginErrorReadLine();

                _webPort = availablePort;
                _webStartTime = DateTime.Now;

                _logger.LogInformation("Web server process started with PID {Pid} on port {Port}", _webProcess.Id, availablePort);

                // Wait for server to be healthy
                var healthy = await WaitForHealthyAsync($"http://localhost:{availablePort}/health", TimeSpan.FromSeconds(10));

                if (!healthy)
                {
                    _logger.LogWarning("Web server did not become healthy within timeout");
                }

                return new ServerStartResult
                {
                    Success = true,
                    ActualPort = availablePort,
                    ErrorMessage = healthy ? null : "Server started but health check failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Web server");
                return new ServerStartResult
                {
                    Success = false,
                    ActualPort = 0,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ServerStartResult> StartApiServerAsync(int port)
        {
            try
            {
                _logger.LogInformation("Starting API server on port {Port}", port);

                // Check if already running
                if (_apiProcess != null && !_apiProcess.HasExited)
                {
                    _logger.LogWarning("API server is already running");
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = _apiPort,
                        ErrorMessage = "API server is already running"
                    };
                }

                // Find available port if requested port is in use
                var availablePort = await FindAvailablePortAsync(port, "API");
                if (availablePort == -1)
                {
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = 0,
                        ErrorMessage = $"Could not find available port starting from {port}"
                    };
                }

                // Get path to API server executable
                var serverPath = GetServerExecutablePath("FlipKit.Api");
                if (!File.Exists(serverPath))
                {
                    _logger.LogError("API server executable not found at {Path}", serverPath);
                    return new ServerStartResult
                    {
                        Success = false,
                        ActualPort = 0,
                        ErrorMessage = $"Server executable not found: {serverPath}"
                    };
                }

                // Start the process
                var startInfo = new ProcessStartInfo
                {
                    FileName = serverPath,
                    Arguments = $"--urls http://0.0.0.0:{availablePort}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(serverPath)
                };

                _apiProcess = new Process { StartInfo = startInfo };

                // Capture output
                _apiProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AddLogLine(_apiLogs, $"[OUT] {args.Data}");
                    }
                };

                _apiProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AddLogLine(_apiLogs, $"[ERR] {args.Data}");
                    }
                };

                _apiProcess.Start();
                _apiProcess.BeginOutputReadLine();
                _apiProcess.BeginErrorReadLine();

                _apiPort = availablePort;
                _apiStartTime = DateTime.Now;

                _logger.LogInformation("API server process started with PID {Pid} on port {Port}", _apiProcess.Id, availablePort);

                // Wait for server to be healthy
                var healthy = await WaitForHealthyAsync($"http://localhost:{availablePort}/health", TimeSpan.FromSeconds(10));

                if (!healthy)
                {
                    _logger.LogWarning("API server did not become healthy within timeout");
                }

                return new ServerStartResult
                {
                    Success = true,
                    ActualPort = availablePort,
                    ErrorMessage = healthy ? null : "Server started but health check failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start API server");
                return new ServerStartResult
                {
                    Success = false,
                    ActualPort = 0,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task StopWebServerAsync()
        {
            try
            {
                if (_webProcess == null || _webProcess.HasExited)
                {
                    _logger.LogInformation("Web server is not running");
                    return;
                }

                _logger.LogInformation("Stopping Web server (PID {Pid})", _webProcess.Id);

                // Kill the process immediately (console apps don't respond to CloseMainWindow)
                _webProcess.Kill(entireProcessTree: true);

                // Wait briefly to ensure process is terminated
                await Task.Delay(100);
                if (!_webProcess.WaitForExit(1000))
                {
                    _logger.LogWarning("Web server did not exit after kill command");
                }

                _webProcess.Dispose();
                _webProcess = null;
                _webStartTime = null;

                _logger.LogInformation("Web server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Web server");
            }
        }

        public async Task StopApiServerAsync()
        {
            try
            {
                if (_apiProcess == null || _apiProcess.HasExited)
                {
                    _logger.LogInformation("API server is not running");
                    return;
                }

                _logger.LogInformation("Stopping API server (PID {Pid})", _apiProcess.Id);

                // Kill the process immediately (console apps don't respond to CloseMainWindow)
                _apiProcess.Kill(entireProcessTree: true);

                // Wait briefly to ensure process is terminated
                await Task.Delay(100);
                if (!_apiProcess.WaitForExit(1000))
                {
                    _logger.LogWarning("API server did not exit after kill command");
                }

                _apiProcess.Dispose();
                _apiProcess = null;
                _apiStartTime = null;

                _logger.LogInformation("API server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping API server");
            }
        }

        public ServerStatus GetServerStatus()
        {
            lock (_lockObject)
            {
                return new ServerStatus
                {
                    IsWebRunning = _webProcess != null && !_webProcess.HasExited,
                    IsApiRunning = _apiProcess != null && !_apiProcess.HasExited,
                    WebPort = _webPort,
                    ApiPort = _apiPort,
                    WebStartTime = _webStartTime,
                    ApiStartTime = _apiStartTime
                };
            }
        }

        public string[] GetWebServerLogs()
        {
            lock (_lockObject)
            {
                return _webLogs.ToArray();
            }
        }

        public string[] GetApiServerLogs()
        {
            lock (_lockObject)
            {
                return _apiLogs.ToArray();
            }
        }

        public void ClearWebServerLogs()
        {
            lock (_lockObject)
            {
                _webLogs.Clear();
                _logger.LogInformation("Web server logs cleared");
            }
        }

        public void ClearApiServerLogs()
        {
            lock (_lockObject)
            {
                _apiLogs.Clear();
                _logger.LogInformation("API server logs cleared");
            }
        }

        private void AddLogLine(List<string> logs, string line)
        {
            lock (_lockObject)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                logs.Add($"[{timestamp}] {line}");

                // Keep only last N lines
                if (logs.Count > _maxLogLines)
                {
                    logs.RemoveAt(0);
                }
            }
        }

        private string GetServerExecutablePath(string serverName)
        {
            // Servers are in the "servers" subfolder relative to Desktop executable
            var desktopDir = AppContext.BaseDirectory;
            var serverDir = Path.Combine(desktopDir, "servers");

            // Handle both .exe (Windows) and no extension (Linux)
            var exePath = Path.Combine(serverDir, $"{serverName}.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }

            // Try without .exe extension (Linux)
            var linuxPath = Path.Combine(serverDir, serverName);
            if (File.Exists(linuxPath))
            {
                return linuxPath;
            }

            // Fallback: maybe servers are in the same directory (development)
            exePath = Path.Combine(desktopDir, $"{serverName}.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }

            linuxPath = Path.Combine(desktopDir, serverName);
            if (File.Exists(linuxPath))
            {
                return linuxPath;
            }

            return Path.Combine(serverDir, $"{serverName}.exe"); // Return expected path even if not found
        }

        private async Task<int> FindAvailablePortAsync(int startPort, string serverName)
        {
            // Try up to 10 ports starting from the requested port
            for (int port = startPort; port < startPort + 10; port++)
            {
                if (await IsPortAvailableAsync(port))
                {
                    if (port != startPort)
                    {
                        _logger.LogInformation("{Server} port {Original} was in use, using {Actual} instead",
                            serverName, startPort, port);
                    }
                    return port;
                }
            }

            _logger.LogError("Could not find available port for {Server} starting from {Port}", serverName, startPort);
            return -1;
        }

        private async Task<bool> IsPortAvailableAsync(int port)
        {
            try
            {
                // Try to make a quick connection to see if something is listening
                var response = await _httpClient.GetAsync($"http://localhost:{port}/");
                // If we get any response, port is in use
                return false;
            }
            catch (HttpRequestException)
            {
                // Connection refused = port is available
                return true;
            }
            catch (TaskCanceledException)
            {
                // Timeout = port is available
                return true;
            }
        }

        private async Task<bool> WaitForHealthyAsync(string healthUrl, TimeSpan timeout)
        {
            var deadline = DateTime.Now.Add(timeout);

            // Wait initial delay for server startup
            await Task.Delay(2000);

            while (DateTime.Now < deadline)
            {
                try
                {
                    var response = await _httpClient.GetAsync(healthUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Health check passed for {Url}", healthUrl);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Health check failed, retrying...");
                }

                await Task.Delay(1000);
            }

            _logger.LogWarning("Health check timed out for {Url}", healthUrl);
            return false;
        }

        private void HealthCheckCallback(object? state)
        {
            try
            {
                // Check if processes have crashed
                if (_webProcess != null && _webProcess.HasExited)
                {
                    _logger.LogError("Web server process exited unexpectedly with code {ExitCode}", _webProcess.ExitCode);
                    _webProcess = null;
                    _webStartTime = null;
                }

                if (_apiProcess != null && _apiProcess.HasExited)
                {
                    _logger.LogError("API server process exited unexpectedly with code {ExitCode}", _apiProcess.ExitCode);
                    _apiProcess = null;
                    _apiStartTime = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check callback");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _healthCheckTimer?.Dispose();

            // Stop servers synchronously during disposal
            StopWebServerAsync().GetAwaiter().GetResult();
            StopApiServerAsync().GetAwaiter().GetResult();

            _disposed = true;
        }
    }
}
