// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Identity.Client
{
    /// <summary>
    /// Represents the outcome of a token acquisition attempt, containing either a successful
    /// <see cref="AuthenticationResult"/> or the <see cref="Exception"/> that caused the failure.
    /// Exactly one of the two properties is non-null.
    /// </summary>
    public class TokenAcquisitionResult
    {
        /// <summary>
        /// The authentication result on success; <c>null</c> on failure.
        /// </summary>
        public AuthenticationResult AuthenticationResult { get; }

        /// <summary>
        /// The exception that caused the failure; <c>null</c> on success.
        /// </summary>
        public Exception Exception { get; }

        internal TokenAcquisitionResult(AuthenticationResult authenticationResult, Exception exception)
        {
            AuthenticationResult = authenticationResult;
            Exception = exception;
        }
    }
}
