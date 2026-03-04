using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class FpsMonitor
{
    private TraceEventSession? _session;
    private int _frameCount;
    private Stopwatch _watch = new Stopwatch();

    public event Action<int>? OnFpsUpdated;

    public void Start()
    {
        Task.Run(() =>
        {
            _watch.Start();

            using (_session = new TraceEventSession("FPSMonitorSession"))
            {
                _session.EnableProvider("Microsoft-Windows-DXGI");

                _session.Source.Dynamic.All += traceEvent =>
                {
                    if (traceEvent.EventName.Contains("Present"))
                    {
                        _frameCount++;

                        if (_watch.ElapsedMilliseconds >= 1000)
                        {
                            int fps = _frameCount;
                            _frameCount = 0;
                            _watch.Restart();

                            OnFpsUpdated?.Invoke(fps);
                        }
                    }
                };

                _session.Source.Process();
            }
        });
    }

    public void Stop()
    {
        _session?.Dispose();
    }
}
