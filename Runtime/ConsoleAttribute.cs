using System;

namespace UnityEssentials
{
    /// <summary>
    /// Marks a static method as a runtime console command.
    /// 
    /// Minimal supported signatures:
    /// - void Cmd()
    /// - void Cmd(string args)
    /// - string Cmd()
    /// - string Cmd(string args)
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

        public ConsoleAttribute(string name = null, string description = null)
        {
            Name = name;
            Description = description;
        }
    }
}