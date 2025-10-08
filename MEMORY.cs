public class MemoryRegion
{
    public ushort Start { get; set; }
    public ushort End { get; set; }

    public MemoryRegion(ushort start, ushort end)
    {
        Start = start;
        End = end;
    }

    public bool Contains(ushort address) => address >= Start && address <= End;
}

public class MEMORY
{
  byte[] mem;
  MBC mbc;
  public bool dma_register_changed = false;
  public bool block_vram = false;
  public bool block_oam = false;

  public bool block_vram_hard = false;
  public bool block_oam_hard = false;
  int MBC_type = 0;
  public bool BOOTRomDisabled = false;

  public ushort dma_register = 0xFF46;
  ushort JOYP = 0xFF00;
  JOYPAD joypad;
  public MemoryRegion boot = new MemoryRegion(0x0000, 0x0FF);
  public MemoryRegion rom = new MemoryRegion(0x0000, 0x7FFF);
  public MemoryRegion vram = new MemoryRegion(0x8000, 0x9FFF);
  public MemoryRegion wram = new MemoryRegion(0xC000, 0xFFDF);
  public MemoryRegion oam = new MemoryRegion(0xFE00, 0xFE9F);
  StreamWriter logFile;
  public int LoggedLines = 0;
  public MEMORY(MBC MemoryBankController,JOYPAD joyp, StreamWriter logger)
  {
    logFile = logger;
    joypad = joyp;
    //Size of the addressable space
    mem = new byte[0x10000];
    mem[JOYP] = 0xFF;
    mbc = MemoryBankController;
    Console.WriteLine("New memory created!");
  }

  public byte Read(ushort address)
  {
    if (address == JOYP) return joypad.Read();
    if (rom.Contains(address) && (BOOTRomDisabled || !boot.Contains(address))) return mbc.Read(address);
    if ((block_oam || block_oam_hard) && oam.Contains(address)) return 0xFF;
    if ((block_vram || block_vram_hard) && vram.Contains(address)) return 0xFF;

    return mem[address];
  }

  public byte DMARead(ushort address)
  {
    if (block_oam_hard && oam.Contains(address)) return 0xFF;
    if (block_vram_hard && vram.Contains(address)) return 0xFF;
    return mem[address];
  }

  public byte PPURead(ushort address)
  {
    return mem[address];
  }

  public byte ReadIndirect(byte address)
  {
    ushort addr = (ushort)((0xFF << 8) | address);
    return Read(addr);
  }

  public void WriteIndirect(byte address, byte data)
  {
    ushort addr = (ushort)((0xFF << 8) | address);
    Write(addr, data);
  }
  public void Write(ushort address, byte data)
  {
    // if (wram.Contains(address) && !(address == 0xFF04))
    // {
    //   Console.WriteLine($"Address: {address:x4}, Data: {data:x4}");
    // }
    //Check if DMA address is being set
    dma_register_changed = dma_register == address;
    if (address == JOYP)
    {
      //Only sets the 5th and 4th bit... hopefully?
      mem[address] = (byte)((mem[address] & 0xCF) | (data & 0x30));
      return;
    }
    if (address == 0xFF50)
    {
      Console.WriteLine("Game started!");
      BOOTRomDisabled = true;
    }

    // if (address > 0xFF00 && !(address == 0xFF04) && !(address == 0xFF81) && !(address == 0xFF82) && !(address == 0xFF83))
    // {
    //   logFile.WriteLine($"Address: {address:x4}, Data: {data:x4}");
    //   LoggedLines += 1;
    // }

    // if ((address == 0xFF01) || (address == 0xFF02))
    // {
    //   logFile.WriteLine($"Address: {address:x4}, Data: {data:x4}, Character: '{(char)data}'");
    //   LoggedLines += 1;
    // }

    // if (address == 0xC2A6)
    // {
    //   Console.WriteLine($"Address: {address:x4}, Data: {data:x4}");
    // }

    // if (address == 0xFF01)
    // {
    //   Console.WriteLine($"Address: {address:x4}, Data: {data:x4}");
    // }
    // if (address == 0xFF02) Console.WriteLine($"Address: {address:x4}, Data: {data:x4}");

    // ROM
    if (rom.Contains(address) && (BOOTRomDisabled || !boot.Contains(address))) mbc.Write(address, data);

    //Block OAM and VRAM if they are blocked (usually by the DMA)
    if ((block_oam || block_oam_hard) && oam.Contains(address)) return;
    if ((block_vram || block_vram_hard) && vram.Contains(address))
    {
      return;
    }

    //RAM
    mem[address] = data;
  }

  public void DMAWrite(ushort address, byte data)
  {
    //Check if DMA address is being set
    dma_register_changed = dma_register == address;
    // if (address == 0xFF44)
    // {
    //   Console.WriteLine($"Address: {address:x4}, Data: {data:x4}");
    // }

    // ROM
    if (address <= 0x7FFF) return;

    //Block OAM and VRAM if they are blocked (usually by the DMA)
    if (block_oam_hard && oam.Contains(address)) return;
    if (block_vram_hard && vram.Contains(address)) return;

    //RAM
    mem[address] = data;
  }

  public void PPUWrite(ushort address, byte data)
  {
    // if (address == JOYP)
    // {
    //   // Console.WriteLine("Test");
    // }
    //Check if DMA address is being set
    dma_register_changed = dma_register == address;
    // ROM
    if (address <= 0x7FFF) return;

    //RAM
    mem[address] = data;
  }

  public void LoadBootROM(byte[] data)
  {
    for (int i = 0; i < data.Length; i++)
    {
      mem[i] = data[i];
    }
  }

  public void LoadToMBC(byte[] data)
  {
    mbc.Load(data);
  }
}