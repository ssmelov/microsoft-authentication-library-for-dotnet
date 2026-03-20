// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.Identity.Client.Extensibility
{
    /// <summary>
    /// Provides ambient telemetry enrichment context for MSAL metric emissions.
    /// Uses <see cref="AsyncLocal{T}"/> so the context flows naturally across await boundaries
    /// and into background proactive refresh operations.
    /// </summary>
    public static class MsalTelemetryContext
    {
        private static readonly AsyncLocal<MsalTelemetryEnrichment> s_current = new AsyncLocal<MsalTelemetryEnrichment>();

        /// <summary>
        /// Gets or sets the current telemetry enrichment.
        /// Set before calling a token acquisition method to attach custom tags to all metrics
        /// emitted during that call, including background proactive refresh operations.
        /// </summary>
        /// <example>
        /// <code>
        /// MsalTelemetryContext.Current = new MsalTelemetryEnrichment()
        ///     .Add("ClientId", clientId)
        ///     .Add("TargetResource", resource);
        ///
        /// await confidentialClientApp.AcquireTokenForClient(scopes).ExecuteAsync();
        /// </code>
        /// </example>
        public static MsalTelemetryEnrichment Current
        {
            get => s_current.Value;
            set => s_current.Value = value;
        }
    }
}
