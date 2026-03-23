// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.TelemetryCore.Internal.Events;
using Microsoft.Identity.Client.TelemetryCore.OpenTelemetry;

namespace Microsoft.Identity.Client.Platforms.Features.OpenTelemetry
{
    /// <summary>
    /// Class to hold the OpenTelemetry objects used by MSAL.
    /// </summary>
    internal class OtelInstrumentation : IOtelInstrumentation
    {
        /// <summary>
        /// Constant to hold the name of the Meter.
        /// </summary>
        public const string MeterName = "MicrosoftIdentityClient_Common_Meter";

        private const string SuccessCounterName = "MsalSuccess";
        private const string FailedCounterName = "MsalFailure";
        private const string TotalDurationHistogramName = "MsalTotalDuration.1A";
        private const string DurationInL1CacheHistogramName = "MsalDurationInL1CacheInUs.1B";
        private const string DurationInL2CacheHistogramName = "MsalDurationInL2Cache.1A";
        private const string DurationInHttpHistogramName = "MsalDurationInHttp.1A";
        private const string DurationInExtensionInMsHistogram = "MsalDurationInExtensionInMs.1B";

        /// <summary>
        /// Meter to hold the MSAL metrics.
        /// </summary>
        internal static readonly Meter Meter = new Meter(MeterName, "1.0.0");

        /// <summary>
        /// Counter to hold the number of successful token acquisition calls.
        /// </summary>
        internal static readonly Lazy<Counter<long>> s_successCounter = new(() => Meter.CreateCounter<long>(
            SuccessCounterName,
            description: "Number of successful token acquisition calls"));

        /// <summary>
        /// Counter to hold the number of failed token acquisition calls.
        /// </summary>
        internal static readonly Lazy<Counter<long>> s_failureCounter = new(() => Meter.CreateCounter<long>(
            FailedCounterName,
            description: "Number of failed token acquisition calls"));

        /// <summary>
        /// Histogram to record total duration in milliseconds of token acquisition calls.
        /// </summary>
        internal static readonly Lazy<Histogram<long>> s_durationTotal = new(() => Meter.CreateHistogram<long>(
            TotalDurationHistogramName,
            unit: "ms",
            description: "Performance of token acquisition calls total latency"));

        /// <summary>
        /// Histogram to record total duration of token acquisition calls in microseconds(us) when token is fetched from L1 cache.
        /// </summary>
        internal static readonly Lazy<Histogram<long>> s_durationInL1CacheInUs = new(() => Meter.CreateHistogram<long>(
            DurationInL1CacheHistogramName,
            unit: "us",
            description: "Performance of token acquisition calls total latency in microseconds when L1 cache is used."));

        /// <summary>
        /// Histogram to record duration in L2 cache for token acquisition calls.
        /// </summary>
        internal static readonly Lazy<Histogram<long>> s_durationInL2Cache = new Lazy<Histogram<long>>(() => Meter.CreateHistogram<long>(
            DurationInL2CacheHistogramName,
            unit: "ms",
            description: "Performance of token acquisition calls cache latency"));

        /// <summary>
        /// Histogram to record duration in milliseconds in http when the token is fetched from identity provider.
        /// </summary>
        internal static readonly Lazy<Histogram<long>> s_durationInHttp = new Lazy<Histogram<long>>(() => Meter.CreateHistogram<long>(
            DurationInHttpHistogramName,
            unit: "ms",
            description: "Performance of token acquisition calls network latency"));

        /// <summary>
        /// Histogram to record total duration of extension modifications in microseconds(us).
        /// </summary>
        internal static readonly Lazy<Histogram<long>> s_durationInExtensionInMs = new(() => Meter.CreateHistogram<long>(
            DurationInExtensionInMsHistogram,
            unit: "us",
            description: "Performance of token acquisition calls extension latency."));

        public OtelInstrumentation()
        {
            // Needed to fail fast if the runtime, like in-process Azure Functions, doesn't support OpenTelemetry 
            _ = Meter.Version;
        }

        // Aggregates the successful requests based on token source and cache refresh reason.
        public void LogSuccessMetrics(
            string platform,
            ApiEvent.ApiIds apiId,
            string callerSdkId,
            string callerSdkVersion,
            CacheLevel cacheLevel,
            long totalDurationInUs,
            AuthenticationResultMetadata authResultMetadata,
            ILoggerAdapter logger,
            IList<KeyValuePair<string, object>> extraTags = null)
        {
            IncrementSuccessCounter(
                platform,
                apiId,
                callerSdkId,
                callerSdkVersion,
                authResultMetadata.TokenSource,
                authResultMetadata.CacheRefreshReason,
                cacheLevel,
                logger,
                authResultMetadata.TelemetryTokenType,
                extraTags);

            if (s_durationTotal.Value.Enabled)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.TokenSource, authResultMetadata.TokenSource },
                    { TelemetryConstants.CacheLevel, cacheLevel },
                    { TelemetryConstants.CacheRefreshReason, authResultMetadata.CacheRefreshReason },
                    { TelemetryConstants.TokenType, authResultMetadata.TelemetryTokenType }
                };
                AppendExtraTags(ref tags, extraTags);
                s_durationTotal.Value.Record(authResultMetadata.DurationTotalInMs, tags);
            }

            // Only log cache duration if L2 cache was used.
            if (s_durationInL2Cache.Value.Enabled && cacheLevel == CacheLevel.L2Cache)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.CacheRefreshReason, authResultMetadata.CacheRefreshReason }
                };
                AppendExtraTags(ref tags, extraTags);
                s_durationInL2Cache.Value.Record(authResultMetadata.DurationInCacheInMs, tags);
            }

            // Only log duration in HTTP when token is fetched from IDP.
            if (s_durationInHttp.Value.Enabled && authResultMetadata.TokenSource == TokenSource.IdentityProvider)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.TokenType, authResultMetadata.TelemetryTokenType }
                };
                AppendExtraTags(ref tags, extraTags);
                s_durationInHttp.Value.Record(authResultMetadata.DurationInHttpInMs, tags);
            }

            // Only log duration in microseconds when the cache level is L1.
            if (s_durationInL1CacheInUs.Value.Enabled && authResultMetadata.TokenSource == TokenSource.Cache
                && authResultMetadata.CacheLevel.Equals(CacheLevel.L1Cache))
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.TokenSource, authResultMetadata.TokenSource },
                    { TelemetryConstants.CacheLevel, authResultMetadata.CacheLevel },
                    { TelemetryConstants.CacheRefreshReason, authResultMetadata.CacheRefreshReason }
                };
                AppendExtraTags(ref tags, extraTags);
                s_durationInL1CacheInUs.Value.Record(totalDurationInUs, tags);
            }

            if (s_durationInExtensionInMs.Value.Enabled)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.TokenSource, authResultMetadata.TokenSource },
                    { TelemetryConstants.CacheLevel, authResultMetadata.CacheLevel },
                    { TelemetryConstants.TokenType, authResultMetadata.TelemetryTokenType }
                };
                AppendExtraTags(ref tags, extraTags);
                s_durationInExtensionInMs.Value.Record(authResultMetadata.DurationCreatingExtendedTokenInUs, tags);
            }
        }

        public void IncrementSuccessCounter(string platform,
            ApiEvent.ApiIds apiId,
            string callerSdkId,
            string callerSdkVersion,
            TokenSource tokenSource,
            CacheRefreshReason cacheRefreshReason,
            CacheLevel cacheLevel,
            ILoggerAdapter logger,
            int tokenType,
            IList<KeyValuePair<string, object>> extraTags = null)
        {
            if (s_successCounter.Value.Enabled)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.CallerSdkId, (callerSdkId ?? string.Empty) + "," + (callerSdkVersion ?? string.Empty) },
                    { TelemetryConstants.TokenSource, tokenSource },
                    { TelemetryConstants.CacheRefreshReason, cacheRefreshReason },
                    { TelemetryConstants.CacheLevel, cacheLevel },
                    { TelemetryConstants.TokenType, tokenType }
                };
                AppendExtraTags(ref tags, extraTags);
                s_successCounter.Value.Add(1, tags);
                logger.Verbose(() => "[OpenTelemetry] Completed incrementing to success counter.");
            }
        }

        public void LogFailureMetrics(string platform,
            string errorCode,
            ApiEvent.ApiIds apiId,
            string callerSdkId,
            string callerSdkVersion,
            CacheRefreshReason cacheRefreshReason,
            int tokenType,
            IList<KeyValuePair<string, object>> extraTags = null)
        {
            if (s_failureCounter.Value.Enabled)
            {
                var tags = new TagList
                {
                    { TelemetryConstants.MsalVersion, MsalIdHelper.GetMsalVersion() },
                    { TelemetryConstants.Platform, platform },
                    { TelemetryConstants.ErrorCode, errorCode },
                    { TelemetryConstants.ApiId, apiId },
                    { TelemetryConstants.CallerSdkId, (callerSdkId ?? string.Empty) + "," + (callerSdkVersion ?? string.Empty) },
                    { TelemetryConstants.CacheRefreshReason, cacheRefreshReason },
                    { TelemetryConstants.TokenType, tokenType }
                };
                AppendExtraTags(ref tags, extraTags);
                s_failureCounter.Value.Add(1, tags);
            }
        }

        private static void AppendExtraTags(ref TagList tags, IList<KeyValuePair<string, object>> extraTags)
        {
            if (extraTags == null) return;
            foreach (var tag in extraTags)
                tags.Add(tag.Key, tag.Value);
        }
    }
}
