// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensibility
{
    /// <summary>
    /// Holds custom tags to be merged into MSAL metric emissions.
    /// Set an instance on <see cref="MsalTelemetryContext.Current"/> before calling a token
    /// acquisition method to attach additional dimensions to all metrics emitted during that call,
    /// including background proactive refresh operations.
    /// </summary>
    public class MsalTelemetryEnrichment
    {
        private readonly List<KeyValuePair<string, object>> _tags = new();

        /// <summary>
        /// Adds a tag to include in MSAL metric emissions.
        /// Returns the same instance to allow chaining.
        /// </summary>
        public MsalTelemetryEnrichment Add(string key, object value)
        {
            _tags.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        /// <summary>
        /// The custom tags to be merged into MSAL metrics.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, object>> Tags => _tags;
    }
}
