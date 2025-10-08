using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL2;

//Important note RR can have multiple different groups depending on instruction. Make sure to handle all instructions

class Program
{
  static int width = 160;
  static int height = 144;
  static StreamWriter logFile = new StreamWriter("cpu_log.txt", append: true);
  static MBC mbc = new MBC(logFile);
  static JOYPAD joypad = new JOYPAD();
  static MEMORY mem = new MEMORY(mbc, joypad, logFile);
  static DMA dma = new DMA(mem);
  static LCD lcd = new LCD(width, height);
  static PPU ppu = new PPU(mem, lcd, logFile);
  static LR35902 cpu = new LR35902(mem, logFile, ppu);
  static void Main(string[] args)
  {
    joypad.mem = mem;
    // Initialize SDL
    if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) < 0)
    {
      Console.WriteLine("SDL could not initialize! SDL_Error: " + SDL.SDL_GetError());
      return;
    }

    // Create window
    IntPtr window = SDL.SDL_CreateWindow(
        "My Emulator",
        SDL.SDL_WINDOWPOS_CENTERED,
        SDL.SDL_WINDOWPOS_CENTERED,
        640, // width
        480, // height
        SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
    );

    if (window == IntPtr.Zero)
    {
      Console.WriteLine("Window could not be created! SDL_Error: " + SDL.SDL_GetError());
      SDL.SDL_Quit();
      return;
    }

    // Create renderer
    IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
    if (renderer == IntPtr.Zero)
    {
      Console.WriteLine("Renderer could not be created! SDL_Error: " + SDL.SDL_GetError());
      SDL.SDL_DestroyWindow(window);
      SDL.SDL_Quit();
      return;
    }

    //Create a texture to draw to
    IntPtr texture = SDL.SDL_CreateTexture(
        renderer,
        SDL.SDL_PIXELFORMAT_ARGB8888, // matches your uint color format
        (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
        width,
        height
    );

    bool running = true;
    SDL.SDL_Event e;

    //Frame counter 
    Stopwatch fpsTimer = new Stopwatch();
    Stopwatch frameTimer = new Stopwatch();
    fpsTimer.Start();
    frameTimer.Start();
    int frameCount = 0;

    //Load the ROM
    string romPath = "tetris.gb";
    byte[] rom = GBLoader.LoadROM(romPath);
    Console.WriteLine($"ROM loaded: {rom.Length} bytes");
    string bootStrapPath = "bootix_dmg.bin";
    byte[] boot = GBLoader.LoadROM(bootStrapPath);
    Console.WriteLine($"BOOTSTRAP loaded: {boot.Length} bytes");
    cpu.InitDMGAndLoadRom(boot, rom);
    bool paused = false;
    bool spaceHeld = false;
    // Main loop
    while (running)
    {

      if (paused)
      {
        IntPtr statePtr = SDL.SDL_GetKeyboardState(out int numKeys);
        if (Marshal.ReadByte(statePtr, (int)SDL.SDL_Scancode.SDL_SCANCODE_SPACE) >= 1 && !spaceHeld)
        {
          spaceHeld = true;
          paused = false;
        }
        if (Marshal.ReadByte(statePtr, (int)SDL.SDL_Scancode.SDL_SCANCODE_SPACE) == 0)
        {
          spaceHeld = false;
        }
      }

      while (!lcd.frame_ready && !paused)
      {
        cpu.Machine_Cycle();
        dma.Machine_Cycle();
        ppu.Machine_Cycle();
        IntPtr statePtr = SDL.SDL_GetKeyboardState(out int numKeys);
        if (Marshal.ReadByte(statePtr, (int)SDL.SDL_Scancode.SDL_SCANCODE_SPACE) >= 1 && !spaceHeld)
        {
          spaceHeld = true;
          paused = true;
        }
        if (Marshal.ReadByte(statePtr, (int)SDL.SDL_Scancode.SDL_SCANCODE_SPACE) == 0)
        {
          spaceHeld = false;
        }
        joypad.Machin_Cycle(statePtr);
      }

      while (SDL.SDL_PollEvent(out e) == 1)
      {
        if (e.type == SDL.SDL_EventType.SDL_QUIT)
        {
          running = false;
        }
      }

      // Clear screen (black)
      SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);

      //Load LCD image
      if (!paused)
      {        
        lcd.CopyFrame(texture);
      }

      //Copy the texture to screen
      SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
      //Tells the renderer to render
      SDL.SDL_RenderPresent(renderer);

      // FPS calculation
      frameCount++;
      if (fpsTimer.ElapsedMilliseconds >= 1000)
      {
        int fps = frameCount;
        frameCount = 0;
        fpsTimer.Restart();

        // Update window title with FPS
        SDL.SDL_SetWindowTitle(window, $"My Emulator - FPS: {fps}");
      }

      //Delay if frame is too early
      if (frameTimer.ElapsedMilliseconds < 16)
      {
        SDL.SDL_Delay((uint)(16 - frameTimer.ElapsedMilliseconds));
      }
      frameTimer.Restart();
    }

    // Cleanup
    SDL.SDL_DestroyRenderer(renderer);
    SDL.SDL_DestroyWindow(window);
    SDL.SDL_Quit();
  }
}
