using System;
using System.Runtime.Serialization;

namespace IPA.Loader
{
    /// <summary>
    ///     Indicates that a plugin cannot be disabled at runtime. Generally not considered an error, however.
    /// </summary>
    [Serializable]
    public class CannotRuntimeDisableException : Exception
    {
        /// <summary>
        ///     Creates an exception for the given plugin metadata.
        /// </summary>
        /// <param name="plugin">the plugin that cannot be disabled</param>
        public CannotRuntimeDisableException(PluginMetadata plugin) : base(
            $"Cannot runtime disable plugin \"{plugin.Name}\" ({plugin.Id})")
        {
            Plugin = plugin;
        }

        /// <summary>
        ///     Creats an exception with the given plugin metadata and message information.
        /// </summary>
        /// <param name="plugin">the plugin that cannot be disabled</param>
        /// <param name="message">the message to associate with it</param>
        public CannotRuntimeDisableException(PluginMetadata plugin, string message) : base(
            $"{message} \"{plugin.Name}\" ({plugin.Id})")
        {
            Plugin = plugin;
        }

        /// <summary>
        ///     Creates an exception from a serialization context. Not currently implemented.
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        /// <exception cref="NotImplementedException"></exception>
        protected CannotRuntimeDisableException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     The plugin that cannot be disabled at runtime.
        /// </summary>
        public PluginMetadata Plugin { get; }
    }
}