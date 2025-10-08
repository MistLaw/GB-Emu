public class DMA
{
  MEMORY mem;
  int bytes_left = 0;
  int bytes_per_transfer = 160;  
  ushort dma_source = 0x0;
  public DMA(MEMORY memory)
  {
    mem = memory;
  }

  public void Machine_Cycle()
  {
    if (mem.dma_register_changed)
    {
      bytes_left = bytes_per_transfer;
      dma_source = (ushort)(mem.DMARead(mem.dma_register) << 8);
      mem.dma_register_changed = false;
      mem.block_oam = true;
      mem.block_vram = true;
    }
    if (bytes_left > 0)
    {
      ushort offset = (ushort)(bytes_per_transfer - bytes_left);
      ushort destination = (ushort)(0xFE00 + offset);
      ushort source = (ushort)(dma_source + offset);
      byte data = mem.DMARead(source);
      mem.DMAWrite(destination, data);
      bytes_left -= 1;
    }
    else
    {
      mem.block_oam = false;
      mem.block_vram = false;
    }
  }
}