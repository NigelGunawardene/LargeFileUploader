using System;
using System.IO;

namespace LargeFileUploader.Streaming;

/// <summary>
/// SeekStream allows seeking on non-seekable streams by buffering read data. The stream is not writable yet.
/// </summary>
public class SeekStream : Stream
{
    private readonly Stream _baseStream;
    private readonly MemoryStream _innerStream;
    private readonly int _bufferSize = 64 * 1024;
    private readonly byte[] _buffer;

    public SeekStream(Stream baseStream)
        : this(baseStream, 64 * 1024)
    {
    }

    public SeekStream(Stream baseStream, int bufferSize) : base()
    {
        _baseStream = baseStream;
        _bufferSize = bufferSize;
        _buffer = new byte[_bufferSize];
        _innerStream = new MemoryStream();
    }

    public override bool CanRead
    {
        get { return _baseStream.CanRead; }
    }

    public override bool CanSeek
    {
        get { return _baseStream.CanRead; }
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override long Position
    {
        get { return _innerStream.Position; }
        set
        {
            if (value > _baseStream.Position)
                FastForward(value);
            _innerStream.Position = value;
        }
    }

    public Stream BaseStream
    {
        get { return _baseStream; }
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    private void FastForward(long position = -1)
    {
        while ((position == -1 || position > this.Length) && ReadChunk() > 0)
        {
            // fast-forward
        }
    }

    private int ReadChunk()
    {
        int thisRead, read = 0;
        long pos = _innerStream.Position;
        do
        {
            thisRead = _baseStream.Read(_buffer, 0, _bufferSize - read);
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
        FastForward(this.Position + 1);
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
            _baseStream.Dispose();
        }
    }

    public override long Length
    {
        get { return _innerStream.Length; }
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