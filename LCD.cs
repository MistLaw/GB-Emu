using System;
using System.Runtime.InteropServices;
using SDL2;

public class LCD
{
  uint[] pixels;
  // int index = 0;
  public bool frame_ready = false;
  int w, h;

  public LCD(int width, int height)
  {
    w = width;
    h = height;
    pixels = new uint[width * height];
  }

  public void Push(Pixel pixel, int index)
  {
    int shadeIndex = (pixel.p >> (pixel.c * 2)) & 0b11;

    // Convert to grayscale intensity
    byte shade = shadeIndex switch
    {
      0 => 255, // white
      1 => 170, // light gray
      2 => 85,  // dark gray
      3 => 0,   // black
      _ => 0
    };

    // Pack to uint color
    uint color = (uint)(0xFF << 24 | shade << 16 | shade << 8 | shade);
    if (index < pixels.Length)
    {
      pixels[index] = color;
    }
    else
    {
      Console.WriteLine($"Out of bounds by: {pixels.Length - index}");
    }
    // Console.WriteLine($"Pixels pushed: {index}");
    // index++;
  }

  public void MergePush(Pixel background, Pixel sprite, byte LX, byte LY)
  {
    int index = (LY * w) + LX;
    if (sprite.c == 0 || ((background.c != 0) && (sprite.bg_prior != 0)))
    {
      Push(background, index);
    }
    else
    {
      Push(sprite, index);
    }
  }

  public void Reset()
  {
    // index = 0;
    frame_ready = false;
  }

  public void FrameReady()
  {
    frame_ready = true;
  }

  public void CopyFrame(IntPtr texture)
  {
    // Lock the texture
    IntPtr texturePtr;
    int pitch;
    SDL.SDL_LockTexture(texture, IntPtr.Zero, out texturePtr, out pitch);

    // Convert uint[] to byte[] (4 bytes per pixel)
    byte[] bytes = new byte[pixels.Length * 4];
    Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);

    // Copy the byte array into the texture memory
    Marshal.Copy(bytes, 0, texturePtr, bytes.Length);

    SDL.SDL_UnlockTexture(texture);
    Reset();
  }
}