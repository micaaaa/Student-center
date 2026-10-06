import { Link, useSearchParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { dateTime, statusLabel } from '../lib/applications';
import { priorities, requestStatuses } from '../lib/maintenance';
import type { MaintenanceRequest } from '../lib/maintenance';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function MaintenanceRequestsPage({ management = false }: { management?: boolean }) {
    const [search, setSearch] = useSearchParams();
    const rawPage = Number(search.get('page') || 1);
    const page = Number.isInteger(rawPage) && rawPage > 0 && rawPage <= 107374182 ? rawPage : 1;
    const status = /^[1-7]$/.test(search.get('status') || '') ? search.get('status')! : '';
    const query = new URLSearchParams({ page: String(page), pageSize: '20' });
    if (status) query.set('status', status);
    const resource = useResource<MaintenanceRequest[]>(
        '/api/maintenance/requests' + (management ? '' : '/me') + '?' + query,
    );
    const base = management ? '/staff/maintenance' : '/maintenance';
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">MAINTENANCE SERVICES</span>
                    <h1>{management ? 'Maintenance requests' : 'My maintenance requests'}</h1>
                </div>
                {management ? (
                    <Link className="secondary" to="/staff/maintenance/categories">
                        Manage categories
                    </Link>
                ) : (
                    <Link className="primary" to="/maintenance/new">
                        Report a problem
                    </Link>
                )}
            </div>
            <div className="list-toolbar">
                <label>
                    Request status
                    <select
                        value={status}
                        onChange={(e) =>
                            setSearch(e.target.value ? { status: e.target.value } : {})
                        }
                    >
                        <option value="">All statuses</option>
                        {requestStatuses.map((value, i) => (
                            <option key={value} value={i + 1}>
                                {statusLabel(value)}
                            </option>
                        ))}
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading requests…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <section className="panel">No requests found on this page.</section>
            ) : (
                <div className="competition-list">
                    {resource.data.map((item) => (
                        <article className="panel competition-card" key={item.id}>
                            <StatusBadge status={requestStatuses[item.status - 1]} />
                            <h2>{item.title}</h2>
                            <p>Priority: {priorities[item.priority - 1]}</p>
                            <p className="muted">Submitted: {dateTime(item.createdAtUtc)}</p>
                            <Link className="secondary" to={base + '/' + item.id}>
                                View request
                            </Link>
                        </article>
                    ))}
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={page === 1 || resource.loading}
                    onClick={() => {
                        query.set('page', String(page - 1));
                        setSearch(query);
                    }}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        resource.loading ||
                        !!resource.error ||
                        resource.data?.length !== 20 ||
                        page >= 107374182
                    }
                    onClick={() => {
                        query.set('page', String(page + 1));
                        setSearch(query);
                    }}
                >
                    Next
                </button>
            </div>
        </>
    );
}
