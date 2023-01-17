using Cosmos.HAL;
using IL2CPU.API.Attribs;

namespace Cosmos.System;

public static class Boot
{
    [BootEntry(40)]
    private static void Init()
    {
    }

    public static void TempDebugTest() => Globals.DeviceMgr.Processor.SetOption(1);
}
