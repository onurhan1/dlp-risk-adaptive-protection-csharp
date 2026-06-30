export interface Classifier {
    id?: number;
    classifier_name: string;
    threshold_type?: string;
    threshold_value_from?: number;
    threshold_calculate_type?: string;
    position?: number;
    analyzed_specific_fields?: string;
}

export interface SeverityAction {
    id?: number;
    type?: string; // only for rules
    max_matches?: string; // only for rules
    selected: string;
    number_of_matches: number;
    severity_type: string;
    dup_severity_type?: string;
    action_plan: string;
}

export interface Source {
    id?: number;
    resource_name: string;
    resource_type: string;
    include: string;
}

export interface DestinationChannelResource {
    id?: number;
    resource_name: string;
    resource_type: string;
    include: string;
}

export interface Destination {
    id?: number;
    email_monitor_directions?: string;
    channel_type: string;
    channel_enabled: string;
    resources?: DestinationChannelResource[];
}

export interface PolicyException {
    id: number;
    exception_rule_name: string;
    enabled: string;
    description?: string;
    condition_enabled?: string;
    source_enabled?: string;
    destination_enabled?: string;
    parts_count_type?: string;
    condition_relation_type?: string;
    classifiers?: Classifier[];
    severity_actions?: SeverityAction[];
    sources?: Source[];
    destinations?: Destination[];
}

export interface PolicyRule {
    id: number;
    rule_name: string;
    parts_count_type?: string;
    condition_relation_type?: string;
    classifiers?: Classifier[];
    severity_actions?: SeverityAction[];
    sources?: Source[];
    destinations?: Destination[];
    exceptions?: PolicyException[];
}

export interface PolicyInventoryItem {
    id: number;
    policy_name: string;
    rules: PolicyRule[];
}

export interface PolicyInventoryStats {
    totalPolicies: number;
    totalRules: number;
    totalExceptions: number;
    activeExceptionsPercentage: number;
}
