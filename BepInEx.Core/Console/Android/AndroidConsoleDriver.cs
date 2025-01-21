using System;
using System.IO;
using BepInEx.Logging;

namespace BepInEx.Core.Console.Android;

public class AndroidConsoleDriver : IConsoleDriver
{
    public TextWriter StandardOut { get; private set; }
    public TextWriter ConsoleOut { get; private set; }
    public bool ConsoleActive { get; private set; }
    public bool ConsoleIsExternal { get; private set; }

    public AndroidConsoleDriver()
    {
        StandardOut = System.Console.Out;
        ConsoleOut = System.Console.Out;
        ConsoleActive = true;
        ConsoleIsExternal = false;
        ConsoleOut.WriteLine("BepInEx Android Console Driver Initialized");
    }
    
    public void PreventClose()
    {
        // Not supported
    }

    public void Initialize(bool alreadyActive, bool useManagedEncoder)
    {
        StandardOut = System.Console.Out;
        ConsoleOut = System.Console.Out;
        ConsoleActive = true;
        ConsoleIsExternal = false;
    }

    public void CreateConsole(uint codepage)
    {
        Logger.Log(LogLevel.Warning, "An external console currently cannot be spawned on Android.");
    }

    public void DetachConsole()
    {
        // Not supported
    }

    public void SetConsoleColor(ConsoleColor color)
    {
        // Not supported
    }

    public void SetConsoleTitle(string title)
    {
        // Not supported
    }
}
