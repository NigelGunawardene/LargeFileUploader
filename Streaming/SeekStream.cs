namespace LargeFileUploader.Streaming;

/// <summary>
///     SeekStream allows seeking on non-seekable streams by buffering read data. The stream is not writable yet.
/// </summary>
public class SeekStream : Stream
{
    private readonly byte[] _buffer;
    private readonly int _bufferSize = 64 * 1024;
    private readonly MemoryStream _innerStream;

    public SeekStream(Stream baseStream)
        : this(baseStream, 64 * 1024)
    {
    }

    public SeekStream(Stream baseStream, int bufferSize)
    {
        BaseStream = baseStream;
        _bufferSize = bufferSize;
        _buffer = new byte[_bufferSize];
        _innerStream = new MemoryStream();
    }

    public override bool CanRead => BaseStream.CanRead;

    public override bool CanSeek => BaseStream.CanRead;

    public override bool CanWrite => false;

    public override long Position
    {
        get => _innerStream.Position;
        set
        {
            if (value > BaseStream.Position)
                FastForward(value);
            _innerStream.Position = value;
        }
    }

    public Stream BaseStream { get; }

    public override long Length => _innerStream.Length;

    public override void Flush()
    {
        BaseStream.Flush();
    }

    private void FastForward(long position = -1)
    {
        while ((position == -1 || position > Length) && ReadChunk() > 0)
        {
            // fast-forward
        }
    }

    private int ReadChunk()
    {
        int thisRead, read = 0;
        var pos = _innerStream.Position;
        do
        {
            thisRead = BaseStream.Read(_buffer, 0, _bufferSize - read);
            _innerStream.Write(_buffer, 0, thisRead);
            read += thisRead;
        } while (read < _bufferSize && thisRead > 0);

        _innerStream.Position = pos;
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        FastForward(offset + count);
        return _innerStream.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
        FastForward(Position + 1);
        return base.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long pos = -1;
        if (origin == SeekOrigin.Begin)
            pos = offset;
        else if (origin == SeekOrigin.Current)
            pos = _innerStream.Position + offset;
        FastForward(pos);
        return _innerStream.Seek(offset, origin);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            _innerStream.Dispose();
            BaseStream.Dispose();
        }
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}