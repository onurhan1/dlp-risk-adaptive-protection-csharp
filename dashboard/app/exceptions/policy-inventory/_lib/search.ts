import { PolicyInventoryItem, PolicyInventorySearchResult } from './types'

export type SearchArea = PolicyInventorySearchResult['match_area']
export type SearchScope = PolicyInventorySearchResult['scope']

export interface PolicyInventorySearchIndexEntry extends PolicyInventorySearchResult {
  searchable_text: string
}

function normalize(value?: string | number | null) {
  return String(value ?? '').trim()
}

function areaAllowed(area: SearchArea, filter: string) {
  return filter === 'all' || filter === area
}

export function buildPolicyInventorySearchIndex(policies: PolicyInventoryItem[]): PolicyInventorySearchIndexEntry[] {
  const entries: PolicyInventorySearchIndexEntry[] = []

  const pushEntry = (
    policy: PolicyInventoryItem,
    area: SearchArea,
    field: string,
    value: string | undefined | null,
    context?: Partial<PolicyInventorySearchResult>
  ) => {
    const matchedValue = normalize(value)
    if (!matchedValue) return

    entries.push({
      ...context,
      id: [
        policy.id,
        context?.rule_id ?? 'policy',
        context?.exception_id ?? context?.scope ?? 'none',
        area,
        field,
        entries.length
      ].join('-'),
      policy_id: policy.id,
      policy_name: policy.policy_name,
      scope: context?.scope ?? 'policy',
      match_area: area,
      match_field: field,
      matched_value: matchedValue,
      searchable_text: matchedValue.toLowerCase(),
    })
  }

  policies.forEach((policy) => {
    pushEntry(policy, 'policy', 'policy_name', policy.policy_name)

    policy.rules?.forEach((rule) => {
      const ruleContext: Partial<PolicyInventorySearchResult> = {
        scope: 'rule',
        rule_id: rule.id,
        rule_name: rule.rule_name,
      }

      pushEntry(policy, 'rule', 'rule_name', rule.rule_name, ruleContext)

      rule.classifiers?.forEach((classifier) => {
        pushEntry(policy, 'classifier', 'classifier_name', classifier.classifier_name, ruleContext)
        pushEntry(policy, 'classifier', 'threshold_type', classifier.threshold_type, ruleContext)
        pushEntry(policy, 'classifier', 'threshold_calculate_type', classifier.threshold_calculate_type, ruleContext)
        pushEntry(policy, 'classifier', 'analyzed_specific_fields', classifier.analyzed_specific_fields, ruleContext)
      })

      rule.severity_actions?.forEach((severityAction) => {
        pushEntry(policy, 'severity', 'severity_type', severityAction.severity_type, ruleContext)
        pushEntry(policy, 'severity', 'action_plan', severityAction.action_plan, ruleContext)
        pushEntry(policy, 'severity', 'type', severityAction.type, ruleContext)
        pushEntry(policy, 'severity', 'max_matches', severityAction.max_matches, ruleContext)
      })

      rule.sources?.forEach((source) => {
        const sourceContext = {
          ...ruleContext,
          resource_type: source.resource_type,
          include: source.include,
        }
        pushEntry(policy, 'source', 'resource_name', source.resource_name, sourceContext)
        pushEntry(policy, 'source', 'resource_type', source.resource_type, sourceContext)
        pushEntry(policy, 'source', 'include', source.include, sourceContext)
      })

      rule.destinations?.forEach((destination) => {
        const destinationContext = {
          ...ruleContext,
          destination_type: destination.channel_type,
          enabled: destination.channel_enabled,
        }
        pushEntry(policy, 'destination', 'channel_type', destination.channel_type, destinationContext)
        pushEntry(policy, 'destination', 'channel_enabled', destination.channel_enabled, destinationContext)
        pushEntry(policy, 'destination', 'email_monitor_directions', destination.email_monitor_directions, destinationContext)

        destination.channel_resources?.forEach((resource) => {
          const resourceContext = {
            ...destinationContext,
            resource_type: resource.resource_type,
            include: resource.include,
          }
          pushEntry(policy, 'destination', 'resource_name', resource.resource_name, resourceContext)
          pushEntry(policy, 'destination', 'resource_type', resource.resource_type, resourceContext)
          pushEntry(policy, 'destination', 'include', resource.include, resourceContext)
        })
      })

      rule.exceptions?.forEach((exception) => {
        const exceptionContext: Partial<PolicyInventorySearchResult> = {
          scope: 'exception',
          rule_id: rule.id,
          rule_name: rule.rule_name,
          exception_id: exception.id,
          exception_rule_name: exception.exception_rule_name,
          enabled: exception.enabled,
          exception_enabled: exception.enabled,
        }

        pushEntry(policy, 'exception', 'exception_rule_name', exception.exception_rule_name, exceptionContext)
        pushEntry(policy, 'exception', 'description', exception.description, exceptionContext)
        pushEntry(policy, 'exception', 'enabled', exception.enabled, exceptionContext)

        exception.classifiers?.forEach((classifier) => {
          pushEntry(policy, 'classifier', 'classifier_name', classifier.classifier_name, exceptionContext)
          pushEntry(policy, 'classifier', 'threshold_type', classifier.threshold_type, exceptionContext)
          pushEntry(policy, 'classifier', 'threshold_calculate_type', classifier.threshold_calculate_type, exceptionContext)
          pushEntry(policy, 'classifier', 'analyzed_specific_fields', classifier.analyzed_specific_fields, exceptionContext)
        })

        exception.severity_actions?.forEach((severityAction) => {
          pushEntry(policy, 'severity', 'severity_type', severityAction.severity_type, exceptionContext)
          pushEntry(policy, 'severity', 'action_plan', severityAction.action_plan, exceptionContext)
          pushEntry(policy, 'severity', 'dup_severity_type', severityAction.dup_severity_type, exceptionContext)
        })

        exception.sources?.forEach((source) => {
          const sourceContext = {
            ...exceptionContext,
            resource_type: source.resource_type,
            include: source.include,
          }
          pushEntry(policy, 'source', 'resource_name', source.resource_name, sourceContext)
          pushEntry(policy, 'source', 'resource_type', source.resource_type, sourceContext)
          pushEntry(policy, 'source', 'include', source.include, sourceContext)
        })

        exception.destinations?.forEach((destination) => {
          const destinationContext = {
            ...exceptionContext,
            destination_type: destination.channel_type,
            enabled: destination.channel_enabled,
          }
          pushEntry(policy, 'destination', 'channel_type', destination.channel_type, destinationContext)
          pushEntry(policy, 'destination', 'channel_enabled', destination.channel_enabled, destinationContext)
          pushEntry(policy, 'destination', 'email_monitor_directions', destination.email_monitor_directions, destinationContext)

          destination.channel_resources?.forEach((resource) => {
            const resourceContext = {
              ...destinationContext,
              resource_type: resource.resource_type,
              include: resource.include,
            }
            pushEntry(policy, 'destination', 'resource_name', resource.resource_name, resourceContext)
            pushEntry(policy, 'destination', 'resource_type', resource.resource_type, resourceContext)
            pushEntry(policy, 'destination', 'include', resource.include, resourceContext)
          })
        })
      })
    })
  })

  return entries
}

export function searchPolicyInventoryIndex(
  index: PolicyInventorySearchIndexEntry[],
  searchQuery: string,
  searchFilter: string
): PolicyInventorySearchResult[] {
  const query = searchQuery.trim().toLowerCase()
  if (!query) return []

  return index
    .filter(entry => areaAllowed(entry.match_area, searchFilter) && entry.searchable_text.includes(query))
    .map(({ searchable_text, ...result }) => result)
}
