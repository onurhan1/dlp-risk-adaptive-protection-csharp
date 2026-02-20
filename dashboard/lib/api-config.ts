/**
 * API Configuration
 *
 * All API calls use relative URLs (e.g. "/api/...") so they go through the
 * same origin as the dashboard.  Next.js `rewrites` (see next.config.js) proxy
 * them to the .NET backend at http://127.0.0.1:5001.
 *
 * Benefits:
 *  - No CORS needed (same origin)
 *  - No mixed-content issues (protocol inherited from page)
 *  - Port 5001 does NOT need to be exposed externally
 *  - Works behind any reverse proxy / DMZ / NAT / IIS ARR
 */

function getApiUrl(): string {
  // Return empty string so that API calls like `${apiUrl}/api/...` become
  // relative paths (e.g. "/api/...").  Next.js rewrites proxy these to the
  // backend at http://127.0.0.1:5001, which means:
  //  - No cross-origin issues (same origin)
  //  - No mixed-content (HTTPS stays HTTPS)
  //  - No need to expose port 5001 to the outside
  //  - Works behind any reverse-proxy / DMZ / NAT
  return '';
}

// Export as a function that gets called at runtime, not a constant
// This ensures it works correctly in all scenarios
export function getApiUrlDynamic(): string {
  return getApiUrl();
}

// For backward compatibility, also export as constant (but it will be evaluated at module load time)
// Note: This may not work correctly in all scenarios, prefer using getApiUrlDynamic()
export const API_URL = getApiUrl();
