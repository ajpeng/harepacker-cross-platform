
## HaSuite / Harepacker resurrected (cross-platform)
[![Github total downloads](https://img.shields.io/github/downloads/ajpeng/harepacker-cross-platform/total.svg)](https://github.com/ajpeng/harepacker-cross-platform/releases)
[![Github All Releases](https://img.shields.io/github/release/ajpeng/harepacker-cross-platform.svg)](https://github.com/ajpeng/harepacker-cross-platform/releases)
[![Github All Issues](https://img.shields.io/github/issues/ajpeng/harepacker-cross-platform.svg)](https://github.com/ajpeng/harepacker-cross-platform/issues)


A collection of tools for MapleStory, including a .wz file and level/field/map editor.
> Original thread: [HaSuite - HaCreator 2.1/HaRepacker 4.2.3](https://github.com/hadeutscher/HaSuite) | [Ragezone](http://forum.ragezone.com/f702/release-hasuite-hacreator-2-1-a-1068988/)

> Discussion thread: [Ragezone](https://forum.ragezone.com/f702/release-harepacker-resurrected-1149521/)


----
## Project contents
* HaCreator - MapleStory level editor
* HaRepacker - MapleStory .wz file editor (cross-platform port: macOS, Linux, Windows)
* HaSharedLibrary - A shared library between HaRepacker & HaCreator for mostly GUI
* spine-csharp 2.1.25 - 2D animation library [official website](https://github.com/EsotericSoftware/spine-runtimes) | [official website, spine demo](http://esotericsoftware.com/spine-demos) | [MapleStory dev's note](https://orangemushroom.net/2015/06/17/developers-note-maplestory-reboot-update-introduction-2-and-3/)
* Real-ESRGAN - For AI 2D image up-scaling. [Official website](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan) | [Official website ](https://github.com/xinntao/Real-ESRGAN)
* UnitTest_WzFile - For testing of wz file across versions.

##### MapleLib2 by haha01haha01;
- is based on MapleLib by Snow, WzLib by JonyLeeson, and information from Fiel\Koolk

  
----

## BUILD
### To build, you need
 - [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
 - [Git](https://git-scm.com/downloads) for cloning and submodule management

### To run (cross-platform — net10.0)
 - Operating system: **macOS**, **Linux**, or **Windows**
 - Processor Architecture: x64, ARM64
 - RAM: 8GB and above minimum recommended

> **Note:** The `cross-platform` branch targets `net10.0` (macOS/Linux/Windows) and uses Avalonia UI, SkiaSharp, and MonoGame.DesktopGL instead of WinForms/WPF and DirectX. HaCreator still requires Windows for the time being.

### Cloning
```
git clone https://github.com/ajpeng/harepacker-cross-platform.git
git submodule update --init --recursive
```

### Running HaRepacker (cross-platform)
```
dotnet run --project HaRepacker/Harepacker-resurrected.csproj
```

### Running tests
```
dotnet test UnitTest_WzFile/UnitTest_WzFile.csproj
```

### Modules / [Submodules](https://www.atlassian.com/git/tutorials/git-submodule) used

- [Spine-Runtime](https://github.com/EsotericSoftware/spine-runtimes)
- [MapleLib](https://github.com/lastbattle/MapleLib)
- [WzImg-MCP-Server](https://github.com/lastbattle/WzImg-MCP-Server) (Codex MCP tooling / IMG data access)

### Codex MCP (optional)
If you want to use the `wzimg` MCP server with OpenAI Codex:
- Build it (after cloning with submodules):
```
dotnet publish ./WzImg-MCP-Server/WzImgMCP/WzImgMCP.csproj -c Release -r win-x64 --self-contained false
```
- Then configure it in Codex (`codex mcp add ...`) pointing at:
`WzImg-MCP-Server/WzImgMCP/bin/Release/net10.0-windows/win-x64/publish/WzImgMCP.exe`


 ----

## Documentation

Technical documentation for HaSuite internals.

### WZ File Format
- [WZ Format Documentation](docs/wz-format/README.md) - WZ/IMG file structure, encryption, and format history
- [WzFileManager Reference](docs/wz-format/WzFileManager.md) - Central WZ file loading and management class
- [Canvas & Outlink System](docs/wz-format/canvas-outlink-system.md) - _Canvas directories and _outlink/_inlink resolution

### HaCreator & HaRepacker Architecture
- [Architecture Overview](docs/hacreator-harepacker-architecture/README.md) - Data source abstraction and component architecture
- [IMG Filesystem Migration](docs/hacreator-harepacker-architecture/IMG_FILESYSTEM_MIGRATION_PLAN.md) - IMG filesystem migration and design
- [IMG Hot Swap](docs/hacreator-harepacker-architecture/img-hot-swap.md) - Hot-swapping IMG files during development

### Map Simulator
- [Damage Number Analysis](docs/mapsimulator/damage_number_analysis.md) - Analysis of damage number rendering

### Architecture & Design
- [AI Map Edit Window Redesign](docs/architecture/AIMapEditWindow-Chat-Redesign-Plan.md) - Chat interface redesign plan

----


## Development

  

Please note that this is a community-driven project. Don't expect any issues to be fixed or new features to be added quickly.

Want to support the development?  **BTC**: [3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx](https://blockstream.info/address/3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx)

**BTC**: [3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx](https://blockstream.info/address/3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx)

  

----

![Harepacker](https://user-images.githubusercontent.com/4586194/109911770-a7d45e80-7ce5-11eb-9843-e4414bb6016f.png)

![Image preview](https://user-images.githubusercontent.com/4586194/109911721-85dadc00-7ce5-11eb-9111-4e2bfdbf5551.png)

![Spine image preview](https://user-images.githubusercontent.com/4586194/109911553-43b19a80-7ce5-11eb-8495-206a9c79d76f.png)

![Limen: Where the world ends](https://user-images.githubusercontent.com/4586194/208673934-e4300f74-8b6f-4866-a778-f7e675355ced.png)

![Rainbow Street Amherst](https://user-images.githubusercontent.com/4586194/208673762-4207a6c5-0f04-42a1-8f32-f6cd39598409.jpg)


![Oblivion Lake](https://user-images.githubusercontent.com/4586194/208673402-8c28c9f4-72da-4c8b-a818-43ed053cf126.png)

 

----

## License

MIT

```
Copyright (c) 2026~2026, ajpeng https://github.com/ajpeng
Copyright (c) 2018~2024, LastBattle https://github.com/lastbattle
Copyright (c) 2010~2013, haha01haha http://forum.ragezone.com/f701/release-universal-harepacker-version-892005/
 
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
