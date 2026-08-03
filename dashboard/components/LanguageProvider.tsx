'use client'

import React, { createContext, useContext, useState, useCallback, useEffect, ReactNode } from 'react'
import { Locale, getTranslation, defaultLocale } from '@/lib/i18n'

type TranslationVars = Record<string, string | number>

interface LanguageContextType {
    locale: Locale
    setLocale: (locale: Locale) => void
    t: (key: string, vars?: TranslationVars) => string
}

const LanguageContext = createContext<LanguageContextType>({
    locale: defaultLocale,
    setLocale: () => { },
    t: (key: string) => key,
})

export function LanguageProvider({ children }: { children: ReactNode }) {
    const [locale, setLocaleState] = useState<Locale>(defaultLocale)

    // Load from localStorage on mount
    useEffect(() => {
        const saved = localStorage.getItem('dlp-locale') as Locale | null
        if (saved && (saved === 'tr' || saved === 'en')) {
            setLocaleState(saved)
        }
    }, [])

    const setLocale = useCallback((newLocale: Locale) => {
        setLocaleState(newLocale)
        localStorage.setItem('dlp-locale', newLocale)
    }, [])

    const t = useCallback((key: string, vars?: TranslationVars) => {
        return getTranslation(locale, key, vars)
    }, [locale])

    return (
        <LanguageContext.Provider value={{ locale, setLocale, t }}>
            {children}
        </LanguageContext.Provider>
    )
}

export function useTranslation() {
    const context = useContext(LanguageContext)
    if (!context) {
        throw new Error('useTranslation must be used within a LanguageProvider')
    }
    return context
}

export { LanguageContext }
