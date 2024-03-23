// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

// This partial contains DocNode and Namespace related classes.
partial class DocumentBase
{
    /// <summary>
    /// Represents an explicitly generated namespace.
    /// </summary>
    public sealed class Namespace
    {
        /// <summary>
        /// Unique namespace id. This is used to represent the namespace to external client code (possibly in a
        /// different process).
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// The name of this explicit namespace.
        /// </summary>
        public NPath Name { get; }

        /// <summary>
        /// The config information, possibly null.
        /// </summary>
        public NamespaceConfig Config { get; }

        /// <summary>
        /// This should ONLY be invoked by the document!
        /// </summary>
        internal Namespace(NPath name, NamespaceConfig config, Guid? guid = null)
        {
            Validation.AssertValueOrNull(config);

            Name = name;
            Config = config;
            Guid = guid ?? Guid.NewGuid();
        }
    }

    /// <summary>
    /// The configuration information for an explicit namespace.
    /// This is optional (can be null).
    /// </summary>
    public abstract class NamespaceConfig
    {
        protected NamespaceConfig()
        {
        }
    }
}
