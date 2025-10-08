public struct Pixel
{
  public byte c = 0x0;
  public byte p = 0x0;
  public byte bg_prior = 0x0;
  public Pixel(byte color, byte palette, byte background_priority)
  {
    c = color;
    p = palette;
    bg_prior = background_priority;
  }
}