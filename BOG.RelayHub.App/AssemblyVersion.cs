using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace BOG.RelayHub
{
    /// <summary>
    /// Creates a class providing information about the main assembly.
    /// </summary>
    public class AssemblyVersion : IAssemblyVersion
    {
        /// <summary>
        /// The main file which is the entry point
        /// </summary>
        public string Filename { get; private set; } = "No info available";

        /// <summary>
        /// The application name
        /// </summary>
        public string Name { get; private set; } = "No info available";

        /// <summary>
        /// The build version running
        /// </summary>
        public string Version { get; private set; } = "No info available";

        /// <summary>
        /// The build date of the assembly
        /// </summary>
        public DateTime BuildDate { get; private set; } = DateTime.MinValue;

        private bool HasAVinfo = false;

        /// <summary>
        /// Instantiator
        /// </summary>
        public AssemblyVersion()
        {
            var av = Assembly.GetEntryAssembly();
            HasAVinfo = av != null;
            if (av != null)
            {
                var FullName = av.Location.Replace("file:///", string.Empty);
                Filename = Path.GetFileName(FullName);
                Name = av.GetName().Name ?? this.Name;
                Version = av.GetName().Version?.ToString() ?? this.Version;
                BuildDate = File.GetLastWriteTime(FullName);
            }
        }

        /// <summary>
        /// Build a simple default string format for the details.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return HasAVinfo ? $"{Path.GetFileName(Filename)}, {Version}, {BuildDate:G}" : "Assembly Info not available.";
        }

        /// <summary>
        /// Build a simple default string format for the details.
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return (new JObject
            {
                { "name", Name },
                { "version", Version },
                { "built", BuildDate.ToString("G") }
            }).ToString(Newtonsoft.Json.Formatting.Indented);
        }
    }
}
