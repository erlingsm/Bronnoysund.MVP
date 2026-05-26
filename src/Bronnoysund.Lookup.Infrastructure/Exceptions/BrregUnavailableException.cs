// SPDX-License-Identifier: MIT

namespace Bronnoysund.Lookup.Infrastructure.Exceptions;

/// <summary>
/// Thrown when the Brreg API is temporarily unavailable — timeout, 5xx, circuit breaker open, etc.
/// </summary>
public sealed class BrregUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
