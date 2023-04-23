namespace LargeFileUploader.Streaming;

public class PipeStream : Stream
{
    private readonly CircularBuffer mem = new();
    private bool complete;
    private long length;

    public override bool CanRead { get; } = true;
    public override bool CanSeek { get; } = false;
    public override bool CanWrite { get; } = true;
    public override long Length => complete ? length : 0L;

    public override long Position
    {
        get => 0L;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Task.Run(() => ReadAsync(buffer, offset, count)).Result;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Task.Run(() => WriteAsync(buffer, offset, count));
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            SemaphoreSlim signal;

            void DataWritten(object o, EventArgs e)
            {
                mem.DataWritten -= DataWritten!;
                signal.Release();
            }

            lock (mem)
            {
                var result = mem.Read(buffer, offset, count);
                total += result;
                offset += result;
                count -= result;
                if (count == 0 || complete) return total;

                signal = new SemaphoreSlim(0, 1);
                mem.DataWritten += DataWritten!;
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        while (true)
        {
            SemaphoreSlim signal;

            void DataRead(object o, EventArgs e)
            {
                mem.DataRead -= DataRead!;
                signal.Release();
            }

            lock (mem)
            {
                var result = mem.Write(buffer, offset, count);
                length += result;
                offset += result;
                count -= result;
                if (count == 0) break;

                signal = new SemaphoreSlim(0, 1);
                mem.DataRead += DataRead!;
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    public void Complete()
    {
        lock (mem)
        {
            mem.Complete();
        }

        complete = true;
    }
}