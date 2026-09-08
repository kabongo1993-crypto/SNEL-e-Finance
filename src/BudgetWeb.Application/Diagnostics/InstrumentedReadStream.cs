using System.Diagnostics;

namespace BudgetWeb.Application.Diagnostics;

/// <summary>Enveloppe temporaire — cumule le temps passé en lecture sans modifier le flux.</summary>
internal sealed class InstrumentedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly Stopwatch _readClock = new();

    public InstrumentedReadStream(Stream inner) => _inner = inner;

    public long ReadElapsedMs => _readClock.ElapsedMilliseconds;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        _readClock.Start();
        try
        {
            return _inner.Read(buffer, offset, count);
        }
        finally
        {
            _readClock.Stop();
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _inner.Write(buffer, offset, count);

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        _readClock.Start();
        try
        {
            return await _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }
        finally
        {
            _readClock.Stop();
        }
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _readClock.Start();
        try
        {
            return await _inner.ReadAsync(buffer, cancellationToken);
        }
        finally
        {
            _readClock.Stop();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
