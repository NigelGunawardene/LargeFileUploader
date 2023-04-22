namespace LargeFileUploader.Streaming;

internal class CircularBuffer
{
    private static readonly int MemSize = Environment.SystemPageSize;
 
    private readonly byte[] mem = new byte[MemSize];
 
    private int Start { get; set; }
 
    private int Length { get; set; }
 
    private int End => (this.Start + this.Length) % MemSize;
 
    private int ReadableChunkLength
    {
        get
        {
            var end = this.End;
            return this.Length == 0 ? 0 : this.Start < end ? end - this.Start : MemSize - this.Start;
        }
    }
 
    private int WritableChunkLength
    {
        get
        {
            var end = this.End;
            return this.Length == 0 ? MemSize : this.Start < end ? MemSize - end : this.Start - end;
        }
    }
 
    public event EventHandler<EventArgs>? DataRead;
    public event EventHandler<EventArgs>? DataWritten;
 
    public int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || this.ReadableChunkLength is not (> 0 and var availableChunkLength))
        {
            return 0;
        }
 
        var chunkLength = Math.Min(count, availableChunkLength);
        Array.Copy(this.mem, this.Start, buffer, offset, chunkLength);
        this.Length -= chunkLength;
        this.Start = this.Length == 0 ? 0 : (this.Start + chunkLength) % MemSize;
        this.DataRead?.Invoke(this, EventArgs.Empty);
        return chunkLength;
    }
 
    public int Write(byte[] buffer, int offset, int count)
    {
        if (count == 0 || this.WritableChunkLength is not (> 0 and var availableChunkLength))
        {
            return 0;
        }
 
        var chunkLength = Math.Min(count, availableChunkLength);
        Array.Copy(buffer, offset, this.mem, this.End, chunkLength);
        this.Length += chunkLength;
        this.DataWritten?.Invoke(this, EventArgs.Empty);
        return chunkLength;
    }
 
    public void Complete()
    {
        this.DataWritten?.Invoke(this, EventArgs.Empty);
    }
}