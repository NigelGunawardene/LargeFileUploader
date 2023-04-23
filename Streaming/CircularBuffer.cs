namespace LargeFileUploader.Streaming;

internal class CircularBuffer
{
    private static readonly int MemSize = Environment.SystemPageSize;

    private readonly byte[] mem = new byte[MemSize];

    private int Start { get; set; }

    private int Length { get; set; }

    private int End => (Start + Length) % MemSize;

    private int ReadableChunkLength
    {
        get
        {
            var end = End;
            return Length == 0 ? 0 : Start < end ? end - Start : MemSize - Start;
        }
    }

    private int WritableChunkLength
    {
        get
        {
            var end = End;
            return Length == 0 ? MemSize : Start < end ? MemSize - end : Start - end;
        }
    }

    public event EventHandler<EventArgs>? DataRead;
    public event EventHandler<EventArgs>? DataWritten;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || ReadableChunkLength is not (> 0 and var availableChunkLength)) return 0;

        var chunkLength = Math.Min(count, availableChunkLength);
        Array.Copy(mem, Start, buffer, offset, chunkLength);
        Length -= chunkLength;
        Start = Length == 0 ? 0 : (Start + chunkLength) % MemSize;
        DataRead?.Invoke(this, EventArgs.Empty);
        return chunkLength;
    }

    public int Write(byte[] buffer, int offset, int count)
    {
        if (count == 0 || WritableChunkLength is not (> 0 and var availableChunkLength)) return 0;

        var chunkLength = Math.Min(count, availableChunkLength);
        Array.Copy(buffer, offset, mem, End, chunkLength);
        Length += chunkLength;
        DataWritten?.Invoke(this, EventArgs.Empty);
        return chunkLength;
    }

    public void Complete()
    {
        DataWritten?.Invoke(this, EventArgs.Empty);
    }
}