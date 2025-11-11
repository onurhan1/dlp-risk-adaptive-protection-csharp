import axios from 'axios'
import { getApiUrlDynamic } from './api-config'

// Create axios instance
// Note: baseURL will be set dynamically per request in the interceptor
const apiClient = axios.create({
  headers: {
    'Content-Type': 'application/json',
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
    if (error.response?.status === 401) {
      // Unauthorized - clear token and redirect to login
      localStorage.removeItem('authToken')
      localStorage.removeItem('username')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default apiClient

