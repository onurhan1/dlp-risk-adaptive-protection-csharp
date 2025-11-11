/**
 * API Configuration
 * 
 * IMPORTANT: This uses window.location.hostname which is the HOST where the dashboard
 * is being served from, NOT the client's computer hostname.
 * 
 * Scenario Examples:
 * 
 * 1. Dashboard hosted on 192.168.1.100:3002
 *    - Client A accesses: http://192.168.1.100:3002
 *      → window.location.hostname = "192.168.1.100"
 *      → API URL = http://192.168.1.100:5001 ✅ (Correct - API is on same server)
 * 
 *    - Client B accesses: http://192.168.1.100:3002
 *      → window.location.hostname = "192.168.1.100"
 *      → API URL = http://192.168.1.100:5001 ✅ (Correct - API is on same server)
 * 
 * 2. Dashboard hosted on localhost:3002 (same machine as API)
 *    - Client accesses: http://localhost:3002
 *      → window.location.hostname = "localhost"
 *      → API URL = http://localhost:5001 ✅ (Correct - API is on same machine)
 * 
 * This works correctly because:
 * - window.location.hostname = the server where dashboard is hosted
 * - NOT the client's computer hostname
 * - All clients connecting to the same dashboard server will use the same API server
 */

function getApiUrl(): string {
  // If running in browser, detect the hostname dynamically
  if (typeof window !== 'undefined') {
    const hostname = window.location.hostname;
    
    // If accessing via localhost or 127.0.0.1, use localhost for API
    // This means dashboard and API are on the same machine
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return 'http://localhost:5001';
    }
    
    // If accessing via network IP, use the same hostname for API
    // This means: if dashboard is on 192.168.1.100:3002, API is on 192.168.1.100:5001
    // ALL clients connecting to 192.168.1.100:3002 will use 192.168.1.100:5001 for API
    // This is correct because API and Dashboard are on the same server
    return `http://${hostname}:5001`;
  }

  // Server-side rendering fallback (should not happen in client components)
  // Check environment variable for build-time configuration
  const envUrl = process.env.NEXT_PUBLIC_API_URL;
  if (envUrl) {
    return envUrl;
  }

  return 'http://localhost:5001';
}

// Export as a function that gets called at runtime, not a constant
// This ensures it works correctly in all scenarios
export function getApiUrlDynamic(): string {
  return getApiUrl();
}

// For backward compatibility, also export as constant (but it will be evaluated at module load time)
// Note: This may not work correctly in all scenarios, prefer using getApiUrlDynamic()
export const API_URL = getApiUrl();
