using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpServerLib;

namespace SharpServer.ModuleInterface
{
    public delegate void HookCallback(ref HttpRequestInfo obj);

    public interface IModule
    {
        IModuleHost Host { get; set; }
        string Name { get; }
        string Description { get; }
        string Author { get; }
        string Version { get; }

        /* Required methods to be implemented */
        void Initialize();
        void Shutdown();

        int ProcessRequest(ref HttpRequestInfo request);

    }

    public interface IModuleHost
    {
        void RegisterHook(string hook, HookCallback method);
    }
}
