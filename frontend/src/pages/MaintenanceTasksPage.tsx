import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { useAuth } from '../auth/AuthContext';
import { useOptionalResource } from '../lib/results';
import type { MaintenanceWorker, MaintenanceRequest } from '../lib/maintenance';
import { requestStatuses, priorities } from '../lib/maintenance';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { WorkControls, MaintenanceActions } from '../components/MaintenanceWork';

export function MaintenanceTasksPage() {
    const worker = useOptionalResource<MaintenanceWorker>('/api/maintenance/work/me', [
        'Worker profile not found.',
    ]);
    if (worker.loading) return <p role="status">Loading worker profile…</p>;
    if (worker.error) return <RequestError error={worker.error} retry={worker.reload} />;
    if (!worker.data)
        return (
            <section className="panel">
                <h1>My maintenance tasks</h1>
                <p>
                    No worker profile is linked to your account. A maintenance supervisor must
                    create one before assigning tasks.
                </p>
            </section>
        );
    return <Tasks worker={worker.data} />;
}
function Tasks({ worker }: { worker: MaintenanceWorker }) {
    const [page, setPage] = useState(1);
    const [status, setStatus] = useState('');
    const resource = useResource<MaintenanceRequest[]>(
        `/api/maintenance/work/requests?page=${page}&pageSize=20${status ? '&status=' + status : ''}`,
    );
    return (
        <>
            <div className="page-heading">
                <h1>My maintenance tasks</h1>
                <p>
                    {worker.name} · {worker.specialization}
                </p>
            </div>
            {!worker.isActive && (
                <p className="notice">
                    Your worker profile is inactive. Existing tasks remain visible.
                </p>
            )}
            <label>
                Task status
                <select
                    value={status}
                    onChange={(e) => {
                        setStatus(e.target.value);
                        setPage(1);
                    }}
                >
                    <option value="">All statuses</option>
                    {[5, 6, 7].map((value) => (
                        <option key={value} value={value}>
                            {requestStatuses[value - 1].replaceAll('_', ' ')}
                        </option>
                    ))}
                </select>
            </label>
            {resource.loading ? (
                <p role="status">Loading tasks…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p className="panel application-section">No assigned tasks on this page.</p>
            ) : (
                <div className="competition-list application-section">
                    {resource.data.map((item) => (
                        <article className="panel" key={item.id}>
                            <h2>{item.title}</h2>
                            <StatusBadge status={requestStatuses[item.status - 1]} />
                            <p>Priority: {priorities[item.priority - 1]}</p>
                            <Link className="secondary" to={'/maintenance-work/' + item.id}>
                                View task
                            </Link>
                        </article>
                    ))}
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={page === 1 || resource.loading}
                    onClick={() => setPage(page - 1)}
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
                    onClick={() => setPage(page + 1)}
                >
                    Next
                </button>
            </div>
        </>
    );
}
export function MaintenanceTaskPage() {
    const { id } = useParams();
    const { user } = useAuth();
    const resource = useResource<MaintenanceRequest>('/api/maintenance/work/requests/' + id);
    const worker = useOptionalResource<MaintenanceWorker>('/api/maintenance/work/me', [
        'Worker profile not found.',
    ]);
    if (resource.loading || worker.loading) return <p role="status">Loading task…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    if (worker.error) return <RequestError error={worker.error} retry={worker.reload} />;
    const item = resource.data!;
    const canWork =
        user?.permissions.includes('ManageMaintenance') ||
        (worker.data?.isActive && worker.data.id === item.assignedWorkerId);
    return (
        <>
            <div className="page-heading heading-row">
                <h1>{item.title}</h1>
                <button
                    className="secondary"
                    onClick={() => {
                        resource.reload();
                        worker.reload();
                    }}
                >
                    Refresh task
                </button>
            </div>
            <section className="panel">
                <StatusBadge status={requestStatuses[item.status - 1]} />
                <p className="preserve-lines">{item.description}</p>
                <p>Priority: {priorities[item.priority - 1]}</p>
                <p className="record-reference">Room reference: {item.roomId}</p>
                {item.resolutionDescription && (
                    <p className="preserve-lines">Resolution: {item.resolutionDescription}</p>
                )}
            </section>
            {[5, 6].includes(item.status) && (
                <WorkControls
                    key={item.id + item.status}
                    item={item}
                    update={resource.setData}
                    disabled={!canWork}
                />
            )}
            <MaintenanceActions key={item.updatedAtUtc} id={item.id} />
        </>
    );
}
