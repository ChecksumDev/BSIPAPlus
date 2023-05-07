using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
#if NET4
using Task = System.Threading.Tasks.Task;
using TaskEx = System.Threading.Tasks.Task;
#endif

#if NET3
using Net3_Proxy;
#endif

namespace IPA.Loader
{
    internal class DisabledConfig
    {
        public static DisabledConfig Instance;

        private Task disableUpdateTask;
        private int updateState;
        public static Config.Config Disabled { get; set; }

        public virtual bool Reset { get; set; } = true;

        [NonNullable]
        [UseConverter(typeof(CollectionConverter<string, HashSet<string>>))]
        public virtual HashSet<string> DisabledModIds { get; set; } = new();

        public static void Load()
        {
            Disabled = Config.Config.GetConfigFor("Disabled Mods", "json");
            Instance = Disabled.Generated<DisabledConfig>();
        }

        protected internal virtual void Changed() { }

        protected internal virtual IDisposable ChangeTransaction()
        {
            return null;
        }

        protected virtual void OnReload()
        {
            if (DisabledModIds == null || Reset)
            {
                DisabledModIds = new HashSet<string>();
                Reset = false;
            }

            if (!PluginLoader.IsFirstLoadComplete)
            {
                return; // if the first load isn't complete, skip all of this
            }

            int referToState = unchecked(++updateState);
            string[] copy = DisabledModIds.ToArray();
            if (disableUpdateTask == null || disableUpdateTask.IsCompleted)
            {
                disableUpdateTask = UpdateDisabledMods(copy);
            }
            else
            {
                disableUpdateTask = disableUpdateTask.ContinueWith(t =>
                {
                    // skip if another got here before the last finished
                    if (referToState != updateState)
                    {
                        return TaskEx.WhenAll();
                    }

                    return UpdateDisabledMods(copy);
                });
            }
        }

        private Task UpdateDisabledMods(string[] updateWithDisabled)
        {
            do
            {
                using StateTransitionTransaction transaction = PluginManager.PluginStateTransaction();
                PluginMetadata[] disabled = transaction.DisabledPlugins.ToArray();
                foreach (PluginMetadata plugin in disabled)
                {
                    transaction.Enable(plugin, true);
                }

                PluginMetadata[] all = transaction.EnabledPlugins.ToArray();
                foreach (PluginMetadata plugin in all.Where(m => updateWithDisabled.Contains(m.Id)))
                {
                    transaction.Disable(plugin, true);
                }

                try
                {
                    if (transaction.WillNeedRestart)
                    {
                        Logger.Loader.Warn("Runtime disabled config reload will need game restart to apply");
                    }

                    return transaction.Commit().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Logger.Loader.Error("Error changing disabled plugins");
                            Logger.Loader.Error(t.Exception);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                }
            } while (true);
        }
    }
}