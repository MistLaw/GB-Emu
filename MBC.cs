public class MBC
{
  List<byte[]> mem;
  int NumberOfBanks = 0;
  int BankSizeInBytes = 0x4000;
  MemoryRegion RAMEnableAddress;
  bool RAMEnabled = false;
  MemoryRegion ROMBankNumberAddress;
  byte ROMBankNumber = 0x1;
  MemoryRegion RAMBankNumberAddress;
  byte RAMBankNumber = 0x0;
  MemoryRegion BankingModeSelectAddress;
  StreamWriter logFile;

  public MBC(StreamWriter logger)
  {
    logFile = logger;
    RAMEnableAddress = new MemoryRegion(0x0000, 0x1FFF);
    ROMBankNumberAddress = new MemoryRegion(0x2000, 0x3FFF);
    RAMBankNumberAddress = new MemoryRegion(0x4000, 0x5FFF);
    BankingModeSelectAddress = new MemoryRegion(0x6000, 0x7FFF);
    mem = new List<byte[]>();
  }

  public void Load(byte[] data)
  {
    NumberOfBanks = data.Length / BankSizeInBytes;
    NumberOfBanks += (data.Length % BankSizeInBytes) > 0 ? 1: 0;
    for (int i = 0; i < NumberOfBanks; i++)
    {
      byte[] NewBank = new byte[BankSizeInBytes];
      for (int p = 0; p < BankSizeInBytes; p++)
      {
        int CurrentAddress = (i * BankSizeInBytes) + p;
        if (CurrentAddress < data.Length)
        {
          NewBank[p] = data[CurrentAddress];
        }
        else
        {
          NewBank[p] = 0xFF;
        }
      }
      mem.Add(NewBank);
    }
  }

  public byte Read(ushort address)
  {
    if (address < BankSizeInBytes)
    {
      return mem[0][address];
    }
    return mem[ROMBankNumber][address - BankSizeInBytes];
  }

  public void Write(ushort address, byte data)
  {
    if (RAMEnableAddress.Contains(address))
    {
      RAMEnabled = (data & 0b11) == 0xA;
    }
    else if (ROMBankNumberAddress.Contains(address))
    {
      ROMBankNumber = (byte)(data & 0x1F);
      if (ROMBankNumber == 0) ROMBankNumber = 1;
      logFile.WriteLine($"Switch to ROM Bank {ROMBankNumber}");
    }
    else if (RAMBankNumberAddress.Contains(address))
    {
      //Figure this out later
      // if (NumberOfBanks > 0x1F)
      // {
      //   ROMBankNumber &= (byte)(data >> 5);
      // }
    }
    else if (BankingModeSelectAddress.Contains(address))
    {
      //No need if RAMBankNumber does nothing
      //Important for large roms greater than 512KB
    }
  }
}