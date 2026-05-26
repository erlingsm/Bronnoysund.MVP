// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Infrastructure.Exceptions;

/// <summary>
/// Thrown when the Brreg API is temporarily unavailable — timeout, 5xx, circuit breaker open, etc.
/// </summary>
public sealed class BrregUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
