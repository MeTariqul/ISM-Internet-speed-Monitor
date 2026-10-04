using System.Net.NetworkInformation;
using System.Diagnostics;

namespace InternetSpeedMonitor.Services;

/// <summary>
/// Service to monitor ALL network interface traffic and calculate real-time speed
/// </summary>
public class NetworkSpeedService : IDisposable
{
    private System.Threading.Timer? _timer;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastCheckTime;
    private bool _isFirstCheck = true;
    
    public event EventHandler<SpeedEventArgs>? SpeedUpdated;
    
    // Speed in bytes per second (raw)
    public double DownloadSpeed { get; private set; }
    public double UploadSpeed { get; private set; }
    
    public bool IsRunning { get; private set; }
    
    private int _refreshIntervalMs = 1000;
    
    public void SetRefreshInterval(int milliseconds)
    {
        _refreshIntervalMs = Math.Max(100, Math.Min(10000, milliseconds));
        if (IsRunning)
        {
            Stop();
            Start();
        }
    }
    
    public void Start()
    {
        if (IsRunning) return;
        
        // Initialize with current network stats - ALL adapters
        var (received, sent) = GetNetworkBytesAll();
        _lastBytesReceived = received;
        _lastBytesSent = sent;
        _lastCheckTime = DateTime.UtcNow;
        _isFirstCheck = true;
        
        Debug.WriteLine($"[NetworkSpeedService] Starting - Initial: {received} bytes received, {sent} bytes sent");
        
        // Update every interval
        _timer = new System.Threading.Timer(UpdateSpeed, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_refreshIntervalMs));
        IsRunning = true;
    }
    
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
    }
    
    private void UpdateSpeed(object? state)
    {
        try
        {
            var (currentReceived, currentSent) = GetNetworkBytesAll();
            var currentTime = DateTime.UtcNow;
            var timeDiff = (currentTime - _lastCheckTime).TotalSeconds;
            
            Debug.WriteLine($"[NetworkSpeedService] Update - Current: {currentReceived} bytes, Last: {_lastBytesReceived} bytes, Diff: {currentReceived - _lastBytesReceived}");
            
            if (timeDiff <= 0 || _isFirstCheck)
            {
                _lastCheckTime = currentTime;
                _lastBytesReceived = currentReceived;
                _lastBytesSent = currentSent;
                _isFirstCheck = false;
                return;
            }
            
            // Calculate speed in bytes per second
            long bytesReceivedDiff = currentReceived - _lastBytesReceived;
            long bytesSentDiff = currentSent - _lastBytesSent;
            
            // Handle network interface resets (counter goes back to 0)
            if (bytesReceivedDiff < 0) bytesReceivedDiff = 0;
            if (bytesSentDiff < 0) bytesSentDiff = 0;
            
            // Store raw bytes per second (we'll convert in display)
            DownloadSpeed = bytesReceivedDiff / timeDiff;
            UploadSpeed = bytesSentDiff / timeDiff;
            
            Debug.WriteLine($"[NetworkSpeedService] Speed - Download: {DownloadSpeed:F2} B/s, Upload: {UploadSpeed:F2} B/s");
            
            _lastCheckTime = currentTime;
            _lastBytesReceived = currentReceived;
            _lastBytesSent = currentSent;
            
            // Fire event on UI thread
            SpeedUpdated?.Invoke(this, new SpeedEventArgs(DownloadSpeed, UploadSpeed));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating speed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets bytes from ALL active network adapters combined
    /// </summary>
    private static (long received, long sent) GetNetworkBytesAll()
    {
        long totalReceived = 0;
        long totalSent = 0;
        
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            
            foreach (var ni in interfaces)
            {
                try
                {
                    var stats = ni.GetIPv4Statistics();
                    totalReceived += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                    Debug.WriteLine($"[NetworkSpeedService] Interface: {ni.Name} - RX: {stats.BytesReceived}, TX: {stats.BytesSent}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting stats for {ni.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting network bytes: {ex.Message}");
        }
        
        return (totalReceived, totalSent);
    }
    
    public void Dispose()
    {
        Stop();
    }
}

public class SpeedEventArgs : EventArgs
{
    // Speed in bytes per second (raw)
    public double DownloadSpeed { get; }
    public double UploadSpeed { get; }
    
    public SpeedEventArgs(double downloadSpeed, double uploadSpeed)
    {
        DownloadSpeed = downloadSpeed;
        UploadSpeed = uploadSpeed;
    }
}
