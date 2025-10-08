public class LR35902
{

  public class Register
  {
    public byte IR;
    public byte IE;

    public byte A;
    public byte F;

    public byte B;
    public byte C;

    public byte D;
    public byte E;

    public byte H;
    public byte L;

    public ushort AF
    {
      get => (ushort)((A << 8) | F);
      set
      {
        A = (byte)(value >> 8);
        F = (byte)(value & 0xF0); // only upper 4 bits are valid
      }
    }

    public ushort BC
    {
      get => (ushort)((B << 8) | C);
      set
      {
        B = (byte)(value >> 8);
        C = (byte)(value & 0xFF);
      }
    }

    public ushort DE
    {
      get => (ushort)((D << 8) | E);
      set
      {
        D = (byte)(value >> 8);
        E = (byte)(value & 0xFF);
      }
    }

    public ushort HL
    {
      get => (ushort)((H << 8) | L);
      set
      {
        H = (byte)(value >> 8);
        L = (byte)(value & 0xFF);
      }
    }

    public ushort PC;
    public ushort SP;

    public bool FlagZ
    {
      get => (F & 0x80) != 0;
      set => F = value ? (byte)(F | 0x80) : (byte)(F & 0x7F);
    }

    // Subtract flag (N) - bit 6
    public bool FlagN
    {
      get => (F & 0x40) != 0;
      set => F = value ? (byte)(F | 0x40) : (byte)(F & 0xBF);
    }

    // Half Carry flag (H) - bit 5
    public bool FlagH
    {
      get => (F & 0x20) != 0;
      set => F = value ? (byte)(F | 0x20) : (byte)(F & 0xDF);
    }

    // Carry flag (C) - bit 4
    public bool FlagC
    {
      get => (F & 0x10) != 0;
      set => F = value ? (byte)(F | 0x10) : (byte)(F & 0xEF);
    }

    //Reference to Mem
    MEMORY mem;
    public string mnemonic;
    public Register(MEMORY memory)
    {
      mem = memory;
      mnemonic = "LD";
    }

    // Return 8-bit register by reference based on 3-bit code
    public ref byte GetRegisterByCode(byte code)
    {
      switch (code)
      {
        case 0b000: return ref B;
        case 0b001: return ref C;
        case 0b010: return ref D;
        case 0b011: return ref E;
        case 0b100: return ref H;
        case 0b101: return ref L;
        case 0b111: return ref A;
        default:
          throw new ArgumentOutOfRangeException(nameof(code));
      }
    }

    public ushort Get16BitRegisterByCode(int code)
    {
      switch (mnemonic)
      {
        case "LD" or "ADD" or "INC" or "DEC":
          switch (code)
          {
            case 0b00: return BC;
            case 0b01: return DE;
            case 0b10: return HL;
            case 0b11: return SP;
          }
          break;
        case "PUSH" or "POP":
          switch (code)
          {
            case 0b00: return BC;
            case 0b01: return DE;
            case 0b10: return HL;
            case 0b11: return AF;
          }
          break;
      }
      throw new ArgumentOutOfRangeException(nameof(code));
    }

    public void Set16BitRegisterByCode(int code, ushort data)
    {
      switch (mnemonic)
      {
        case "LD" or "ADD" or "INC" or "DEC":
          switch (code)
          {
            case 0b00: BC = data; break;
            case 0b01: DE = data; break;
            case 0b10: HL = data; break;
            case 0b11: SP = data; break;
            default: throw new ArgumentOutOfRangeException(nameof(code));
          }
          break;
        case "PUSH" or "POP":
          switch (code)
          {
            case 0b00: BC = data; break;
            case 0b01: DE = data; break;
            case 0b10: HL = data; break;
            case 0b11: AF = data; break;
            default: throw new ArgumentOutOfRangeException(nameof(code));
          }
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(mnemonic));
      }
    }


    public byte ReadNextMem()
    {
      byte output = mem.Read(PC);
      PC += 1;
      return output;
    }

    public ushort ReadNN()
    {
      byte lower = ReadNextMem();
      byte upper = ReadNextMem();
      return (ushort)((upper << 8) | lower);
    }

    public void WriteNN(byte data)
    {
      byte lower = ReadNextMem();
      byte upper = ReadNextMem();
      mem.Write((ushort)((upper << 8) | lower), data);
    }

    // public void WriteNN16bit(ushort data)
    // {
    //   byte upper = (byte)(data >> 8);   // shift right 8 bits, then cast to byte
    //   byte lower = (byte)(data & 0xFF);
    //   ushort nn = ReadNN()
    // }
  }

  public Register regs;
  //CPU Flags
  bool IME = false; // Interrupt Master Enable
  bool IME_Schedule = false;
  bool HALT = false;
  int total_cycles = 0;
  bool TIMA_Schedule = false;
  bool[] PCsPrinted = new bool[0x10000];
  ushort FullDiv = 0;
  byte DIV
  {
    get => mem.Read(0xFF04);
    set => mem.Write(0xFF04, value);
  }
  byte TIMA
  {
    get => mem.Read(0xFF05);
    set => mem.Write(0xFF05, value);
  }
  byte TMA
  {
    get => mem.Read(0xFF06);
    set => mem.Write(0xFF06, value);
  }
  byte TAC
  {
    get => mem.Read(0xFF07);
    set => mem.Write(0xFF07, value);
  }
  bool TimerEnabled
  {
    get => (TAC & 0b100) != 0;
  }
  byte TimerEnabledBit
  {
    get => (byte)((TAC >> 2) & 1);
  }
  byte TimerMode
  {
    get => (byte)(TAC & 0b11);
  }
  string instruction = "LD";
  int remainingCycles = 1; //How many cycles the CPU will be busy for

  //Important memory addresses
  byte IE
  {
    get => mem.Read(0xFFFF);
    set => mem.Write(0xFFFF, value);
  }
  byte IF
  {
    get => mem.Read(0xFF0F);
    set => mem.Write(0xFF0F, value);
  }

  //Used to store the codes in the mask
  byte RegCodeX = 0;
  byte RegCodeY = 0;
  bool ExtendedInstructions = false;
  int Conditional = 0;
  bool StartLogging = false;

  byte R1
  {
    get { return regs.GetRegisterByCode(RegCodeX); }
    set
    {
      ref byte reg = ref regs.GetRegisterByCode(RegCodeX);
      reg = value;
    }
  }

  ushort RR
  {
    get { return regs.Get16BitRegisterByCode(RegCodeX); }
    set
    {
      regs.Set16BitRegisterByCode(RegCodeX, value);
    }
  }

  byte R2
  {
    get { return regs.GetRegisterByCode(RegCodeY); }
    set
    {
      ref byte reg = ref regs.GetRegisterByCode(RegCodeY);
      reg = value;
    }
  }

  MEMORY mem;

  List<(int, string, Action, string, bool)> opcodeHandlers;
  List<(int, string, Action, string, bool)> opcodeHandlersExtended;
  StreamWriter logFile;
  PPU ppu;

  public LR35902(MEMORY memory, StreamWriter logger, PPU pictureprocessingunit)
  {
    logFile = logger;
    ppu = pictureprocessingunit;
    mem = memory;
    regs = new Register(memory);
    regs.PC = 0;

    opcodeHandlers = new List<(int, string, Action execute, string mnemonic, bool allRegCodesAllowed)>()
    {
      (0,"0b11001011", () => { ExtendedInstructions = true; regs.IR = regs.ReadNextMem(); }, "EXTENDED INSTRUCTIONS", false), //EXTENDED INSTRUCTION

      (1,"0b01xxxyyy", () => { R1 = R2; }, "LD", false), //LD r, r'
      (2,"0b00xxx110", () => { R1 = regs.ReadNextMem(); }, "LD", false), //LD r, n
      (2,"0b01xxx110", () => { R1 = mem.Read(regs.HL); }, "LD", false), //LD r, (HL)
      (2,"0b01110xxx", () => { mem.Write(regs.HL, R1); }, "LD", false), //LD (HL), r
      (3,"0b00110110", () => { mem.Write(regs.HL, regs.ReadNextMem()); }, "LD", false), //LD (HL), n
      (2,"0b00001010", () => { regs.A = mem.Read(regs.BC); }, "LD", false), //LD A, (BC)
      (2,"0b00011010", () => { regs.A = mem.Read(regs.DE); }, "LD", false), //LD A, (DE)
      (2,"0b00000010", () => { mem.Write(regs.BC, regs.A); }, "LD", false), //LD (BC), A
      (2,"0b00010010", () => { mem.Write(regs.DE, regs.A); }, "LD", false), //LD (DE), A
      (4,"0b11111010", () => { regs.A = mem.Read(regs.ReadNN()); }, "LD", false), //LD A, (nn)
      (4,"0b11101010", () => { regs.WriteNN(regs.A); }, "LD", false), //LD (nn), A
      (2,"0b11110010", () => { regs.A = mem.ReadIndirect(regs.C); }, "LDH", false), //LDH A, (C)
      (2,"0b11100010", () => { mem.WriteIndirect(regs.C, regs.A); }, "LDH", false), //LDH (C), A
      (3,"0b11110000", () => { regs.A = mem.ReadIndirect(regs.ReadNextMem()); }, "LDH", false), //LDH A, (n)
      (3,"0b11100000", () => { mem.WriteIndirect(regs.ReadNextMem(), regs.A); }, "LDH", false), //LDH (n), A
      (2,"0b00111010", () => { regs.A = mem.Read(regs.HL); regs.HL -= 1; }, "LD", false), //LD A, (HL-)
      (2,"0b00110010", () => { mem.Write(regs.HL, regs.A); regs.HL -= 1; }, "LD", false), //LD (HL-), A
      (2,"0b00101010", () => { regs.A = mem.Read(regs.HL); regs.HL += 1; }, "LD", false), //LD A, (HL+)
      (2,"0b00100010", () => { mem.Write(regs.HL, regs.A); regs.HL += 1; }, "LD", false), //LD (HL+), A

      //16 bit load instructions
      (3,"0b00xx0001", () => { RR = regs.ReadNN(); }, "LD", false), //LD rr, nn
      (5,"0b00001000", () => {
        ushort nn = regs.ReadNN();
        mem.Write(nn, regs.SP.Low());
        nn += 1;
        mem.Write(nn, regs.SP.High());
       }, "LD", false), //LD (nn), SP
      (2,"0b11111001", () => { regs.SP = regs.HL; }, "LD", false), //LD SP, HL
      (4,"0b11xx0101", () => {
        regs.SP -= 1;
        mem.Write(regs.SP, RR.High());
        regs.SP -= 1;
        mem.Write(regs.SP, RR.Low());
      }, "PUSH", false), //PUSH rr
      (3,"0b11xx0001", () => {
        byte low = mem.Read(regs.SP);
        regs.SP += 1;
        byte high = mem.Read(regs.SP);
        regs.SP += 1;
        RR = (ushort)((high << 8) | low);
      }, "POP", false), //POP rr
      (3,"0b11111000", () => { regs.HL = Add16bit8bitS(regs.SP, (sbyte)regs.ReadNextMem()); }, "LD", false), //LD HL, SP+e

      //8bit Arithmetic and Logic
      (1,"0b10000xxx", () => { regs.A = Add8bit(regs.A, R1, false); }, "ADD", false), //ADD r
      (2,"0b10000110", () => { regs.A = Add8bit(regs.A, mem.Read(regs.HL), false); }, "ADD", false), //ADD (HL)
      (2,"0b11000110", () => { regs.A = Add8bit(regs.A, regs.ReadNextMem(), false); }, "ADD", false), //ADD n
      (1,"0b10001xxx", () => { regs.A = Add8bit(regs.A, R1, regs.FlagC); }, "ADC", false), //ADC r
      (2,"0b10001110", () => { regs.A = Add8bit(regs.A, mem.Read(regs.HL), regs.FlagC); }, "ADC", false), //ADC (HL)
      (2,"0b11001110", () => { regs.A = Add8bit(regs.A, regs.ReadNextMem(), regs.FlagC); }, "ADC", false), //ADC n

      (1,"0b10010xxx", () => { regs.A = Sub8bit(regs.A, R1, false); }, "SUB", false), //SUB r
      (2,"0b10010110", () => { regs.A = Sub8bit(regs.A, mem.Read(regs.HL), false); }, "SUB", false), //SUB (HL)
      (2,"0b11010110", () => { regs.A = Sub8bit(regs.A, regs.ReadNextMem(), false); }, "SUB", false), //SUB n
      (1,"0b10011xxx", () => { regs.A = Sub8bit(regs.A, R1, regs.FlagC); }, "SBC", false), //SBC r
      (2,"0b10011110", () => { regs.A = Sub8bit(regs.A, mem.Read(regs.HL), regs.FlagC); }, "SBC", false), //SBC (HL)
      (2,"0b11011110", () => { regs.A = Sub8bit(regs.A, regs.ReadNextMem(), regs.FlagC); }, "SBC", false), //SBC n

      (1,"0b10111xxx", () => { Sub8bit(regs.A, R1, false); }, "CP", false), //CP r
      (2,"0b10111110", () => { Sub8bit(regs.A, mem.Read(regs.HL), false); }, "CP", false), //CP (HL)
      (2,"0b11111110", () => { Sub8bit(regs.A, regs.ReadNextMem(), false); }, "CP", false), //CP n

      (1,"0b00xxx100", () => { R1 = Increment(R1); }, "INC", false), //INC r
      (3,"0b00110100", () => { mem.Write(regs.HL, Increment(mem.Read(regs.HL))); }, "INC", false), //INC (HL)
      (1,"0b00xxx101", () => { R1 = Decrement(R1); }, "DEC", false), //DEC r
      (3,"0b00110101", () => { mem.Write(regs.HL, Decrement(mem.Read(regs.HL))); }, "DEC", false), //DEC (HL)
      
      (1,"0b10100xxx", () => { regs.A = AND(regs.A, R1); }, "AND", false), //AND r
      (2,"0b10100110", () => { regs.A = AND(regs.A, mem.Read(regs.HL)); }, "AND", false), //AND (HL)
      (2,"0b11100110", () => { regs.A = AND(regs.A, regs.ReadNextMem()); }, "AND", false), //AND n
      
      (1,"0b10110xxx", () => { regs.A = OR(regs.A, R1); }, "OR", false), //OR r
      (2,"0b10110110", () => { regs.A = OR(regs.A, mem.Read(regs.HL)); }, "OR", false), //OR (HL)
      (2,"0b11110110", () => { regs.A = OR(regs.A, regs.ReadNextMem()); }, "OR", false), //OR n
      
      (1,"0b10101xxx", () => { regs.A = XOR(regs.A, R1); }, "XOR", false), //XOR r
      (2,"0b10101110", () => { regs.A = XOR(regs.A, mem.Read(regs.HL)); }, "XOR", false), //XOR (HL)
      (2,"0b11101110", () => { regs.A = XOR(regs.A, regs.ReadNextMem()); }, "XOR", false), //XOR n

      (1,"0b00111111", () => { regs.FlagC = !regs.FlagC; regs.FlagH = false; regs.FlagN = false; }, "CCF", false), //CCF
      (1,"0b00110111", () => { regs.FlagC = true; regs.FlagH = false; regs.FlagN = false; }, "SCF", false), //SCF
      (1,"0b00100111", () => { regs.A = DAA(regs.A); }, "DAA", false), //DAA
      (1,"0b00101111", () => { regs.A = (byte)~regs.A; regs.FlagN = true; regs.FlagH = true; }, "CPL", false), //CPL

      //16bit Arithmetic
      (2,"0b00xx0011", () => { RR = (ushort)(RR + 1); }, "INC", false), //INC rr
      (2,"0b00xx1011", () => { RR = (ushort)(RR - 1); }, "DEC", false), //DEC rr
      (2,"0b00xx1001", () => { regs.HL = Add16bit(regs.HL, RR); }, "ADD", false), //ADD HL rr
      (4,"0b11101000", () => { regs.SP = Add16bit8bitS(regs.SP, (sbyte)regs.ReadNextMem()); }, "ADD", false), //ADD SP e

      //Bit Operations
      (1,"0b00000111", () => { regs.A = RotateLeft(regs.A, true); regs.FlagZ = false; }, "RLCA", false), //RLCA
      (1,"0b00001111", () => { regs.A = RotateRight(regs.A, true); regs.FlagZ = false; }, "RRCA", false), //RRCA
      (1,"0b00010111", () => { regs.A = RotateLeft(regs.A, false); regs.FlagZ = false; }, "RLA", false), //RLA
      (1,"0b00011111", () => { regs.A = RotateRight(regs.A, false); regs.FlagZ = false; }, "RRA", false), //RRA

      //Control flow
      (4,"0b11000011", () => { regs.PC = regs.ReadNN(); }, "JP", false), //JP nn
      (1,"0b11101001", () => { regs.PC = regs.HL; }, "JP", false), //JP HL
      (3,"0b110xx010", () => {
        ushort nn = regs.ReadNN();
        if (ConditionFromCode((byte)RegCodeX)){
          regs.PC = nn;
          Conditional = 1;
        }
      }, "JP", false), //JP cc nn
      (3,"0b00011000", () => {
        sbyte e = (sbyte)regs.ReadNextMem();
        regs.PC = Add16bit8bitSNoFlag(regs.PC, e );
        }, "JR", false), //JR e
      (2,"0b001xx000", () => {
        sbyte e = (sbyte)regs.ReadNextMem();
        if (ConditionFromCode((byte)RegCodeX)){
          regs.PC = Add16bit8bitSNoFlag(regs.PC, e);
          Conditional = 1;
        }
      }, "JR", false), //JR cc e

      (6,"0b11001101", () => {
        ushort nn = regs.ReadNN();
        regs.SP -= 1;
        mem.Write(regs.SP, regs.PC.High());
        regs.SP -= 1;
        mem.Write(regs.SP, regs.PC.Low());
        regs.PC = nn;
      }, "CALL", false), //CALL nn
      (3,"0b110xx100", () => {
        ushort nn = regs.ReadNN();
        if(ConditionFromCode((byte)RegCodeX)){
          regs.SP -= 1;
          mem.Write(regs.SP, regs.PC.High());
          regs.SP -= 1;
          mem.Write(regs.SP, regs.PC.Low());
          regs.PC = nn;
          Conditional = 3;
        }
      }, "CALL", false), //CALL cc nn

      (4,"0b11001001", () => {
        byte low = mem.Read(regs.SP);
        regs.SP += 1;
        byte high = mem.Read(regs.SP);
        regs.SP += 1;
        regs.PC = (ushort)((high << 8) | low);
      }, "RET", false), //RET nn
      (2,"0b110xx000", () => {
        if(ConditionFromCode((byte)RegCodeX)){
          byte low = mem.Read(regs.SP);
          regs.SP += 1;
          byte high = mem.Read(regs.SP);
          regs.SP += 1;
          regs.PC = (ushort)((high << 8) | low);
          Conditional = 3;
        }
      }, "RET", false), //RET cc nn
      (4,"0b11011001", () => {
        byte low = mem.Read(regs.SP);
        regs.SP += 1;
        byte high = mem.Read(regs.SP);
        regs.SP += 1;
        regs.PC = (ushort)((high << 8) | low);
        IME = true;
      }, "RETI", false), //RETI nn

      
      (4,"0b11xxx111", () => {
        regs.SP -= 1;
        mem.Write(regs.SP, regs.PC.High());
        regs.SP -= 1;
        mem.Write(regs.SP, regs.PC.Low());
        regs.PC = (ushort)(RegCodeX << 3);
      }, "RST", true), //RST n
      (1,"0b01110110", () => { HALT = true; }, "HALT", false), //HALT
      (2,"0b00010000", () => { HALT = false; regs.ReadNextMem(); }, "STOP", false), //STOP //Implement a better STOP later
      (1,"0b11110011", () => { IME = false; }, "DI", false), //DI
      (1,"0b11111011", () => { IME_Schedule = true; }, "EI", false), //EI
      (1,"0b00000000", () => {  }, "NOP", false), //NOP
    };

    opcodeHandlersExtended = new List<(int, string, Action execute, string mnemonic, bool)>()
    {
      (2,"0b00000xxx", () => { R1 = RotateLeft(R1, true); regs.FlagZ = R1 == 0; }, "RLC", false), //RLC r
      (4,"0b00000110", () => {
        byte result = RotateLeft(mem.Read(regs.HL), true);
        mem.Write(regs.HL, result);
        regs.FlagZ = result == 0;
      }, "RLC", false), //RLC (HL)
      (2,"0b00001xxx", () => { R1 = RotateRight(R1, true); regs.FlagZ = R1 == 0; }, "RRC", false), //RRC r
      (4,"0b00001110", () => {
        byte result = RotateRight(mem.Read(regs.HL), true);
        mem.Write(regs.HL, result);
        regs.FlagZ = result == 0;
      }, "RRC", false), //RRC (HL)
      (2,"0b00010xxx", () => { R1 = RotateLeft(R1, false); regs.FlagZ = R1 == 0; }, "RL", false), //RL r
      (4,"0b00010110", () => {
        byte result = RotateLeft(mem.Read(regs.HL), false);
        mem.Write(regs.HL, result);
        regs.FlagZ = result == 0;
      }, "RL", false), //RL (HL)
      (2,"0b00011xxx", () => { R1 = RotateRight(R1, false); regs.FlagZ = R1 == 0; }, "RR", false), //RR r
      (4,"0b00011110", () => {
        byte result = RotateRight(mem.Read(regs.HL), false);
        mem.Write(regs.HL, result);
        regs.FlagZ = result == 0;
      }, "RR", false), //RR (HL)
      (2,"0b00100xxx", () => { R1 = ShiftLeft(R1); }, "SLA", false), //SLA r
      (4,"0b00100110", () => {
        byte result = ShiftLeft(mem.Read(regs.HL));
        mem.Write(regs.HL, result);
      }, "SLA", false), //SLA (HL)
      (2,"0b00101xxx", () => { R1 = ShiftRight(R1); }, "SRA", false), //SRA r
      (4,"0b00101110", () => {
        byte result = ShiftRight(mem.Read(regs.HL));
        mem.Write(regs.HL, result);
      }, "SRA", false), //SRA (HL)
      (2,"0b00110xxx", () => { R1 = SWAP(R1); }, "SWAP", false), //SWAP r
      (4,"0b00110110", () => {
        byte result = SWAP(mem.Read(regs.HL));
        mem.Write(regs.HL, result);
      }, "SWAP", false), //SWAP (HL)
      (2,"0b00111xxx", () => { R1 = ShiftRight(R1, false); }, "SRL", false), //SRL r
      (4,"0b00111110", () => {
        byte result = ShiftRight(mem.Read(regs.HL), false);
        mem.Write(regs.HL, result);
      }, "SRL", false), //SRL (HL)

      (2,"0b01xxxyyy", () => { BIT((byte)RegCodeX, R2); }, "BIT", true), //BIT b, r
      (3,"0b01xxx110", () => { BIT((byte)RegCodeX, mem.Read(regs.HL)); }, "BIT", true), //BIT b, r
      (2,"0b10xxxyyy", () => { R2 = ResetBit((byte)RegCodeX, R2); }, "RES", true), //RES b, r
      (4,"0b10xxx110", () => {
        byte result = ResetBit((byte)RegCodeX, mem.Read(regs.HL));
        mem.Write(regs.HL, result);
       }, "RES", true), //RES b, r
      (2,"0b11xxxyyy", () => { R2 = SetBit((byte)RegCodeX, R2); }, "SET", true), //SET b, r
      (4,"0b11xxx110", () => {
        byte result = SetBit((byte)RegCodeX, mem.Read(regs.HL));
        mem.Write(regs.HL, result);
       }, "SET", true), //SET b, r
    };
  }

  //When skipping bootrom used to initialize everything.
  public void InitDMGAndLoadRom(byte[] DMG, byte[] ROM)
  {
    mem.LoadToMBC(ROM);
    mem.LoadBootROM(DMG);
  }

  public void DumpVRAM()
  {
    string logFile = "vram_dump.txt";

    using (StreamWriter writer = new StreamWriter(logFile))
    {
      for (ushort addr = 0x8000; addr <= 0x9FFF; addr++)
      {
        byte value = mem.Read(addr);

        // Print to console
        // Console.Write($"{value:X2} ");

        // Write to file
        writer.Write($"{value:X2} ");

        // Newline every 16 bytes for readability
        if ((addr - 0x8000 + 1) % 16 == 0)
        {
          // Console.WriteLine();
          writer.WriteLine();
        }
      }
    }

    Console.WriteLine($"VRAM dumped to {logFile}");
  }

  byte GetDivEdge()
  {
    switch (TimerMode)
    {
      case 0b00: { return (byte)((FullDiv >> 9) & TimerEnabledBit); }
      case 0b01: { return (byte)((FullDiv >> 3) & TimerEnabledBit); }
      case 0b10: { return (byte)((FullDiv >> 5) & TimerEnabledBit); }
      case 0b11: { return (byte)((FullDiv >> 7) & TimerEnabledBit); }
    }
    return 0;
  }
  public void Machine_Cycle()
  {
    total_cycles += 1;
    Conditional = 0;
    if (regs.PC == 0x100)
    {
      Console.WriteLine(regs.A);
      StartLogging = true;
    }
    ushort PrevPC = regs.PC;

    //Handle Timer Code
    //Get the edge first and then increment Div
    byte DivEdge = GetDivEdge();
    FullDiv += 4;
    DIV = (byte)(FullDiv >> 8);
    //Detect if the edge was falling during the increment
    bool FallingEdge = (DivEdge == 1) && (DivEdge != GetDivEdge());
    if (TIMA_Schedule)
    {
      if (TIMA == 0x0)
      {
        TIMA = TMA;
        IF |= 0x04;
      }
      TIMA_Schedule = false;
    }
    if (FallingEdge)
    {
      TIMA += 1;
      if (TIMA == 0x0)
      {
        TIMA_Schedule = true;
      }
    }


    //Check if the CPU is busy
    remainingCycles -= 1;
    if (remainingCycles > 0)
    {
      return;
    }
    //Check if there are any interrupts scheduled
    //IE is a list of bits (1 byte) which acts as a mask for which types of interrupts its willing to process
    //IF is a list of bits (1 byte) of which interrupts are queued
    bool pending = (IE & IF) != 0;
    //If any of the interrupts are set then we should process or reset halt
    if (pending)
    {
      //IME is master flag for if we should process interrupts
      if (IME)
      {
        InterruptHandler((byte)(IE & IF));
      }
      //Reset halt regardless of IME
      HALT = false;
    }

    //If halted we should return and not process the op codes
    if (HALT)
    {
      return;
    }

    //Fetch the next instruction from memory
    byte opcode = regs.ReadNextMem();

    //Execute the instruction
    Execute_OPCODE(opcode);

    //If an IME is scheduled from previous instruction (not this cycle) set it now so next cycle can use it
    if (IME_Schedule && instruction != "EI")
    {
      IME = true;
      IME_Schedule = false;
    }
    // if ((!PCsPrinted[regs.PC] && (regs.PC >= 0x101)) && (regs.PC == 0x745))
    if (StartLogging)
    {
      // logFile.WriteLine(
      //   $"A:{regs.A:X2} F:" +
      //   $"{(regs.FlagZ ? "Z" : "-")}" +
      //   $"{(regs.FlagN ? "N" : "-")}" +
      //   $"{(regs.FlagH ? "H" : "-")}" +
      //   $"{(regs.FlagC ? "C" : "-")} " +
      //   $"BC:{regs.BC:X4} " +
      //   $"DE:{regs.DE:X4} " +
      //   $"HL:{regs.HL:X4} " +
      //   $"SP:{regs.SP:X4} " +
      //   $"PC:{regs.PC:X4} " +
      //   $"(cy: {((total_cycles)*4)}) " +
      //   $"{instruction} OP: {opcode}"
    // );
        
      // logFile.WriteLine($"PPU M-Cycles {ppu.machine_cycle_count}");
      // mem.LoggedLines += 1;
      PCsPrinted[regs.PC] = true;
      mem.LoggedLines += 1;
    }
    if ((mem.LoggedLines % 10 == 0 && mem.LoggedLines > 0 ))
    {
      // Console.WriteLine("File saved!");
      logFile.Flush();
    }
  }

  public void Execute_OPCODE(byte opcode)
  {
    //Set the IR to the opcode to apply mask
    regs.IR = opcode;
    ExtendedInstructions = false;
    //Add an condtional cycles
    Conditional = 0;

    //Logic for what each op code does
    //Checks the conditions in the opcodeHandlers and executes the correct command
    foreach (var (duration, mask, execute, mnemonic, allRegCodesAllowed) in opcodeHandlers)
    {
      if (OPCODE_Mask(mask, mnemonic, allRegCodesAllowed))
      {
        instruction = mnemonic;
        execute();
        remainingCycles = duration + Conditional;
        break;
      }
    }

    if (ExtendedInstructions)
    {
      foreach (var (duration, mask, execute, mnemonic, allRegCodesAllowed) in opcodeHandlersExtended)
      {
        if (OPCODE_Mask(mask, mnemonic, allRegCodesAllowed))
        {
          instruction = mnemonic;
          execute();
          remainingCycles = duration + Conditional;
          break;
        }
      }
    }
  }

  void InterruptHandler(byte interrupt)
  {
    //List of intterupt addresses (Addresses you jump to when a specific interrupt happens)
    byte[] interruptAddr = new byte[] { 0x40, 0x48, 0x50, 0x58, 0x60 };
    //Sanity check to make sure we only fetch the bottom 5 bits or if interrupt is 0
    interrupt = (byte)(interrupt & 0x1F);
    if (interrupt == 0) return;

    //Fetch the "lowest" (bit 0 being the lowerst) non zero bit
    int bitindex = 0;
    while ((interrupt & 0x1) != 1)
    {
      bitindex += 1;
      interrupt >>= 1;
    }

    //Clear IME and disable the bit (Mark the interrupt as complete)
    IME = false;
    IF &= (byte)((1 << bitindex) ^ 0xFF);

    //Push the curent PC to stack
    regs.SP -= 1;
    mem.Write(regs.SP, regs.PC.High());
    regs.SP -= 1;
    mem.Write(regs.SP, regs.PC.Low());

    //Load the interrupt function
    regs.PC = interruptAddr[bitindex];
  }

  ushort Add16bit8bitS(ushort lh, sbyte rh)
  {
    byte lowlh = (byte)(lh & 0xFF);
    byte rhByte = (byte)rh; // interpret bits for half-carry/carry calculation

    ushort result = (ushort)(lh + rh); // signed addition
    //Half carry and carry bits calculated unsigned (Hardware accurate)
    bool halfCarry = ((lowlh & 0xF) + (rhByte & 0xF)) > 0xF;
    bool carry = (lowlh + rhByte) > 0xFF;


    regs.FlagZ = false;
    regs.FlagN = false;
    regs.FlagH = halfCarry;
    regs.FlagC = carry;

    return result;
  }

  ushort Add16bit8bitSNoFlag(ushort lh, sbyte rh)
  {
    ushort result = (ushort)(lh + rh); // signed addition

    return result;
  }


  byte Add8bit(byte lh, byte rh, bool c)
  {
    byte carry_bit = c ? (byte)1 : (byte)0;
    byte result = (byte)(lh + rh + carry_bit);
    bool halfCarry = ((rh & 0xF) + (lh & 0xF) + carry_bit) > 0xF;
    bool carry = (rh + lh + carry_bit) > 0xFF;


    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = halfCarry;
    regs.FlagC = carry;

    return result;
  }

  ushort Add16bit(ushort lh, ushort rh)
  {
    ushort result = (ushort)(lh + rh);
    bool halfCarry = ((lh & 0x0FFF) + (rh & 0x0FFF)) > 0x0FFF; 
    bool carry = (rh + lh) > 0xFFFF;

    regs.FlagN = false;
    regs.FlagH = halfCarry;
    regs.FlagC = carry;

    return result;
  }

  byte Increment(byte lh)
  {
    byte result = (byte)(lh + 1);
    bool halfCarry = ((1 & 0xF) + (lh & 0xF)) > 0xF;

    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = halfCarry;

    return result;
  }

  byte Decrement(byte lh)
  {
    byte result = (byte)(lh - 1);
    bool halfCarry = ((1 & 0xF) > (lh & 0xF));

    regs.FlagZ = result == 0;
    regs.FlagN = true;
    regs.FlagH = halfCarry;

    return result;
  }

  byte Sub8bit(byte lh, byte rh, bool c)
  {
    byte carry_bit = c ? (byte)1 : (byte)0;
    byte result = (byte)(lh - rh - carry_bit);
    bool halfCarry = ((lh & 0xF) < ((rh & 0xF) + carry_bit));
    bool carry = (lh < (rh + carry_bit));

    regs.FlagZ = result == 0;
    regs.FlagN = true;
    regs.FlagH = halfCarry;
    regs.FlagC = carry;

    return result;
  }

  byte AND(byte rh, byte lh)
  {
    byte result = (byte)(rh & lh);
    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = true;
    regs.FlagC = false;
    return result;
  }

  byte OR(byte rh, byte lh)
  {
    byte result = (byte)(rh | lh);
    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = false;
    regs.FlagC = false;
    return result;
  }

  byte XOR(byte rh, byte lh)
  {
    byte result = (byte)(rh ^ lh);
    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = false;
    regs.FlagC = false;
    return result;
  }

  byte DAA(byte value)
  {
    byte offset = 0x0;

    if (!regs.FlagN) // after addition
    {
      if ((value & 0x0F) > 9 || regs.FlagH)
        offset |= 0x06;
      if (value > 0x99 || regs.FlagC)
        offset |= 0x60;

      int result = value + offset;
      regs.FlagC = regs.FlagC || (result > 0xFF); // update carry only here
      value = (byte)result;
    }
    else // after subtraction
    {
      if (regs.FlagH) offset |= 0x06;
      if (regs.FlagC) offset |= 0x60;

      value = (byte)(value - offset);
      // carry stays the same in subtraction mode
    }

    regs.FlagZ = (value == 0);
    regs.FlagH = false; // always cleared
    return value;
  }

  byte RotateLeft(byte value, bool circular)
  {
    byte msb = (byte)(value >> 7);
    byte result = (byte)(value << 1);
    if (circular)
    {
      result |= msb;
    }
    else
    {
      result |= (byte)(regs.FlagC ? 1 : 0);
    }

    regs.FlagC = msb == 1;
    regs.FlagN = false;
    regs.FlagH = false;

    return result;
  }

  byte RotateRight(byte value, bool circular)
  {
    byte lsb = (byte)(value & 1);
    byte result = (byte)(value >> 1);

    if (circular)
    {
      result |= (byte)(lsb << 7);
    }
    else
    {
      result |= (byte)((regs.FlagC ? 1 : 0) << 7);
    }

    regs.FlagC = lsb == 1;
    regs.FlagN = false;
    regs.FlagH = false;

    return result;
  }

  byte ShiftLeft(byte value)
  {
    byte msb = (byte)(value >> 7);
    byte result = (byte)(value << 1);

    regs.FlagZ = result == 0;
    regs.FlagC = msb == 1;
    regs.FlagN = false;
    regs.FlagH = false;

    return result;
  }

  byte ShiftRight(byte value, bool arithmetic = true)
  {
    //Should we preserve sign? Guide says no but its an arithmetic operation so...
    byte msb = (byte)(value >> 7);
    byte lsb = (byte)(value & 1);
    byte result = (byte)(value >> 1);
    if (arithmetic)
    {
      result |= (byte)(msb << 7);
    }
    else
    {
      result &= 0x7F;
    }

    regs.FlagZ = result == 0;
    regs.FlagC = lsb == 1;
    regs.FlagN = false;
    regs.FlagH = false;

    return result;
  }

  byte SWAP(byte value)
  {
    byte high = (byte)(value >> 4);
    byte low = (byte)(value & 0xF);
    byte result = (byte)(low << 4);
    result |= high;

    regs.FlagZ = result == 0;
    regs.FlagN = false;
    regs.FlagH = false;
    regs.FlagC = false;
    return result;
  }

  void BIT(byte b, byte value)
  {
    regs.FlagZ = (byte)((value >> b) & 1) == 0;
    regs.FlagN = false;
    regs.FlagH = true;
  }

  byte ResetBit(byte b, byte value)
  {
    byte mask = (byte)((1 << b) ^ 0xFF);
    return (byte)(value & mask);
  }

  byte SetBit(byte b, byte value)
  {
    byte mask = (byte)((1 << b));
    return (byte)(value | mask);
  }

  bool ConditionFromCode(byte code)
  {
    switch (code)
    {
      case 0b00:
        {
          return !regs.FlagZ;
        }
      case 0b01:
        {
          return regs.FlagZ;
        }
      case 0b10:
        {
          return !regs.FlagC;
        }
      case 0b11:
        {
          return regs.FlagC;
        }
      default:
        {
          throw new Exception("Not a valid Reg code!");
        }
    }
  }

  void ExtractRegCode(string mask, string mnemonic)
  {
    regs.mnemonic = mnemonic;
    if (mask.Length > 8)
    {
      throw new InvalidOperationException("Mask is of length greater than 8.");
    }
    RegCodeX = 0;
    RegCodeY = 0;
    byte op = regs.IR;
    if (mask.Contains('x'))
    {
      int firstX = mask.IndexOf("x");
      int lastX = mask.LastIndexOf("x");
      byte topBitsRemoved = (byte)(op << firstX);
      RegCodeX = (byte)(topBitsRemoved >> (8 - ((lastX - firstX) + 1)));
    }
    if (mask.Contains('y'))
    {
      int firstY = mask.IndexOf("y");
      int lastY = mask.LastIndexOf("y");
      byte topBitsRemoved = (byte)(op << firstY);
      RegCodeY = (byte)(topBitsRemoved >> (8 - ((lastY - firstY) + 1)));
    }
  }

  bool OPCODE_Mask(string mask, string mnemonic, bool allRegCodesAllowed)
  {
    //Removes 0b prefix
    mask = mask.Substring(2);

    //Convert mask into bit mask and modify op code
    //Example 0b01xxxyyy mask turns to 0b01111111 and op code 0b01110001 turns to 0b01111111
    byte bitmask = 0;
    byte modop = regs.IR;
    for (int i = 0; i < mask.Length; i++)
    {
      if (mask[i] != '0')
      {
        bitmask |= (byte)(1 << (mask.Length - 1 - i)); // MSB first
        if (mask[i] != '1')
        {
          modop |= (byte)(1 << (mask.Length - 1 - i));
        }
      }
    }

    if (bitmask == modop)
    {
      ExtractRegCode(mask, mnemonic);
      //Exclude 110 as its a reference to a mem address
      if ((RegCodeX == 0b110 && !allRegCodesAllowed) || RegCodeY == 0b110)
      {
        return false;
      }
      return true;
    }
    return false;
  }
}