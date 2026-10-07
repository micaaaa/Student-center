import { useResource } from '../hooks/useResource';
import { money } from '../lib/billing';

const summaries: Record<
    string,
    { path: string; fields: [string, string][]; currency?: boolean; note?: string }
> = {
    students: {
        path: '/api/students/overview',
        fields: [
            ['total', 'Student profiles'],
            ['active', 'Active profiles'],
        ],
    },
    applications: {
        path: '/api/staff/applications/overview',
        fields: [
            ['submitted', 'Awaiting review'],
            ['underReview', 'Under review'],
        ],
    },
    housing: {
        path: '/api/accommodations/overview',
        fields: [
            ['reserved', 'Awaiting move-in'],
            ['residents', 'Current residents'],
        ],
    },
    meals: {
        path: '/api/meals/overview',
        fields: [
            ['purchased', 'Meals purchased'],
            ['consumed', 'Meals consumed'],
        ],
        note: 'Today (UTC)',
    },
    maintenance: {
        path: '/api/maintenance/overview',
        fields: [
            ['submitted', 'New requests'],
            ['ongoing', 'Accepted or in progress'],
        ],
    },
    payments: {
        path: '/api/billing/overview',
        fields: [
            ['outstanding', 'Outstanding'],
            ['overdue', 'Overdue'],
        ],
        currency: true,
    },
};

function Summary({ service }: { service: string }) {
    const config = summaries[service];
    const resource = useResource<Record<string, number>>(config.path);
    if (resource.loading) return <p role="status">Loading overview…</p>;
    if (resource.error)
        return (
            <div className="service-summary">
                <p>Overview is temporarily unavailable.</p>
                <button className="text-link" type="button" onClick={resource.reload}>
                    Try again
                </button>
            </div>
        );
    return (
        <div className="service-summary">
            {config.note && <p>{config.note}</p>}
            <dl className="overview-metrics">
                {config.fields.map(([key, label]) => (
                    <div key={key}>
                        <dt>{label}</dt>
                        <dd>
                            {config.currency
                                ? money(resource.data![key])
                                : resource.data![key].toLocaleString('en-GB')}
                        </dd>
                    </div>
                ))}
            </dl>
        </div>
    );
}
export function StaffServiceSummary({
    service,
    permissions,
}: {
    service: string;
    permissions: string[];
}) {
    if (
        !summaries[service] ||
        (service === 'maintenance' && !permissions.includes('ManageMaintenance'))
    )
        return null;
    return <Summary service={service} />;
}
