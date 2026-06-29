import { useMemo } from 'react'
import type { Incident, PolicyReport } from '../_lib/types'
import { parseViolationTriggers, calculatePercentile } from '../_lib/utils'

interface ClassifierAccumulator {
  incidentMatches: Map<number, number>
}

type ClassifierMap = Map<string, ClassifierAccumulator>
type ExceptionMap = Map<string, ClassifierMap>
interface RuleAccumulator {
  classifiers: ClassifierMap
  exceptions: ExceptionMap
}
type RuleMap = Map<string, RuleAccumulator>
type PolicyMap = Map<string, RuleMap>

function ensurePolicy(policyMap: PolicyMap, name: string): RuleMap {
  if (!policyMap.has(name)) policyMap.set(name, new Map())
  return policyMap.get(name)!
}

function ensureRule(ruleMap: RuleMap, name: string): RuleAccumulator {
  if (!ruleMap.has(name)) ruleMap.set(name, { classifiers: new Map(), exceptions: new Map() })
  return ruleMap.get(name)!
}

function ensureClassifier(map: ClassifierMap, name: string): ClassifierAccumulator {
  if (!map.has(name)) map.set(name, { incidentMatches: new Map() })
  return map.get(name)!
}

function ensureException(exceptionMap: ExceptionMap, name: string): ClassifierMap {
  if (!exceptionMap.has(name)) exceptionMap.set(name, new Map())
  return exceptionMap.get(name)!
}

function addToClassifier(map: ClassifierMap, classifierName: string, incidentId: number, matches: number) {
  const data = ensureClassifier(map, classifierName)
  const current = data.incidentMatches.get(incidentId) || 0
  data.incidentMatches.set(incidentId, Math.max(current, matches))
}

function processClassifiers(classifiers: any[], classifierMap: ClassifierMap, incidentId: number, fallbackMatches: number) {
  if (classifiers.length > 0) {
    classifiers.forEach((c: any) => {
      addToClassifier(
        classifierMap,
        c.ClassifierName || c.classifier_name || 'Unknown Classifier',
        incidentId,
        c.NumberMatches || c.number_matches || 0
      )
    })
  } else {
    addToClassifier(classifierMap, 'No Classifier', incidentId, fallbackMatches)
  }
}

function buildClassifierStats(classifierMap: ClassifierMap) {
  return Array.from(classifierMap.entries()).map(([name, data]) => {
    const matchValues = Array.from(data.incidentMatches.values())
    const avgMatches = matchValues.length > 0 ? matchValues.reduce((a, b) => a + b, 0) / matchValues.length : 0
    const p25 = calculatePercentile(matchValues, 25)
    const p50 = calculatePercentile(matchValues, 50)
    const p70 = calculatePercentile(matchValues, 70)
    const p75 = calculatePercentile(matchValues, 75)
    const p90 = calculatePercentile(matchValues, 90)

    return {
      name,
      incidentCount: data.incidentMatches.size,
      avgMatches, p25, p50, p70, p75, p90,
      recommendations: {
        medium: { threshold: Math.max(p50 - 1, 1), label: 'Medium (Audit)', action: 'Audit' },
        high: { threshold: p90 + 1, label: 'High (Block)', action: 'Block' }
      }
    }
  })
}

function aggregateStats(maps: ClassifierMap[]) {
  const incidentMaxMatches = new Map<number, number>()
  maps.forEach(map => {
    map.forEach(data => {
      data.incidentMatches.forEach((matches, id) => {
        const current = incidentMaxMatches.get(id) || 0
        incidentMaxMatches.set(id, Math.max(current, matches))
      })
    })
  })
  
  const allMatches = Array.from(incidentMaxMatches.values())
  return {
    incidentCount: incidentMaxMatches.size,
    avgMatches: allMatches.length > 0 ? allMatches.reduce((a, b) => a + b, 0) / allMatches.length : 0,
    p25: calculatePercentile(allMatches, 25),
    p75: calculatePercentile(allMatches, 75),
    p90: calculatePercentile(allMatches, 90),
    allMatches
  }
}

export default function useUserReportData(userIncidents: Incident[]): PolicyReport[] {
  return useMemo(() => {
    if (!userIncidents.length) return []

    const policyMap: PolicyMap = new Map()

    userIncidents.forEach(incident => {
      const allTriggers = parseViolationTriggers(incident.violationTriggers)
      const triggers = allTriggers.filter((t: any) => !t.parent_rule_name && !t.ParentRuleName)
      const exceptionTriggers = allTriggers.filter((t: any) => t.parent_rule_name || t.ParentRuleName)

      if (triggers.length === 0 && exceptionTriggers.length === 0 && incident.policy) {
        const ruleMap = ensurePolicy(policyMap, incident.policy)
        const ruleData = ensureRule(ruleMap, 'Unknown Rule')
        addToClassifier(ruleData.classifiers, 'No Classifier', incident.id, incident.maxMatches || 0)
      } else {
        // Regular triggers
        triggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy || 'Unknown Policy'
          const ruleName = t.RuleName || t.rule_name || 'Unknown Rule'
          const ruleMap = ensurePolicy(policyMap, policyName)
          const ruleData = ensureRule(ruleMap, ruleName)
          processClassifiers(t.Classifiers || t.classifiers || [], ruleData.classifiers, incident.id, incident.maxMatches || 0)
        })

        // Exception triggers
        exceptionTriggers.forEach((t: any) => {
          const policyName = t.PolicyName || t.policy_name || incident.policy || 'Unknown Policy'
          const exceptionName = t.RuleName || t.rule_name || 'Unknown Exception'
          const parentRuleName = t.parent_rule_name || t.ParentRuleName
          if (!policyName || !exceptionName) return

          const ruleMap = ensurePolicy(policyMap, policyName)

          if (parentRuleName) {
            const ruleData = ensureRule(ruleMap, parentRuleName)
            const exClassifierMap = ensureException(ruleData.exceptions, exceptionName)
            processClassifiers(t.Classifiers || t.classifiers || [], exClassifierMap, incident.id, incident.maxMatches || 0)
          } else {
            const ruleData = ensureRule(ruleMap, exceptionName)
            processClassifiers(t.Classifiers || t.classifiers || [], ruleData.classifiers, incident.id, incident.maxMatches || 0)
          }
        })
      }
    })

    // Convert to array
    return Array.from(policyMap.entries()).map(([policyName, ruleMap]) => {
      const rules = Array.from(ruleMap.entries()).map(([ruleName, ruleData]) => {
        const classifiers = buildClassifierStats(ruleData.classifiers)
        const exceptions = Array.from(ruleData.exceptions.entries()).map(([exName, exClassifierMap]) => {
          const exClassifiers = buildClassifierStats(exClassifierMap)
          const exStats = aggregateStats([exClassifierMap])
          return { name: exName, incidentCount: exStats.incidentCount, avgMatches: exStats.avgMatches, p25: exStats.p25, p75: exStats.p75, p90: exStats.p90, classifiers: exClassifiers }
        })

        const allMaps: ClassifierMap[] = [ruleData.classifiers]
        ruleData.exceptions.forEach(exMap => allMaps.push(exMap))
        const ruleStats = aggregateStats(allMaps)

        return { name: ruleName, incidentCount: ruleStats.incidentCount, avgMatches: ruleStats.avgMatches, p25: ruleStats.p25, p75: ruleStats.p75, p90: ruleStats.p90, allMatches: ruleStats.allMatches, classifiers, exceptions }
      })

      // Policy-level stats
      const allPolicyMaps: ClassifierMap[] = []
      ruleMap.forEach(ruleData => {
        allPolicyMaps.push(ruleData.classifiers)
        ruleData.exceptions.forEach(exMap => allPolicyMaps.push(exMap))
      })
      const policyStats = aggregateStats(allPolicyMaps)

      return { name: policyName, incidentCount: policyStats.incidentCount, avgMatches: policyStats.avgMatches, rules }
    })
  }, [userIncidents])
}
