using System;

namespace BOG.RelayHub
{
    /// <summary>
    /// Object which contains information about the main executing assembly.
    /// </summary>
    public interface IAssemblyVersion
    {
        /// <summary>
        /// The main file which is the entry point
        /// </summary>
        string Filename { get; }

        /// <summary>
        /// The application name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The build version running
        /// </summary>
        string Version { get; }

        /// <summary>
        /// The build date of the assembly
        /// </summary>
        DateTime BuildDate { get; }

        /// <summary>
        /// Default string format for the details.
        /// </summary>
        /// <returns></returns>
        string ToString();

        /// <summary>
        /// JSON object for the details.
        /// </summary>
        /// <returns></returns>
        string ToJson();
    }
}