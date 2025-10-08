using SDL2;
using System.Runtime.InteropServices;

public class JOYPAD
{
  byte JOYP
  {
    get => mem.PPURead(0xFF00);
  }
  byte SsAB = 0xFF;
  byte DPad = 0xFF;
  public MEMORY mem = default;
  public JOYPAD()
  {
  }

  public void Machin_Cycle(IntPtr keyStates)
  {
    byte A_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_Z) >= 1 ? 0x0 : 0x1);
    byte B_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_X) >= 1 ? 0x0 : 0x1);
    byte UP_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_UP) >= 1 ? 0x0 : 0x1);
    byte DOWN_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_DOWN) >= 1 ? 0x0 : 0x1);
    byte LEFT_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_LEFT) >= 1 ? 0x0 : 0x1);
    byte RIGHT_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_RIGHT) >= 1 ? 0x0 : 0x1);
    byte START_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_RETURN) >= 1 ? 0x0 : 0x1);
    byte SELECT_PRESSED = (byte)(Marshal.ReadByte(keyStates, (int)SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE) >= 1 ? 0x0 : 0x1);

    if (START_PRESSED == 0x0)
    {
      Console.WriteLine("Start button pressed!");
    }

    SsAB = SetBit(SsAB, 3, START_PRESSED);
    SsAB = SetBit(SsAB, 2, SELECT_PRESSED);
    SsAB = SetBit(SsAB, 1, B_PRESSED);
    SsAB = SetBit(SsAB, 0, A_PRESSED);

    DPad = SetBit(DPad, 3, DOWN_PRESSED);
    DPad = SetBit(DPad, 2, UP_PRESSED);
    DPad = SetBit(DPad, 1, LEFT_PRESSED);
    DPad = SetBit(DPad, 0, RIGHT_PRESSED);
  }

  public byte SetBit(byte byteToChange, int index, byte value)
  {
    //Clear the bit
    byteToChange &= (byte)((1 << index) ^ 0xFF);
    //Set the bit
    byteToChange |= (byte)(value << index);
    return byteToChange;
  }
  public byte GetBit(int index)
  {
    byte output = JOYP;
    return (byte)((output >> index) & 0x1);
  }

  public byte Read()
  {
    byte upper = (byte)(JOYP & 0xF0);
    if (GetBit(5) == 0x0 && GetBit(4) == 0x0) return (byte)(upper | ((SsAB & DPad) & 0xF));
    if (GetBit(5) == 0x0) return (byte)(upper | (SsAB & 0xF));
    if (GetBit(4) == 0x0) return (byte)(upper | (DPad & 0xF));
    return (byte)(upper | 0xF);
  }
}