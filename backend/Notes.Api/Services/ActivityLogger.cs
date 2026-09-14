using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.Models;

namespace Notes.Api.Services;

/// <summary>
/// 行为日志异步写入服务。
/// 请求内通过 <see cref="Enqueue"/> 入队（微秒级，不阻塞业务），
/// 后台 <see cref="LogFlushWorker"/> 攒批（每 20 条或每 3 秒）批量落库。
/// 日志异常不影响业务请求结果。
/// </summary>
public sealed class ActivityLogger : IDisposable
{
    private const int BatchSize = 20;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<ActivityLog> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private int _disposed;

    public ActivityLogger(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _channel = Channel.CreateBounded<ActivityLog>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest // 队列满时丢最旧，保护内存
        });
        _worker = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>入队一条日志（非阻塞）。</summary>
    public void Enqueue(ActivityLog log)
    {
        _channel.Writer.TryWrite(log);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var batch = new List<ActivityLog>(BatchSize);
        var lastFlush = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 等待下一条；同时受 FlushInterval 限时（到点即使不满批也落库）
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(FlushInterval);
                var item = await _channel.Reader.ReadAsync(timeoutCts.Token);
                batch.Add(item);
            }
            catch (OperationCanceledException)
            {
                // 超时或停止：走落库判断
            }

            var due = batch.Count >= BatchSize || DateTime.UtcNow - lastFlush >= FlushInterval;
            if (due && batch.Count > 0)
            {
                await FlushAsync(batch);
                batch.Clear();
                lastFlush = DateTime.UtcNow;
            }
        }

        // 停机兜底：尝试把剩余批次落库（限时，避免拖慢停机）
        if (batch.Count > 0)
        {
            try { await FlushAsync(batch); } catch { /* ignore */ }
        }
    }

    private async Task FlushAsync(List<ActivityLog> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ActivityLogs.AddRange(batch);
            await db.SaveChangesAsync();
        }
        catch
        {
            // 日志落库失败绝不影响业务；静默丢弃本批
        }
    }

    public void Dispose()
    {
        // 幂等：ActivityLogger 既被 HostedService 手动释放，也会被 DI 容器（单例）释放，
        // 必须防止重复释放导致 CTS.Cancel() 抛 ObjectDisposedException。
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _cts.Cancel();
        try { _worker.Wait(FlushTimeout); } catch { /* ignore */ }
        _cts.Dispose();
    }
}

/// <summary>占位宿主：保证 ActivityLogger 作为单例被容器创建并常驻消费队列。</summary>
public sealed class ActivityLogHostedService : IHostedService
{
    private readonly ActivityLogger _logger;

    public ActivityLogHostedService(ActivityLogger logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct)
    {
        _logger.Dispose();
        return Task.CompletedTask;
    }
}
