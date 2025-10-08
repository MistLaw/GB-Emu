# GB-Emu
A small Game Boy emulator I built to deepen my understanding of machine hardware and low-level programming.
<br>
<br>

## Features
- Emulates the CPU, PPU, MBC, DMA, and memory systems of the gameboy
- Supports MBC0 and MBC1 cartridges
- Key bindings configurable in code
- Passes blargg's cpu_instrs and instr_timing tests.
<br>
<br>

## Known Limitations
- Sprite rendering can sometimes distort (Future: run DMG acid tests to verify GPU behavior)
- Limited ROM support (Tetris runs as expected, other ROMs mostly show graphical bugs)
- No audio emulation yet
- Not cycle-accurate
- Does not support MBC2, MBC3, or MBC5 cartridges
<br>
<br>

## Build Instructions

### Requirements
- [.NET SDK](https://dotnet.microsoft.com/download) installed

### Build
Open a terminal in the project folder and run:

```bash
dotnet build -c Release
```
<br>
<br>

## Development Log

My goal for this project was to see how good of an emulator I could build within a week while exploring the intricacies of low-level programming and machine architecture.

### Challenges
One of the biggest challenges in emulator development is the limited testing methodologies. Unlike a regular program, which can be broken down into smaller components and easily verified, an emulator often needs to be somewhat complete before most of its functionalities can be tested. This is because the test platform is the emulator itself. As a result, careful attention to detail is critical when designing each component.

I learned this lesson the hard way, a majority of the week was spent fixing my own mistakes in the implementation of various CPU instructions.

### CPU Emulation
The core of the emulator reads data from a ROM byte by byte and decodes the opcode within each byte. Each opcode corresponds to a specific CPU instruction on the Sharp LR35902, the Game Boy's CPU. My emulator operates on the scale of an **M-Cycle**, which is equivalent to 4 **T-Cycles**, essentially the smallest unit of execution for the CPU or GPU.

### PPU and Graphics
The PPU runs in parallel with the CPU and reads from OAM and VRAM to determine what and how to draw. The process involves two fetchers, one for sprites and one for the background/window, which follow a specific set of rules to determine which tiles to draw for a given X and Y coordinate on the screen. These pixels are pushed onto a **Pixel FIFO**, which then outputs them to the LCD. While the process is complex, the general idea is that the PPU constantly processes and prepares pixels in parallel with the CPU.

### Other Components
Other components, such as the MBC, DMA, and JOYPAD, were simpler to implement compared to the CPU and PPU. However, they still play an essential role in making the emulator function as a whole.

### Testing and Debugging
Blargg's test ROMs were invaluable for debugging. When the emulator stops working, examining the trace of instructions that ran last usually reveals the problem. In particularly difficult cases, I compared traces from a known working emulator with my own. While I would have preferred to debug entirely independently, this method was much faster and allowed me to make the most of my one week timeframe.
<br>
<br>

## Future Plans

I would like to spend more time refining this project because it was very enjoyable and incredibly rewarding. Seeing the Nintendo logo appear on screen for the first time, or being greeted with the Tetris start menu, filled me with an indescribable joy and reminded me why I love programming so much.

### Planned Improvements
- **Run DMG acid tests**: This test allows you to debug why and when the PPU makes mistakes. Running it will help solve the graphical glitches most games experience.  
- **Support more MBC types and audio**: Currently only MBC0 and MBC1 are supported, and audio is not yet implemented.  
- **Optimize performance**: The emulator runs fine at 60 fps, which matches the Game Boy's speed, but slows down significantly in debug mode. Currently, opcodes are decoded during emulation. Preprocessing all opcodes into a lookup table could potentially increase speed up to 10 times.  
- **Game Boy Color support**: In the long term, I plan to transition this emulator into a Game Boy Color emulator, which is backward compatible with the original Game Boy and shares many similarities.
<br>
<br>

## References

During development, I relied on a combination of documentation and opcode references to understand the Game Boy hardware and instruction set:

- **[Game Boy Technical Reference](https://gekkio.fi/files/gb-docs/gbctr.pdf)**  
  The main document used to figure out most of the opcodes. It is somewhat incomplete and missing several opcodes, and tables for `xxx` or `xx` were never shown.  

- **[GB Ops](https://izik1.github.io/gbops/)**  
  Provides a clear breakdown of each opcode. While it does not describe flag effects or register increments, it is important to pay close attention to these details when implementing the CPU.

- **[Pan Docs](https://gbdev.io/pandocs/)**  
  Helped with general knowledge of the Game Boy’s quirks and specific hardware behaviors. This resource was especially useful for understanding memory, PPU, and peripheral interactions.

- **[Bootix DMG Boot ROM](https://github.com/Ashiepaws/Bootix)**  
  A copyright free DMG boot ROM.

- **[Blargg’s Test ROMs](https://github.com/retrio/gb-test-roms)**  
  A set of test ROMs used to verify CPU instruction execution and timing accuracy. These were essential for debugging and ensuring correct emulation.
