using System;

namespace LibVLCSharp.Shared
{
    internal struct MediaDiscovererDescriptionStructure
    {
        internal MediaDiscovererDescriptionStructure(IntPtr name, IntPtr longName, MediaDiscovererCategory category)
        {
            Name = name;
            LongName = longName;
            Category = category;
        }

        internal readonly IntPtr Name;
        internal readonly IntPtr LongName;
        internal readonly MediaDiscovererCategory Category;
    }

    /// <summary>
    /// Description of a media discoverer
    /// </summary>
    public struct MediaDiscovererDescription
    {
        internal MediaDiscovererDescription(string name, string longName, MediaDiscovererCategory category)
        {
            Name = name;
            LongName = longName;
            Category = category;
        }

        /// <summary>
        /// Media discoverer description name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Media discoverer description long name
        /// </summary>
        public string LongName { get; }

        /// <summary>
        /// Media discoverer category
        /// </summary>
        public MediaDiscovererCategory Category { get; }
    }
}
