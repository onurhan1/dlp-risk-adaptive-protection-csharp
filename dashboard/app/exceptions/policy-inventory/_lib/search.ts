import { PolicyInventoryItem, PolicyInventorySearchResult } from './types'

type SearchArea = PolicyInventorySearchResult['match_area']

function normalize(value?: string | number | null) {
  return String(value ?? '').trim()
}

function includesQuery(value: string | undefined | null, query: string) {
  return normalize(value).toLowerCase().includes(query)
}

function areaAllowed(area: SearchArea, filter: string) {
  return filter === 'all' || filter === area
}

export function buildPolicyInventorySearchResults(
  policies: PolicyInventoryItem[],
  searchQuery: string,
  searchFilter: string
): PolicyInventorySearchResult[] {
  const query = searchQuery.trim().toLowerCase()
  if (!query) return []

  const results: PolicyInventorySearchResult[] = []

  const pushResult = (
    policy: PolicyInventoryItem,
    area: SearchArea,
    field: string,
    value: string | undefined | null,
    context?: Partial<PolicyInventorySearchResult>
  ) => {
    const matchedValue = normalize(value)
    if (!matchedValue || !areaAllowed(area, searchFilter) || !includesQuery(matchedValue, query)) return

    results.push({
      ...context,
      id: [
        policy.id,
        context?.rule_id ?? 'policy',
        context?.exception_id ?? context?.scope ?? 'none',
        area,
        field,
        results.length
      ].join('-'),
      policy_id: policy.id,
      policy_name: policy.policy_name,
      scope: context?.scope ?? 'policy',
      match_area: area,
      match_field: field,
      matched_value: matchedValue,
    })
  }

  policies.forEach((policy) => {
    pushResult(policy, 'policy', 'policy_name', policy.policy_name)

    policy.rules?.forEach((rule) => {
      const ruleContext: Partial<PolicyInventorySearchResult> = {
        scope: 'rule',
        rule_id: rule.id,
        rule_name: rule.rule_name,
      }

      pushResult(policy, 'rule', 'rule_name', rule.rule_name, ruleContext)

      rule.classifiers?.forEach((classifier) => {
        pushResult(policy, 'classifier', 'classifier_name', classifier.classifier_name, ruleContext)
        pushResult(policy, 'classifier', 'threshold_type', classifier.threshold_type, ruleContext)
        pushResult(policy, 'classifier', 'threshold_calculate_type', classifier.threshold_calculate_type, ruleContext)
        pushResult(policy, 'classifier', 'analyzed_specific_fields', classifier.analyzed_specific_fields, ruleContext)
      })

      rule.severity_actions?.forEach((severityAction) => {
        pushResult(policy, 'severity', 'severity_type', severityAction.severity_type, ruleContext)
        pushResult(policy, 'severity', 'action_plan', severityAction.action_plan, ruleContext)
        pushResult(policy, 'severity', 'type', severityAction.type, ruleContext)
        pushResult(policy, 'severity', 'max_matches', severityAction.max_matches, ruleContext)
      })

      rule.sources?.forEach((source) => {
        const sourceContext = {
          ...ruleContext,
          resource_type: source.resource_type,
          include: source.include,
        }
        pushResult(policy, 'source', 'resource_name', source.resource_name, sourceContext)
        pushResult(policy, 'source', 'resource_type', source.resource_type, sourceContext)
        pushResult(policy, 'source', 'include', source.include, sourceContext)
      })

      rule.destinations?.forEach((destination) => {
        const destinationContext = {
          ...ruleContext,
          destination_type: destination.channel_type,
          enabled: destination.channel_enabled,
        }
        pushResult(policy, 'destination', 'channel_type', destination.channel_type, destinationContext)
        pushResult(policy, 'destination', 'channel_enabled', destination.channel_enabled, destinationContext)
        pushResult(policy, 'destination', 'email_monitor_directions', destination.email_monitor_directions, destinationContext)

        destination.channel_resources?.forEach((resource) => {
          const resourceContext = {
            ...destinationContext,
            resource_type: resource.resource_type,
            include: resource.include,
          }
          pushResult(policy, 'destination', 'resource_name', resource.resource_name, resourceContext)
          pushResult(policy, 'destination', 'resource_type', resource.resource_type, resourceContext)
          pushResult(policy, 'destination', 'include', resource.include, resourceContext)
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
        }

        pushResult(policy, 'exception', 'exception_rule_name', exception.exception_rule_name, exceptionContext)
        pushResult(policy, 'exception', 'description', exception.description, exceptionContext)
        pushResult(policy, 'exception', 'enabled', exception.enabled, exceptionContext)

        exception.classifiers?.forEach((classifier) => {
          pushResult(policy, 'classifier', 'classifier_name', classifier.classifier_name, exceptionContext)
          pushResult(policy, 'classifier', 'threshold_type', classifier.threshold_type, exceptionContext)
          pushResult(policy, 'classifier', 'threshold_calculate_type', classifier.threshold_calculate_type, exceptionContext)
          pushResult(policy, 'classifier', 'analyzed_specific_fields', classifier.analyzed_specific_fields, exceptionContext)
        })

        exception.severity_actions?.forEach((severityAction) => {
          pushResult(policy, 'severity', 'severity_type', severityAction.severity_type, exceptionContext)
          pushResult(policy, 'severity', 'action_plan', severityAction.action_plan, exceptionContext)
          pushResult(policy, 'severity', 'dup_severity_type', severityAction.dup_severity_type, exceptionContext)
        })

        exception.sources?.forEach((source) => {
          const sourceContext = {
            ...exceptionContext,
            resource_type: source.resource_type,
            include: source.include,
          }
          pushResult(policy, 'source', 'resource_name', source.resource_name, sourceContext)
          pushResult(policy, 'source', 'resource_type', source.resource_type, sourceContext)
          pushResult(policy, 'source', 'include', source.include, sourceContext)
        })

        exception.destinations?.forEach((destination) => {
          const destinationContext = {
            ...exceptionContext,
            destination_type: destination.channel_type,
            enabled: destination.channel_enabled,
          }
          pushResult(policy, 'destination', 'channel_type', destination.channel_type, destinationContext)
          pushResult(policy, 'destination', 'channel_enabled', destination.channel_enabled, destinationContext)
          pushResult(policy, 'destination', 'email_monitor_directions', destination.email_monitor_directions, destinationContext)

          destination.channel_resources?.forEach((resource) => {
            const resourceContext = {
              ...destinationContext,
              resource_type: resource.resource_type,
              include: resource.include,
            }
            pushResult(policy, 'destination', 'resource_name', resource.resource_name, resourceContext)
            pushResult(policy, 'destination', 'resource_type', resource.resource_type, resourceContext)
            pushResult(policy, 'destination', 'include', resource.include, resourceContext)
          })
        })
      })
    })
  })

  return results
}
