import axios from 'axios'
import { getApiUrlDynamic } from './api-config'

// Create axios instance
// Note: baseURL will be set dynamically per request in the interceptor
// Varsayilan istek zaman asimi. Timeout olmadan yavas/asili kalan bir istek sayfayi
// sonsuza kadar "Yukleniyor" durumunda birakiyordu. Buyuk liste ceken sayfalar bu
// degeri istek bazinda LONG_REQUEST_TIMEOUT_MS ile yukseltir.
export const DEFAULT_REQUEST_TIMEOUT_MS = 60_000
export const LONG_REQUEST_TIMEOUT_MS = 180_000

const apiClient = axios.create({
  timeout: DEFAULT_REQUEST_TIMEOUT_MS,
  headers: {
    'Content-Type': 'application/json; charset=utf-8',
  },
})

// Request interceptor to:
// 1. Set dynamic baseURL based on current hostname (works for both localhost and network IP)
// 2. Add auth token
apiClient.interceptors.request.use(
  (config) => {
    // Dynamically set baseURL for each request to ensure correct hostname detection
    // This is crucial for remote device access
    config.baseURL = getApiUrlDynamic()
    
    // Ensure UTF-8 encoding for all requests
    if (!config.headers['Content-Type']) {
      config.headers['Content-Type'] = 'application/json; charset=utf-8'
    }
    
    const token = localStorage.getItem('authToken')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor to handle auth errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && !error.config?._skipAuthRedirect) {
      localStorage.removeItem('authToken')
      localStorage.removeItem('username')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default apiClient

