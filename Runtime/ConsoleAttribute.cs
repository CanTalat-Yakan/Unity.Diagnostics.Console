using System;

namespace UnityEssentials
{
    public enum ConsoleTarget
    {
        /// <summary>
        /// Resolve the declaring type via a public static <c>Instance</c> property.
        /// </summary>
        Singleton = 0,

        /// <summary>
        /// Find exactly one instance in the loaded scenes (errors if none or multiple).
        /// This is the default for instance commands.
        /// </summary>
        SceneSingle = 1,

        /// <summary>
        /// Invoke on all instances found in the loaded scenes (no-op if none).
        /// </summary>
        SceneAll = 2,
    }

    /// <summary>
    /// Marks a method as a runtime console command.
    /// 
    /// Discovery is reflection-based: during startup, the console scans loaded runtime assemblies and registers
    /// method metadata (even if no target instance exists yet).
    /// 
    /// Static vs instance invocation is inferred from the method itself:
    /// - Static methods are invoked directly.
    /// - Instance methods resolve a target using <see cref="ConsoleTarget"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class ConsoleAttribute : Attribute
    {
        /// <summary>
        /// Command name as typed by the user. If null/empty, the method name is used.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Optional help string.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// How to resolve a target for instance commands.
        /// 
        /// Note: This value is ignored for static methods.
        /// </summary>
        public ConsoleTarget Target { get; set; } = ConsoleTarget.SceneSingle;

        public ConsoleAttribute(string name = null, string description = null)
        {
            Name = name;
            Description = description;
        }

        public ConsoleAttribute(string name, string description, ConsoleTarget target)
        {
            Name = name;
            Description = description;
            Target = target;
        }
    }
}