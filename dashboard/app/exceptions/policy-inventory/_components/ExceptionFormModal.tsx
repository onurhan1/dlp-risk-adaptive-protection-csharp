import React, { useState, useEffect } from 'react'
import { X, Save, Plus, Trash2 } from 'lucide-react'
import { PolicyException, Classifier, SeverityAction, Source, Destination } from '../_lib/types'

interface ExceptionFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: Partial<PolicyException>) => void
  initialData?: PolicyException | null
  ruleName?: string
}

const TABS = ['Genel', 'Classifiers', 'Severity', 'Source', 'Destination']

export default function ExceptionFormModal({ isOpen, onClose, onSave, initialData, ruleName }: ExceptionFormModalProps) {
  const [activeTab, setActiveTab] = useState(0)
  const [excName, setExcName] = useState('')
  const [enabled, setEnabled] = useState('true')
  const [description, setDescription] = useState('')
  
  // Toggle states
  const [conditionEnabled, setConditionEnabled] = useState('false')
  const [sourceEnabled, setSourceEnabled] = useState('false')
  const [destinationEnabled, setDestinationEnabled] = useState('false')

  // Array states
  const [classifiers, setClassifiers] = useState<Classifier[]>([])
  const [severityActions, setSeverityActions] = useState<SeverityAction[]>([])
  const [sources, setSources] = useState<Source[]>([])
  const [destinations, setDestinations] = useState<Destination[]>([])

  useEffect(() => {
    if (initialData) {
      setExcName(initialData.exception_rule_name || '')
      setEnabled(initialData.enabled || 'true')
      setDescription(initialData.description || '')
      setConditionEnabled(initialData.condition_enabled || 'false')
      setSourceEnabled(initialData.source_enabled || 'false')
      setDestinationEnabled(initialData.destination_enabled || 'false')
      setClassifiers(initialData.classifiers || [])
      setSeverityActions(initialData.severity_actions || [])
      setSources(initialData.sources || [])
      setDestinations(initialData.destinations || [])
    } else {
      setExcName('')
      setEnabled('true')
      setDescription('')
      setConditionEnabled('false')
      setSourceEnabled('false')
      setDestinationEnabled('false')
      setClassifiers([])
      setSeverityActions([])
      setSources([])
      setDestinations([])
    }
    setActiveTab(0)
  }, [initialData, isOpen])

  if (!isOpen) return null

  const handleSave = () => {
    onSave({
      exception_rule_name: excName,
      enabled,
      description,
      condition_enabled: conditionEnabled,
      source_enabled: sourceEnabled,
      destination_enabled: destinationEnabled,
      classifiers,
      severity_actions: severityActions,
      sources,
      destinations
    })
    onClose()
  }

  const updateArray = <T,>(arr: T[], setArr: React.Dispatch<React.SetStateAction<T[]>>, idx: number, key: keyof T, val: any) => {
    const newArr = [...arr]
    newArr[idx] = { ...newArr[idx], [key]: val }
    setArr(newArr)
  }

  const removeArray = <T,>(arr: T[], setArr: React.Dispatch<React.SetStateAction<T[]>>, idx: number) => {
    const newArr = [...arr]
    newArr.splice(idx, 1)
    setArr(newArr)
  }

  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      background: 'rgba(15, 23, 42, 0.18)', zIndex: 9999,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      padding: '24px'
    }}>
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '10px',
        width: '800px', maxWidth: '100%', height: '85vh', maxHeight: '750px',
        boxShadow: '0 24px 70px rgba(15, 23, 42, 0.22)', display: 'flex', flexDirection: 'column',
        overflow: 'hidden'
      }}>
        {/* Header */}
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <h2 style={{ margin: 0, fontSize: '18px', color: 'var(--text-primary)' }}>
              {initialData ? 'Exception Düzenle' : 'Yeni Exception Ekle'}
            </h2>
            {ruleName && <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: 'var(--text-secondary)' }}>Kural: {ruleName}</p>}
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <X size={20} />
          </button>
        </div>

        {/* Tabs Header */}
        <div style={{ display: 'flex', borderBottom: '1px solid var(--border-color)', padding: '0 20px', gap: '24px' }}>
          {TABS.map((tab, idx) => (
            <div 
              key={tab} 
              onClick={() => setActiveTab(idx)}
              style={{
                padding: '12px 0', fontSize: '14px', fontWeight: '500', cursor: 'pointer',
                color: activeTab === idx ? '#10b981' : 'var(--text-secondary)',
                borderBottom: activeTab === idx ? '2px solid #10b981' : '2px solid transparent',
                transition: 'all 0.2s'
              }}
            >
              {tab}
            </div>
          ))}
        </div>
        
        {/* Body Content */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '20px' }}>
          {activeTab === 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div style={{ display: 'flex', gap: '16px' }}>
                <div style={{ flex: 1 }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Exception Adı</label>
                  <input type="text" value={excName} onChange={(e) => setExcName(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }} />
                </div>
                <div style={{ width: '120px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Durum</label>
                  <select value={enabled} onChange={(e) => setEnabled(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)' }}>
                    <option value="true">Aktif</option>
                    <option value="false">Pasif</option>
                  </select>
                </div>
              </div>
              
              <div>
                <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '500' }}>Açıklama</label>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', background: 'var(--bg-color)', color: 'var(--text-primary)', resize: 'none' }} />
              </div>

              <div style={{ display: 'flex', gap: '24px', marginTop: '8px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={conditionEnabled === 'true'} onChange={(e) => setConditionEnabled(e.target.checked ? 'true' : 'false')} />
                  Condition Enabled
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={sourceEnabled === 'true'} onChange={(e) => setSourceEnabled(e.target.checked ? 'true' : 'false')} />
                  Source Enabled
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <input type="checkbox" checked={destinationEnabled === 'true'} onChange={(e) => setDestinationEnabled(e.target.checked ? 'true' : 'false')} />
                  Destination Enabled
                </label>
              </div>
            </div>
          )}
          
          {activeTab === 1 && (
             conditionEnabled === 'true' ? (
               <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
                  <h4 style={{ margin: 0, color: 'var(--text-primary)' }}>Classifiers</h4>
                  <button onClick={() => setClassifiers([...classifiers, { classifier_name: '' }])} style={{ background: '#10b981', color: '#fff', border: 'none', padding: '6px 12px', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px', fontSize: '13px' }}><Plus size={14} /> Yeni Ekle</button>
                </div>
                {classifiers.map((c, i) => (
                  <div key={i} style={{ display: 'flex', gap: '12px', marginBottom: '12px', alignItems: 'flex-start', background: 'var(--bg-color)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      <input placeholder="Classifier Name" value={c.classifier_name || ''} onChange={(e) => updateArray(classifiers, setClassifiers, i, 'classifier_name', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                      <input placeholder="Threshold Type" value={c.threshold_type || ''} onChange={(e) => updateArray(classifiers, setClassifiers, i, 'threshold_type', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                    </div>
                    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      <input placeholder="Threshold Value From" type="number" value={c.threshold_value_from || ''} onChange={(e) => updateArray(classifiers, setClassifiers, i, 'threshold_value_from', parseInt(e.target.value) || 0)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                      <input placeholder="Threshold Calculate Type" value={c.threshold_calculate_type || ''} onChange={(e) => updateArray(classifiers, setClassifiers, i, 'threshold_calculate_type', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                    </div>
                    <button onClick={() => removeArray(classifiers, setClassifiers, i)} style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: 'none', padding: '8px', borderRadius: '4px', cursor: 'pointer' }}><Trash2 size={16} /></button>
                  </div>
                ))}
              </div>
             ) : (
               <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
                 Condition pasif. Etkinleştirmek için Genel sekmesine gidin.
               </div>
             )
          )}

          {activeTab === 2 && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
                <h4 style={{ margin: 0, color: 'var(--text-primary)' }}>Severity Actions</h4>
                <button onClick={() => setSeverityActions([...severityActions, { selected: 'true', number_of_matches: 1, severity_type: 'LOW', action_plan: '' }])} style={{ background: '#f59e0b', color: '#fff', border: 'none', padding: '6px 12px', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px', fontSize: '13px' }}><Plus size={14} /> Yeni Ekle</button>
              </div>
              {severityActions.map((s, i) => (
                <div key={i} style={{ display: 'flex', gap: '12px', marginBottom: '12px', alignItems: 'center', background: 'var(--bg-color)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <select value={s.selected} onChange={(e) => updateArray(severityActions, setSeverityActions, i, 'selected', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }}>
                    <option value="true">Active</option>
                    <option value="false">Inactive</option>
                  </select>
                  <input placeholder="# Matches" type="number" value={s.number_of_matches} onChange={(e) => updateArray(severityActions, setSeverityActions, i, 'number_of_matches', parseInt(e.target.value) || 0)} style={{ width: '80px', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                  <select value={s.severity_type} onChange={(e) => updateArray(severityActions, setSeverityActions, i, 'severity_type', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }}>
                    <option value="LOW">LOW</option><option value="MEDIUM">MEDIUM</option><option value="HIGH">HIGH</option><option value="CRITICAL">CRITICAL</option>
                  </select>
                  <input placeholder="Action Plan" value={s.action_plan} onChange={(e) => updateArray(severityActions, setSeverityActions, i, 'action_plan', e.target.value)} style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                  <button onClick={() => removeArray(severityActions, setSeverityActions, i)} style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: 'none', padding: '8px', borderRadius: '4px', cursor: 'pointer' }}><Trash2 size={16} /></button>
                </div>
              ))}
            </div>
          )}

          {activeTab === 3 && (
            sourceEnabled === 'true' ? (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
                  <h4 style={{ margin: 0, color: 'var(--text-primary)' }}>Sources</h4>
                  <button onClick={() => setSources([...sources, { resource_name: '', resource_type: '', include: 'true' }])} style={{ background: '#3b82f6', color: '#fff', border: 'none', padding: '6px 12px', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px', fontSize: '13px' }}><Plus size={14} /> Yeni Ekle</button>
                </div>
                {sources.map((src, i) => (
                  <div key={i} style={{ display: 'flex', gap: '12px', marginBottom: '12px', alignItems: 'center', background: 'var(--bg-color)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                    <input placeholder="Resource Name" value={src.resource_name} onChange={(e) => updateArray(sources, setSources, i, 'resource_name', e.target.value)} style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                    <input placeholder="Resource Type (ör: DIR_GROUP)" value={src.resource_type} onChange={(e) => updateArray(sources, setSources, i, 'resource_type', e.target.value)} style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                    <select value={src.include} onChange={(e) => updateArray(sources, setSources, i, 'include', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }}>
                      <option value="true">Include</option>
                      <option value="false">Exclude</option>
                    </select>
                    <button onClick={() => removeArray(sources, setSources, i)} style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: 'none', padding: '8px', borderRadius: '4px', cursor: 'pointer' }}><Trash2 size={16} /></button>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
                Source pasif. Etkinleştirmek için Genel sekmesine gidin.
              </div>
            )
          )}

          {activeTab === 4 && (
            destinationEnabled === 'true' ? (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
                  <h4 style={{ margin: 0, color: 'var(--text-primary)' }}>Destinations</h4>
                  <button onClick={() => setDestinations([...destinations, { channel_type: '', channel_enabled: 'true' }])} style={{ background: '#8b5cf6', color: '#fff', border: 'none', padding: '6px 12px', borderRadius: '6px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px', fontSize: '13px' }}><Plus size={14} /> Yeni Ekle</button>
                </div>
                {destinations.map((dst, i) => (
                  <div key={i} style={{ display: 'flex', gap: '12px', marginBottom: '12px', alignItems: 'center', background: 'var(--bg-color)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                    <input placeholder="Channel Type (ör: EMAIL, HTTP)" value={dst.channel_type} onChange={(e) => updateArray(destinations, setDestinations, i, 'channel_type', e.target.value)} style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }} />
                    <select value={dst.channel_enabled} onChange={(e) => updateArray(destinations, setDestinations, i, 'channel_enabled', e.target.value)} style={{ padding: '8px', borderRadius: '4px', border: '1px solid var(--border-color)', background: 'var(--card-bg)', color: 'var(--text-primary)' }}>
                      <option value="true">Enabled</option>
                      <option value="false">Disabled</option>
                    </select>
                    <button onClick={() => removeArray(destinations, setDestinations, i)} style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: 'none', padding: '8px', borderRadius: '4px', cursor: 'pointer' }}><Trash2 size={16} /></button>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ color: 'var(--text-secondary)', fontSize: '14px', textAlign: 'center', marginTop: '40px' }}>
                Destination pasif. Etkinleştirmek için Genel sekmesine gidin.
              </div>
            )
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 20px', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
          <button onClick={onClose} style={{
            padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-color)',
            background: 'transparent', color: 'var(--text-primary)', cursor: 'pointer', fontSize: '14px'
          }}>İptal</button>
          <button 
            onClick={handleSave}
            disabled={!excName.trim()}
            style={{
              padding: '8px 16px', borderRadius: '6px', border: 'none',
              background: 'linear-gradient(135deg, #10b981, #059669)', color: '#fff', 
              cursor: excName.trim() ? 'pointer' : 'not-allowed', 
              fontSize: '14px', display: 'flex', alignItems: 'center', gap: '8px',
              opacity: excName.trim() ? 1 : 0.6
            }}
          >
            <Save size={16} /> Kaydet
          </button>
        </div>
      </div>
    </div>
  )
}
