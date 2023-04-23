namespace LargeFileUploader.Streaming;

/// <summary>
///     PeekableStream wraps a Stream and can be used to peek ahead in the underlying stream,
///     without consuming the bytes. In other words, doing Peek() will allow you to look ahead in the stream,
///     but it won't affect the result of subsequent Read() calls.
///     This is sometimes necessary, e.g. for peeking at the magic number of a stream of bytes and decide which
///     stream processor to hand over the stream.
/// </summary>
public class PeekableStream : Stream
{
    private readonly byte[] lookAheadBuffer;
    private readonly Stream underlyingStream;

    private int lookAheadIndex;

    public PeekableStream(Stream underlyingStream, int maxPeekBytes)
    {
        this.underlyingStream = underlyingStream;
        lookAheadBuffer = new byte[maxPeekBytes];
    }

    public override bool CanRead => true;

    public override long Position
    {
        get => underlyingStream.Position - lookAheadIndex;
        set
        {
            underlyingStream.Position = value;
            lookAheadIndex =
                0; // this needs to be done AFTER the call to underlyingStream.Position, as that might throw NotSupportedException, 
            // in which case we don't want to change the lookAhead status
        }
    }

    // from here on, only simple delegations to underlyingStream

    public override bool CanSeek => underlyingStream.CanSeek;
    public override bool CanWrite => underlyingStream.CanWrite;
    public override bool CanTimeout => underlyingStream.CanTimeout;

    public override int ReadTimeout
    {
        get => underlyingStream.ReadTimeout;
        set => underlyingStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => underlyingStream.WriteTimeout;
        set => underlyingStream.WriteTimeout = value;
    }

    public override long Length => underlyingStream.Length;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            underlyingStream.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>
    ///     Peeks at a maximum of count bytes, or less if the stream ends before that number of bytes can be read.
    ///     Calls to this method do not influence subsequent calls to Read() and Peek().
    ///     Please note that this method will always peek count bytes unless the end of the stream is reached before that - in
    ///     contrast to the Read()
    ///     method, which might read less than count bytes, even though the end of the stream has not been reached.
    /// </summary>
    /// <param name="buffer">
    ///     An array of bytes. When this method returns, the buffer contains the specified byte array with the values between
    ///     offset and
    ///     (offset + number-of-peeked-bytes - 1) replaced by the bytes peeked from the current source.
    /// </param>
    /// <param name="offset">
    ///     The zero-based byte offset in buffer at which to begin storing the data peeked from the current
    ///     stream.
    /// </param>
    /// <param name="count">The maximum number of bytes to be peeked from the current stream.</param>
    /// <returns>
    ///     The total number of bytes peeked into the buffer. If it is less than the number of bytes requested then the
    ///     end of the stream has been reached.
    /// </returns>
    public virtual int Peek(byte[] buffer, int offset, int count)
    {
        if (count > lookAheadBuffer.Length)
            throw new ArgumentOutOfRangeException("count",
                "must be smaller than peekable size, which is " + lookAheadBuffer.Length);

        while (lookAheadIndex < count)
        {
            var bytesRead = underlyingStream.Read(lookAheadBuffer, lookAheadIndex, count - lookAheadIndex);

            if (bytesRead == 0) // end of stream reached
                break;

            lookAheadIndex += bytesRead;
        }

        var peeked = Math.Min(count, lookAheadIndex);
        Array.Copy(lookAheadBuffer, 0, buffer, offset, peeked);
        return peeked;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesTakenFromLookAheadBuffer = 0;
        if (count > 0 && lookAheadIndex > 0)
        {
            bytesTakenFromLookAheadBuffer = Math.Min(count, lookAheadIndex);
            Array.Copy(lookAheadBuffer, 0, buffer, offset, bytesTakenFromLookAheadBuffer);
            count -= bytesTakenFromLookAheadBuffer;
            offset += bytesTakenFromLookAheadBuffer;
            lookAheadIndex -= bytesTakenFromLookAheadBuffer;
            if (lookAheadIndex > 0) // move remaining bytes in lookAheadBuffer to front
                // copying into same array should be fine, according to http://msdn.microsoft.com/en-us/library/z50k9bft(v=VS.90).aspx :
                // "If sourceArray and destinationArray overlap, this method behaves as if the original values of sourceArray were preserved
                // in a temporary location before destinationArray is overwritten."
                Array.Copy(lookAheadBuffer, lookAheadBuffer.Length - bytesTakenFromLookAheadBuffer + 1, lookAheadBuffer,
                    0, lookAheadIndex);
        }

        return count > 0
            ? bytesTakenFromLookAheadBuffer + underlyingStream.Read(buffer, offset, count)
            : bytesTakenFromLookAheadBuffer;
    }

    public override int ReadByte()
    {
        if (lookAheadIndex > 0)
        {
            lookAheadIndex--;
            var firstByte = lookAheadBuffer[0];
            if (lookAheadIndex > 0) // move remaining bytes in lookAheadBuffer to front
                Array.Copy(lookAheadBuffer, 1, lookAheadBuffer, 0, lookAheadIndex);
            return firstByte;
        }

        return underlyingStream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var ret = underlyingStream.Seek(offset, origin);
        lookAheadIndex =
            0; // this needs to be done AFTER the call to underlyingStream.Seek(), as that might throw NotSupportedException,
        // in which case we don't want to change the lookAhead status
        return ret;
    }

    public override void Flush()
    {
        underlyingStream.Flush();
    }

    public override void SetLength(long value)
    {
        underlyingStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        underlyingStream.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        underlyingStream.WriteByte(value);
    }
}