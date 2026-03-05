using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareMonitor
{
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

                try
                {
                    _session = new TraceEventSession(
                        "FPSMonitorSession_" + Process.GetCurrentProcess().Id);

                    _session.EnableProvider("Microsoft-Windows-DXGI");

                    _session.Source.Dynamic.All += traceEvent =>
                    {
                        if (traceEvent.EventName.Contains("Present"))
                        {
                            Interlocked.Increment(ref _frameCount);

                            if (_watch.ElapsedMilliseconds >= 1000)
                            {
                                int fps = Interlocked.Exchange(ref _frameCount, 0);
                                _watch.Restart();

                                OnFpsUpdated?.Invoke(fps);
                            }
                        }
                    };

                    _session.Source.Process();
                }
                catch
                {
                    // sesson açılamazsa crash engelle
                }
            });
        }

        public void Stop()
        {
            try
            {
                _session?.Dispose();
            }
            catch { }
        }
    }
}
