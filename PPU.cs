public class PPU
{
  struct BackgroundPixelFetcher
  {

    //Current pixel that we are attempting to load on the scan line
    //Remeber that the LCD draw independantly to the PPU this is just what the Fetcher is currently working on.
    byte CounterPosX = 0;
    int step = 1;
    byte TileRowWidth = 32;
    byte TileDimensions = 8;
    byte TileByteCount = 16;

    byte TileRowLow = 0x0;
    byte TileRowHigh = 0x0;

    ushort TileHighAddress = 0x0;

    ushort TileLowAddress = 0x0;
    MEMORY mem;
    PixelFIFO fifo;

    ushort TileBaseData = 0x8000;
    ushort TileBaseMap = 0x9800;
    bool window = false;
    bool SignedTileData = true;
    //Tracks how many window lines have been DRAWN
    int WindowLineCounter = 0;

    public BackgroundPixelFetcher(MEMORY memory, PixelFIFO FIFO)
    {
      fifo = FIFO;
      mem = memory;
    }

    public void Reset()
    {
      window = false;
      WindowLineCounter = 0;
      TileRowLow = 0x0;
      TileRowHigh = 0x0;
      TileHighAddress = 0x0;
      TileLowAddress = 0x0;
      CounterPosX = 0;
      step = 1;
    }
    public void CheckWindowOrBackground(byte LY, byte WX, byte WY, bool WindowDisplayEnable)
    {
      //Checks if we are currently fetching window background tiles
      //WindowDisplayEnable is a master switch
      window = (WY <= LY) && ((WX - 7) <= CounterPosX) && WindowDisplayEnable;
    }

    public void SelectTileAddresses(bool BGTileMapSelect, bool TileDataSelect, bool WindowTileMapSelect)
    {
      SignedTileData = !TileDataSelect;
      TileBaseData = TileDataSelect ? (ushort)0x8000 : (ushort)0x9000;
      if (window)
      {
        TileBaseMap = WindowTileMapSelect ? (ushort)0x9C00 : (ushort)0x9800;
      }
      else
      {
        TileBaseMap = BGTileMapSelect ? (ushort)0x9C00 : (ushort)0x9800;
      }
    }
    public void Half_Machine_Cycle(byte LY, byte SCY, byte SCX, byte BGP, bool BGWindowEnable)
    {
      //The steps each represent 2 t-cycles or half a machine cycle
      //This function needs to be run twice per machine cycle (or the sprite fetcher needs to run)
      if (step == 1)
      {
        //Fetch tile number
        ushort tileColumn = window ? (ushort)(CounterPosX & 0x1F) : (ushort)((ushort)(CounterPosX + (SCX / TileDimensions)) & 0x1F);
        ushort tileRow = window ? (ushort)(WindowLineCounter / TileDimensions) : (ushort)(((LY + SCY) & 0xFF) / TileDimensions);
        ushort tileAddress = (ushort)(TileBaseMap + (tileRow * TileRowWidth) + tileColumn);

        byte tileNumber = mem.PPURead(tileAddress);

        ushort tileDataOffset = window ? (ushort)(2 * (WindowLineCounter % TileDimensions)) : (ushort)(2 * ((LY + SCY) % TileDimensions));
        if (SignedTileData)
        {
          int signedTileNumber = (sbyte)tileNumber;
          TileLowAddress = (ushort)(TileBaseData + (signedTileNumber * TileByteCount) + tileDataOffset);
          TileHighAddress = (ushort)(TileBaseData + (signedTileNumber * TileByteCount) + tileDataOffset + 1);
        }
        else
        {
          TileLowAddress = (ushort)(TileBaseData + (tileNumber * TileByteCount) + tileDataOffset);
          TileHighAddress = (ushort)(TileBaseData + (tileNumber * TileByteCount) + tileDataOffset + 1);
        }
        step = 2;
        return;
      }
      else if (step == 2)
      {
        TileRowLow = mem.PPURead(TileLowAddress);
        step = 3;
        return;
      }
      else if (step == 3)
      {
        TileRowHigh = mem.PPURead(TileHighAddress);
        step = 4;
        return;
      }
      else if (step == 4)
      {
        //Attempt to write to FIFO
        //IF successful reset to step 1 and increment counter
        if (fifo.PushPixelsBackground(TileRowLow, TileRowHigh, BGP, BGWindowEnable))
        {
          step = 1;
          CounterPosX += 1;
        }
        return;
      }
    }
  }

  struct SpritePixelFetcher
  {
    int step = 1;
    byte TileRowWidth = 32;
    byte TileDimensions = 8;
    byte TileByteCount = 16;

    byte TileRowLow = 0x0;
    byte TileRowHigh = 0x0;

    ushort TileHighAddress = 0x0;

    ushort TileLowAddress = 0x0;
    MEMORY mem;
    PixelFIFO fifo;

    ushort TileBaseData = 0x8000;
    //Tracks how many window lines have been DRAWN

    public SpritePixelFetcher(MEMORY memory, PixelFIFO FIFO)
    {
      fifo = FIFO;
      mem = memory;
    }

    public void Reset()
    {
      TileRowLow = 0x0;
      TileRowHigh = 0x0;
      TileHighAddress = 0x0;
      TileLowAddress = 0x0;
      step = 1;
    }

    public bool Half_Machine_Cycle(Sprite sprite, byte LY, byte OBP0, byte OBP1)
    {
      //The steps each represent 2 t-cycles or half a machine cycle
      //This function needs to be run twice per machine cycle (or the sprite fetcher needs to run)
      if (step == 1)
      {
        //Fetch tile number
        byte tileNumber = sprite.index;

        int line = LY - (sprite.y - 16);
        bool YFlip = ((sprite.attributes >> 6) & 1) == 1;
        if (line > 16 || line < 0) throw new Exception("The value passed into the Sprite fetcher exceeds whats expected!");
        if (sprite.tallSprite)
        {
          byte tallTileNumber = (byte)(tileNumber & 0xFE);
          if (line >= 8)
          {
            line -= 8;
            tallTileNumber = (byte)(tileNumber | 1);
          }
          if (YFlip)
          {
            line = (byte)(7 - line);
          }
          ushort tileDataOffset = (ushort)(2 * line);
          TileLowAddress = (ushort)(TileBaseData + (tallTileNumber * 16) + tileDataOffset);
          TileHighAddress = (ushort)(TileBaseData + (tallTileNumber * 16) + tileDataOffset + 1);
        }
        else
        {
          if (YFlip)
          {
            line = (byte)(7 - line);
          }
          ushort tileDataOffset = (ushort)(2 * line);
          TileLowAddress = (ushort)(TileBaseData + (tileNumber * 16) + tileDataOffset);
          TileHighAddress = (ushort)(TileBaseData + (tileNumber * 16) + tileDataOffset + 1);
        }

        step = 2;
        return false;
      }
      else if (step == 2)
      {
        TileRowLow = mem.PPURead(TileLowAddress);
        step = 3;
        return false;
      }
      else if (step == 3)
      {
        TileRowHigh = mem.PPURead(TileHighAddress);
        step = 4;
        return false;
      }
      else if (step == 4)
      {
        //Attempt to write to FIFO
        //If successful reset to step 1
        byte palette = ((sprite.attributes >> 4) & 1) == 1 ? OBP1 : OBP0;
        byte priority = (byte)((sprite.attributes >> 7) & 1);
        bool XFlip = ((sprite.attributes >> 5) & 1) == 1;
        if (fifo.PushPixelsSprite(TileRowLow, TileRowHigh, palette, priority, XFlip))
        {
          step = 1;
          return true;
        }
        return false;
      }
      else
      {
        throw new Exception("Step exceeded 4!");
      }
    }
  }
  public struct PixelFIFO
  {
    public struct ShiftRegister
    {
      Queue<Pixel> pixels;
      public Pixel pixel = default;

      public byte AvailableSpace
      {
        get => (byte)(max_size - pixels.Count);
      }
      int max_size = 8;
      public ShiftRegister()
      {
        pixels = new Queue<Pixel>();
      }

      public bool Push(byte color, byte palette, byte background_priority)
      {
        if (pixels.Count < max_size)
        {
          pixels.Enqueue(new Pixel(color, palette, background_priority));
          return true;
        }
        return false;
      }

      public bool Pop()
      {
        if (pixels.Count > 0)
        {
          pixel = pixels.Dequeue();
          return true;
        }
        pixel = default;
        return false;
      }
      public void Reset()
      {
        pixels.Clear();
      }
    }
    public ShiftRegister BackgroundRegister;
    public ShiftRegister PixelRegister;

    public PixelFIFO()
    {
      BackgroundRegister = new ShiftRegister();
      PixelRegister = new ShiftRegister();
    }

    public bool PushPixelsBackground(byte TileLow, byte TileHigh, byte palette, bool BGWindowEnable)
    {
      if (BackgroundRegister.AvailableSpace >= 8)
      {
        for (int i = 7; i >= 0; i--)
        {
          byte color;
          if (BGWindowEnable)
          {
            color = (byte)((((TileHigh >> i) & 1) << 1) | ((TileLow >> i) & 1));
          }
          else
          {
            color = 0x0;
          }
          BackgroundRegister.Push(color, palette, 0x0);
        }
        return true;
      }
      return false;
    }

    public bool PushPixelsSprite(byte TileLow, byte TileHigh, byte palette, byte priority, bool XFlip)
    {
      if (PixelRegister.AvailableSpace >= 8)
      {
        for (int i = 7; i >= 0; i--)
        {
          int index = XFlip ? (byte)(7 - i) : i;
          byte color = (byte)((((TileHigh >> index) & 1) << 1) | ((TileLow >> index) & 1));
          PixelRegister.Push(color, palette, priority);
        }
        return true;
      }
      return false;
    }

    public (Pixel, bool) GetPixelSprite()
    {
      bool pixelexists = PixelRegister.Pop();
      return (PixelRegister.pixel, pixelexists);
    }

    public (Pixel, bool) GetPixelBackground()
    {
      bool pixelexists = BackgroundRegister.Pop();
      return (BackgroundRegister.pixel, pixelexists);
    }

    public void Reset()
    {
      BackgroundRegister.Reset();
      PixelRegister.Reset();
    }
  }
  struct Sprite
  {
    public byte x;
    public byte y;
    public byte index;
    public byte attributes;
    public bool tallSprite = false;
    public Sprite(byte x_pos, byte y_pos, byte tile_index, byte attributes_flags)
    {
      x = x_pos;
      y = y_pos;
      index = tile_index;
      attributes = attributes_flags;
    }

    public bool CheckVerticalBounds(byte LY, bool TallSprite)
    {
      //The top needs to be subtracted by 16 because tiles can be out of frame by upto 16 pixels (Used for animations? Of objects moving into frame from above?)
      int top = y - 16; // y is a byte, promote to int
      int height = TallSprite ? 16 : 8;
      int bottom = top + height;

      return (LY >= top && LY < bottom);
    }

    public bool CheckHorizontalBounds(byte LX)
    {
      return x <= (LX + 8);
    }
  }
  int MODE = 2;
  public int machine_cycle_count = 0;
  int total_cycles = 0;
  int sprite_index = 0;
  List<Sprite> SpriteBuffer = new List<Sprite>();
  Sprite SpriteToProcess = default;
  bool ProcessingSprite = false;
  bool SpriteProcessedThisLX = false;
  BackgroundPixelFetcher bgPixelFetcher;
  SpritePixelFetcher sPixelFetcher;
  MEMORY mem;
  PixelFIFO fifo;
  LCD lcd;
  byte IF
  {
    get => mem.Read(0xFF0F);
    set => mem.Write(0xFF0F, value);
  }
  // LCDC properties
  bool DisplayEnable        => GetBitFromByte(7, LCDC); // LCDC.7
  bool WindowTileMapSelect  => GetBitFromByte(6, LCDC); // LCDC.6
  bool WindowDisplayEnable  => GetBitFromByte(5, LCDC); // LCDC.5
  bool TileDataSelect       => GetBitFromByte(4, LCDC); // LCDC.4
  bool BGTileMapSelect      => GetBitFromByte(3, LCDC); // LCDC.3
  bool SpriteSize           => GetBitFromByte(2, LCDC); // LCDC.2
  bool SpriteEnable         => GetBitFromByte(1, LCDC); // LCDC.1
  bool BGWindowEnable       => GetBitFromByte(0, LCDC); // LCDC.0
  byte LCDC
  {
      get => mem.PPURead(0xFF40);
      set => mem.PPUWrite(0xFF40, value);
  }
  byte STAT
  {
      get => mem.PPURead(0xFF41);
      set => mem.PPUWrite(0xFF41, value);
  }
  byte SCY
  {
      get => mem.PPURead(0xFF42);
      set => mem.PPUWrite(0xFF42, value);
  }
  byte SCX
  {
      get => mem.PPURead(0xFF43);
      set => mem.PPUWrite(0xFF43, value);
  }
  byte LY
  {
      get => mem.PPURead(0xFF44);
      set => mem.PPUWrite(0xFF44, value);
  }
  byte LYC
  {
      get => mem.PPURead(0xFF45);
      set => mem.PPUWrite(0xFF45, value);
  }
  byte BGP
  {
      get => mem.PPURead(0xFF47);
      set => mem.PPUWrite(0xFF47, value);
  }
  byte OBP0
  {
      get => mem.PPURead(0xFF48);
      set => mem.PPUWrite(0xFF48, value);
  }
  byte OBP1
  {
      get => mem.PPURead(0xFF49);
      set => mem.PPUWrite(0xFF49, value);
  }
  byte WY
  {
      get => mem.PPURead(0xFF4A);
      set => mem.PPUWrite(0xFF4A, value);
  }
  byte WX
  {
      get => mem.PPURead(0xFF4B);
      set => mem.PPUWrite(0xFF4B, value);
  }
  byte LX = 0;
  StreamWriter logFile;

  public PPU(MEMORY memory, LCD display, StreamWriter logger)
  {
    logFile = logger;
    mem = memory;
    lcd = display;
    fifo = new PixelFIFO();
    bgPixelFetcher = new BackgroundPixelFetcher(memory, fifo);
    sPixelFetcher = new SpritePixelFetcher(memory, fifo);
  }

  public void Machine_Cycle()
  {
    if (LY == LYC)
    {
      STAT |= 0b100;
    }
    if (!SpriteEnable)
    {
      ProcessingSprite = false;
      SpriteToProcess = default;
    }
    if (!DisplayEnable)
    {
      ResetScanLine();
      LY = 0;
      machine_cycle_count = 0;
      ChangeToMode(1);
      return;
    }
    Mode_Select();
    if (MODE == 2 && SpriteBuffer.Count < 10)
    {
      //Fetch two sprites (40 sprites total on the OAM space)
      //So thats 2 sprites per machine cycle since OAM Search lasts 20 machine cycles
      Sprite sprite1 = GetNextSprite();
      Sprite sprite2 = GetNextSprite();

      if (sprite1.CheckVerticalBounds(LY, SpriteSize) && sprite1.x > 0) SpriteBuffer.Add(sprite1);
      if (sprite2.CheckVerticalBounds(LY, SpriteSize) && SpriteBuffer.Count < 10 && sprite2.x > 0) SpriteBuffer.Add(sprite2);
    }
    else if (MODE == 3)
    {
      for (int b = 0; b < 2; b++)
      {
        //This is gonna be a long one and possibly the hardest process in the entire emulation
        //Lets try to break this into small steps that are manageable

        //First check if a sprite can be written
        for (int i = 0; (i < SpriteBuffer.Count) && SpriteEnable; i++)
        {
          Sprite sprite = SpriteBuffer[i];
          if (sprite.CheckHorizontalBounds(LX))
          {
            ProcessingSprite = true;
            SpriteToProcess = SpriteBuffer[i];
            SpriteBuffer.RemoveAt(i);
            break;
          }
        }


        //Repeat these steps twice for a full machine cycle

        //Fetch Pixels into the Sprite FIFO via a Sprite Pixel Fetcher
        if (ProcessingSprite)
        {
          SpriteProcessedThisLX = true;
          ProcessingSprite = !sPixelFetcher.Half_Machine_Cycle(SpriteToProcess, LY, OBP0, OBP1);
        }
        else
        {
          //Fetch Pixels into the Background FIFO via a Background Pixel Fetcher
          bgPixelFetcher.CheckWindowOrBackground(LY, WX, WY, WindowDisplayEnable);
          bgPixelFetcher.SelectTileAddresses(BGTileMapSelect, TileDataSelect, WindowTileMapSelect);
          bgPixelFetcher.Half_Machine_Cycle(LY, SCY, SCX, BGP, BGWindowEnable);

          //Attempt to merge and push the current FIFO values into the LCD
          //Increment LX everytime we successfully push FIFO to LCD
          (Pixel bgPixel, bool bgPixelExists) = fifo.GetPixelBackground();
          if (bgPixelExists)
          {
            (Pixel sPixel, bool sPixelExists) = fifo.GetPixelSprite();
            lcd.MergePush(bgPixel, sPixel, LX, LY);
            LX++;
            SpriteProcessedThisLX = false;
            if (LX == 160)
            {
              ChangeToMode(0);
              total_cycles += 1;
              machine_cycle_count += 1;
              return;
            }
          }

          (bgPixel, bgPixelExists) = fifo.GetPixelBackground();
          if (bgPixelExists)
          {
            (Pixel sPixel, bool sPixelExists) = fifo.GetPixelSprite();
            lcd.MergePush(bgPixel, sPixel, LX, LY);
            LX++;
            SpriteProcessedThisLX = false;
            if (LX == 160)
            {
              ChangeToMode(0);
              total_cycles += 1;
              machine_cycle_count += 1;
              return;
            }
          }
        }
      }
    }
    total_cycles += 1;
    machine_cycle_count += 1;
  }

  void Mode_Select()
  {
    if (MODE == 2)
    {

    }
    if (machine_cycle_count >= 114)
    {
      //Trigger VBlank at the end of the 143 machine cycle
      if (LY >= 143 && LY < 153)
      {
        ChangeToMode(1);
        ResetScanLine();
        return;
      }
      else if (LY >= 153)
      {
        // Console.WriteLine(total_cycles);
        ResetToMode2();
        return;
      }
      ChangeToMode(2);
      ResetScanLine();
    }
    if (machine_cycle_count < 20 && MODE != 0 && MODE != 1) ChangeToMode(2);
    else if ((LX < 160) && MODE != 0 && MODE != 1) ChangeToMode(3);
  }

  void ChangeToMode(int mode_num)
  {
    if (MODE == mode_num) { return; }
    // Console.WriteLine($"M-Cycles: {machine_cycle_count} for MODE {MODE}");
    MODE = mode_num;
    if (MODE == 2 || MODE == 3) { mem.block_oam_hard = true; }
    if (MODE == 3) { mem.block_vram_hard = true; mem.block_oam_hard = false; }
    if (MODE == 0 || MODE == 1) { mem.block_vram_hard = false; mem.block_oam_hard = false; }
    if (MODE == 1)
    {
      IF |= 0x1;
      lcd.FrameReady();
    }
    STAT = (byte)((STAT & 0b11111100) | (MODE & 0b11));

    // Check for STAT interrupt
    if ((MODE == 0 && (STAT & 0x10) > 0) ||  // Mode 0 interrupt enable
        (MODE == 1 && (STAT & 0x20) > 0) ||  // Mode 1 interrupt enable
        (MODE == 2 && (STAT & 0x40) > 0))    // Mode 2 interrupt enable
    {
        IF |= 0x2;  // Trigger LCD STAT interrupt
    }
  }

  Sprite GetNextSprite() {
    ushort offset = (ushort)(mem.oam.Start + (sprite_index * 4));
    sprite_index += 1;

    byte y = mem.PPURead(offset);
    byte x = mem.PPURead((ushort)(offset + 1));
    byte index = mem.PPURead((ushort)(offset + 2));
    byte attributes = mem.PPURead((ushort)(offset + 3));

    return new Sprite(x, y, index, attributes);
  }

  void ResetScanLine()
  {
    LX = 0;
    LY++;
    SpriteProcessedThisLX = false;
    SpriteToProcess = default;
    ProcessingSprite = false;
    machine_cycle_count = 0;
    sprite_index = 0;
    SpriteBuffer.Clear();
    fifo.Reset();
    bgPixelFetcher.Reset();
    sPixelFetcher.Reset();
  }

  void ResetToMode2()
  {
    ResetScanLine();
    LY = 0;
    ChangeToMode(2);
  }
  bool GetBitFromByte(int index, byte value) {
    return (value & (1 << index)) != 0;
  }
}