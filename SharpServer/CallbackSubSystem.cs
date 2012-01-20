using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpServerLib;
using SharpServer.ModuleInterface;

namespace SharpServer
{
    class LoadedCallback
    {
        public HookCallback method = null;
        public string hook = null;
    }

    class CallbackSubSystem
    {
        private List<LoadedCallback> callbacks = null;

        public CallbackSubSystem()
        {
            callbacks = new List<LoadedCallback>();
        }

        public void RegisterCallback(string hook, HookCallback method)
        {
            LoadedCallback cb = new LoadedCallback();
            cb.hook = hook;
            cb.method = method;

            callbacks.Add(cb);
        }

        public void DeregisterCallback(string hook, HookCallback method)
        {
            int i = 0;
            foreach (LoadedCallback callback in callbacks)
            {
                if (callback.hook == hook && callback.method == method)
                {
                    callbacks.RemoveAt(i);
                }
                i++;
            }
        }

        public void RunHook(string hook, ref HttpRequestInfo obj)
        {
            foreach (LoadedCallback callback in callbacks)
            {
                if (callback.hook == hook)
                {
                    callback.method(ref obj);
                }
            }
        }
    }
}
