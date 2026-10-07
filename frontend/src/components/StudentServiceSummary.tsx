import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import { useOptionalResource } from '../lib/results';
import type { StudentApplication } from '../lib/applications';
import { statusLabel } from '../lib/applications';
import type { MyAccommodation } from '../lib/accommodation';
import type { Entitlement } from '../lib/food';
import { mealTypes } from '../lib/food';
import type { Balance } from '../lib/billing';
import { money } from '../lib/billing';

function Summary({
    resource,
    children,
}: {
    resource: { loading: boolean; error: unknown; reload: () => void };
    children: ReactNode;
}) {
    if (resource.loading) return <p role="status">Loading current information…</p>;
    if (resource.error)
        return (
            <div className="service-summary">
                <p>Current information is unavailable.</p>
                <button type="button" className="text-link" onClick={resource.reload}>
                    Try again
                </button>
            </div>
        );
    return <div className="service-summary">{children}</div>;
}
function ApplicationSummary() {
    const resource = useResource<StudentApplication[]>('/api/applications/me');
    const latest = resource.data
        ?.slice()
        .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))[0];
    return (
        <Summary resource={resource}>
            {latest ? (
                <>
                    <strong>Latest application: {statusLabel(latest.status)}</strong>
                    <p>
                        {latest.status === 'DRAFT'
                            ? 'Your application has not been submitted yet.'
                            : 'Open your application to review its current status and results.'}
                    </p>
                    <Link className="text-link" to={'/applications/' + latest.id}>
                        Open latest application
                    </Link>
                </>
            ) : (
                <p>You have not created an application yet.</p>
            )}
        </Summary>
    );
}
function AccommodationSummary() {
    const resource = useOptionalResource<MyAccommodation>('/api/accommodations/me', [
        'You do not have a current accommodation.',
    ]);
    const item = resource.data;
    return (
        <Summary resource={resource}>
            {item ? (
                <>
                    <strong>
                        {item.dorm.name} · Room {item.room.number}
                    </strong>
                    <p>
                        {item.status === 'ASSIGNED'
                            ? 'Room assigned — move-in has not been recorded yet.'
                            : statusLabel(item.status)}{' '}
                        · {item.academicYear}
                    </p>
                </>
            ) : (
                <p>No current room assignment.</p>
            )}
        </Summary>
    );
}
function MealSummary() {
    const now = new Date();
    const year = now.getUTCFullYear();
    const month = now.getUTCMonth() + 1;
    const resource = useResource<Entitlement[]>(
        `/api/meals/me/entitlements?year=${year}&month=${month}`,
    );
    const active = resource.data?.filter((item) => item.status === 1) || [];
    return (
        <Summary resource={resource}>
            <strong>
                {active.reduce((total, item) => total + item.remainingQuantity, 0)} meals remaining
            </strong>
            <p>
                {String(month).padStart(2, '0')}/{year} · active entitlements
            </p>
            {active.length ? (
                <ul className="meal-summary">
                    {mealTypes.map((label, index) => (
                        <li key={label}>
                            {label}:{' '}
                            {active
                                .filter((item) => item.mealType === index + 1)
                                .reduce((total, item) => total + item.remainingQuantity, 0)}
                        </li>
                    ))}
                </ul>
            ) : (
                <p>No active meal entitlement for this month.</p>
            )}
        </Summary>
    );
}
function BillingSummary() {
    const resource = useResource<Balance>('/api/billing/me/balance');
    return (
        <Summary resource={resource}>
            {resource.data && (
                <>
                    <strong>{money(resource.data.outstandingAmount)} outstanding</strong>
                    <p>
                        {resource.data.overdueAmount > 0
                            ? `${money(resource.data.overdueAmount)} overdue`
                            : 'No overdue balance.'}
                    </p>
                </>
            )}
        </Summary>
    );
}
export function StudentServiceSummary({ service }: { service: string }) {
    switch (service) {
        case 'applications':
            return <ApplicationSummary />;
        case 'housing':
            return <AccommodationSummary />;
        case 'meals':
            return <MealSummary />;
        case 'payments':
            return <BillingSummary />;
        default:
            return null;
    }
}
