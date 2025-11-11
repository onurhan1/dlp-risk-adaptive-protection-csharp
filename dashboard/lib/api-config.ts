/**
 * API Configuration
 * Automatically detects if running on same machine or remote device
 * - Same machine: uses localhost:5001
 * - Remote device: uses the hostname/IP from window.location
 */

function getApiUrl(): string {
  // Check if API URL is explicitly set via environment variable
  if (typeof window !== 'undefined' && process.env.NEXT_PUBLIC_API_URL) {
    return process.env.NEXT_PUBLIC_API_URL;
  }

  // If running in browser, detect the hostname
  if (typeof window !== 'undefined') {
    const hostname = window.location.hostname;
    
    // If accessing via localhost or 127.0.0.1, use localhost for API
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return 'http://localhost:5001';
    }
    
    // If accessing via network IP, use the same hostname for API
    // This allows remote devices to connect to the API on the same machine
    return `http://${hostname}:5001`;
  }

  // Server-side rendering fallback
  return 'http://localhost:5001';
}

export const API_URL = getApiUrl();

