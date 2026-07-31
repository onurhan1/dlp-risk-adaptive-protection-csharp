import type { CSSProperties } from 'react'

/**
 * Shared form styling for the playbook panels, matching the inline styles used across the
 * investigation components (SendMailModal, ManualMailSender) so the screens stay consistent.
 */

export const inputStyle: CSSProperties = {
  width: '100%',
  padding: '8px 11px',
  border: '1px solid var(--border)',
  borderRadius: '6px',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  fontFamily: 'inherit',
  fontSize: '13px',
}

export const labelStyle: CSSProperties = {
  display: 'block',
  marginBottom: '5px',
  fontWeight: 500,
  fontSize: '12px',
  color: 'var(--text-primary)',
}

export const hintStyle: CSSProperties = {
  fontSize: '11px',
  color: 'var(--text-muted)',
  marginTop: '4px',
  lineHeight: 1.4,
}

export const fieldGroupStyle: CSSProperties = {
  display: 'flex',
  flexDirection: 'column',
  gap: '14px',
}

export const primaryButtonStyle: CSSProperties = {
  display: 'inline-flex',
  alignItems: 'center',
  gap: '6px',
  padding: '8px 16px',
  background: 'var(--primary)',
  color: 'white',
  border: 'none',
  borderRadius: '6px',
  cursor: 'pointer',
  fontSize: '13px',
  fontWeight: 500,
}

export const secondaryButtonStyle: CSSProperties = {
  display: 'inline-flex',
  alignItems: 'center',
  gap: '6px',
  padding: '8px 16px',
  background: 'var(--surface)',
  color: 'var(--text-primary)',
  border: '1px solid var(--border)',
  borderRadius: '6px',
  cursor: 'pointer',
  fontSize: '13px',
}

export function disabled(style: CSSProperties, isDisabled: boolean): CSSProperties {
  return isDisabled
    ? { ...style, cursor: 'not-allowed', opacity: 0.55 }
    : style
}
