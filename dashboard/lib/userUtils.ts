/**
 * Resolves "unknown" or empty user identifiers using a fallback chain.
 * Mirrors the backend GetValidUserIdentifier logic.
 *
 * @param primary   - The primary identifier (user_email, login_name, etc.)
 * @param fallbacks - Additional fallback values to try (email_address, full_name, etc.)
 * @returns The first valid (non-empty, non-"unknown") value, or the original primary.
 */
export function resolveUser(primary?: string | null, ...fallbacks: (string | null | undefined)[]): string {
    if (primary && primary.toLowerCase() !== 'unknown' && primary.trim() !== '') {
        return primary
    }

    for (const fb of fallbacks) {
        if (fb && fb.toLowerCase() !== 'unknown' && fb.trim() !== '') {
            return fb
        }
    }

    return primary || ''
}
