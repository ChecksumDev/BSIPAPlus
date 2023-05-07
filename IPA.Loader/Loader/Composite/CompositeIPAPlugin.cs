using IPA.Logging;
using IPA.Old;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IPA.Loader.Composite
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class CompositeIPAPlugin : IPlugin
    {
        private readonly IEnumerable<IPlugin> plugins;

        public CompositeIPAPlugin(IEnumerable<IPlugin> plugins)
        {
            this.plugins = plugins;
        }

        public void OnApplicationStart()
        {
            Invoke(plugin => plugin.OnApplicationStart());
        }

        public void OnApplicationQuit()
        {
            Invoke(plugin => plugin.OnApplicationQuit());
        }

        public void OnUpdate()
        {
            Invoke(plugin => plugin.OnUpdate());
        }

        public void OnFixedUpdate()
        {
            Invoke(plugin => plugin.OnFixedUpdate());
        }

        public string Name => throw new InvalidOperationException();

        public string Version => throw new InvalidOperationException();

        public void OnLevelWasLoaded(int level)
        {
            Invoke(plugin => plugin.OnLevelWasLoaded(level));
        }

        public void OnLevelWasInitialized(int level)
        {
            Invoke(plugin => plugin.OnLevelWasInitialized(level));
        }

        private void Invoke(CompositeCall callback, [CallerMemberName] string member = "")
        {
            foreach (IPlugin plugin in plugins)
            {
                try
                {
                    callback(plugin);
                }
                catch (Exception ex)
                {
                    Logger.Default.Error($"{plugin.Name} {member}: {ex}");
                }
            }
        }

        public void OnLateUpdate()
        {
            Invoke(plugin =>
            {
                if (plugin is IEnhancedPlugin saberPlugin)
                {
                    saberPlugin.OnLateUpdate();
                }
            });
        }

        private delegate void CompositeCall(IPlugin plugin);
    }
#pragma warning restore CS0618 // Type or member is obsolete
}