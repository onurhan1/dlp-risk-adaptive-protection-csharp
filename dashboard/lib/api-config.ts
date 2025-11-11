/**
 * API Configuration
 * Automatically detects if running on same machine or remote device
 * - Same machine: uses localhost:5001
 * - Remote device: uses the hostname/IP from window.location
 * 
 * This function is called at runtime to ensure it works correctly
 * in both same-machine and remote-device scenarios.
 */

function getApiUrl(): string {
  // If running in browser, detect the hostname dynamically
  if (typeof window !== 'undefined') {
    const hostname = window.location.hostname;
    
    // If accessing via localhost or 127.0.0.1, use localhost for API
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return 'http://localhost:5001';
    }
    
    // If accessing via network IP, use the same hostname for API
    // This allows remote devices to connect to the API on the same machine
    // Example: If dashboard is accessed via 192.168.1.100:3002,
    // API will be accessed via 192.168.1.100:5001
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

