using System.Buffers.Binary;
using System.Text;
using Acme.PlcComm.Interfaces;

namespace Acme.PlcComm.Common;

/// <summary>
/// 大端字节序转换 (用于西门子S7、欧姆龙FINS)
/// </summary>
public class BigEndianTransform : IByteTransform
{
    public static readonly BigEndianTransform Instance = new();

    public short ToInt16(byte[] buffer, int index) => BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(index, 2));
    public ushort ToUInt16(byte[] buffer, int index) => BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(index, 2));
    public int ToInt32(byte[] buffer, int index) => BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(index, 4));
    public uint ToUInt32(byte[] buffer, int index) => BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(index, 4));
    public long ToInt64(byte[] buffer, int index) => BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(index, 8));
    public ulong ToUInt64(byte[] buffer, int index) => BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(index, 8));
    public float ToFloat(byte[] buffer, int index) => BinaryPrimitives.ReadSingleBigEndian(buffer.AsSpan(index, 4));
    public double ToDouble(byte[] buffer, int index) => BinaryPrimitives.ReadDoubleBigEndian(buffer.AsSpan(index, 8));
    
    public bool ToBool(byte[] buffer, int index)
    {
        if (buffer == null || buffer.Length <= index)
            return false;
        return buffer[index] != 0;
    }

    public string ToString(byte[] buffer, int index, int length, Encoding encoding)
    {
        if (buffer == null || buffer.Length < index + length)
            return string.Empty;
        return encoding.GetString(buffer, index, length).TrimEnd('\0');
    }

    public byte[] GetBytes(short value)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(ushort value)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(int value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(uint value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(long value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(ulong value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(float value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(double value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(bool value) => new byte[] { (byte)(value ? 1 : 0) };

    public byte[] GetBytes(string value, int length, Encoding encoding)
    {
        var bytes = encoding.GetBytes(value);
        if (bytes.Length >= length)
            return bytes[..length];
        
        var result = new byte[length];
        Array.Copy(bytes, result, bytes.Length);
        return result;
    }
}

/// <summary>
/// 小端字节序转换 (用于三菱MC)
/// </summary>
public class LittleEndianTransform : IByteTransform
{
    public static readonly LittleEndianTransform Instance = new();

    public short ToInt16(byte[] buffer, int index) => BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(index, 2));
    public ushort ToUInt16(byte[] buffer, int index) => BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(index, 2));
    public int ToInt32(byte[] buffer, int index) => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(index, 4));
    public uint ToUInt32(byte[] buffer, int index) => BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(index, 4));
    public long ToInt64(byte[] buffer, int index) => BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(index, 8));
    public ulong ToUInt64(byte[] buffer, int index) => BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(index, 8));
    public float ToFloat(byte[] buffer, int index) => BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(index, 4));
    public double ToDouble(byte[] buffer, int index) => BinaryPrimitives.ReadDoubleLittleEndian(buffer.AsSpan(index, 8));
    
    public bool ToBool(byte[] buffer, int index)
    {
        if (buffer == null || buffer.Length <= index)
            return false;
        return buffer[index] != 0;
    }

    public string ToString(byte[] buffer, int index, int length, Encoding encoding)
    {
        if (buffer == null || buffer.Length < index + length)
            return string.Empty;
        return encoding.GetString(buffer, index, length).TrimEnd('\0');
    }

    public byte[] GetBytes(short value)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(ushort value)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(int value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(uint value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(long value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(ulong value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(float value)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(double value)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        return buffer;
    }

    public byte[] GetBytes(bool value) => new byte[] { (byte)(value ? 1 : 0) };

    public byte[] GetBytes(string value, int length, Encoding encoding)
    {
        var bytes = encoding.GetBytes(value);
        if (bytes.Length >= length)
            return bytes[..length];
        
        var result = new byte[length];
        Array.Copy(bytes, result, bytes.Length);
        return result;
    }
}
