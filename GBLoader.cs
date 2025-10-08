using System;
using System.IO;

public class GBLoader
{
    public static byte[] LoadROM(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"ROM not found at path: {path}");
        }

        byte[] romData = File.ReadAllBytes(path);
        return romData;
    }
}