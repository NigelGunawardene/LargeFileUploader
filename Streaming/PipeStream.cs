namespace LargeFileUploader.Streaming;

public class PipeStream : Stream
{
  private readonly CircularBuffer mem = new();
  private bool complete;
  private long length;
 
  public override bool CanRead { get; } = true;
  public override bool CanSeek { get; } = false;
  public override bool CanWrite { get; } = true;
  public override long Length => this.complete ? this.length : 0L;
 
  public override long Position
  {
    get => 0L;
    set => throw new NotSupportedException();
  }
 
  public override void Flush() { }
 
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
    return Task.Run(() => this.ReadAsync(buffer, offset, count)).Result;
  }
 
  public override void Write(byte[] buffer, int offset, int count)
  {
    Task.Run(() => this.WriteAsync(buffer, offset, count));
  }
 
  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    var total = 0;
    while (true)
    {
      SemaphoreSlim signal;
 
      void DataWritten(object o, EventArgs e)
      {
        this.mem.DataWritten -= DataWritten!;
        signal.Release();
      }
 
      lock (this.mem)
      {
        var result = this.mem.Read(buffer, offset, count);
        total += result;
        offset += result;
        count -= result;
        if (count == 0 || this.complete)
        {
          return total;
        }
 
        signal = new SemaphoreSlim(0, 1);
        this.mem.DataWritten += DataWritten!;
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
        this.mem.DataRead -= DataRead!;
        signal.Release();
      }
 
      lock (this.mem)
      {
        var result = this.mem.Write(buffer, offset, count);
        this.length += result;
        offset += result;
        count -= result;
        if (count == 0)
        {
          break;
        }
 
        signal = new SemaphoreSlim(0, 1);
        this.mem.DataRead += DataRead!;
      }
 
      await signal.WaitAsync(cancellationToken);
    }
  }
 
  public void Complete()
  {
    lock (this.mem)
    {
      this.mem.Complete();
    }
 
    this.complete = true;
  }
}