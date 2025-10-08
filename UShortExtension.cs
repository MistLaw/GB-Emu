public static class UShortExtensions
{
    public static byte Low(this ushort value) => (byte)(value & 0xFF);
    public static byte High(this ushort value) => (byte)(value >> 8);
}