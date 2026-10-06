import { StaffAccountPicker } from '../components/StaffAccountPicker';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import type { MaintenanceWorker } from '../lib/maintenance';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function MaintenanceWorkersPage() {
    const [page, setPage] = useState(1);
    const resource = useResource<MaintenanceWorker[]>(
        `/api/maintenance/workers?page=${page}&pageSize=20`,
    );
    const [editing, setEditing] = useState<MaintenanceWorker | 'new' | null>(null);
    return (
        <>
            <div className="page-heading heading-row">
                <h1>Maintenance workers</h1>
                <button className="primary" disabled={!!editing} onClick={() => setEditing('new')}>
                    Add worker
                </button>
            </div>
            {editing && (
                <WorkerForm
                    key={editing === 'new' ? 'new' : editing.id}
                    item={editing === 'new' ? undefined : editing}
                    cancel={() => setEditing(null)}
                    saved={() => {
                        setEditing(null);
                        resource.reload();
                    }}
                />
            )}
            {resource.loading ? (
                <p role="status">Loading workers…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p className="panel">No workers on this page.</p>
            ) : (
                <div className="competition-list application-section">
                    {resource.data.map((item) => (
                        <article className="panel" key={item.id}>
                            <h2>{item.name}</h2>
                            <StatusBadge status={item.isActive ? 'ACTIVE' : 'INACTIVE'} />
                            <p>{item.specialization}</p>

                            <button
                                className="secondary"
                                disabled={!!editing}
                                onClick={() => setEditing(item)}
                            >
                                Edit {item.name}
                            </button>
                        </article>
                    ))}
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={!!editing || resource.loading || page === 1}
                    onClick={() => setPage(page - 1)}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        !!editing ||
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
function WorkerForm({
    item,
    cancel,
    saved,
}: {
    item?: MaintenanceWorker;
    cancel: () => void;
    saved: () => void;
}) {
    const [userId, setUserId] = useState(item?.userId || '');
    const [accountName, setAccountName] = useState('');
    const [name, setName] = useState(item?.name || '');
    const [specialization, setSpecialization] = useState(item?.specialization || '');
    const [active, setActive] = useState(item?.isActive ?? true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        if (
            !name.trim() ||
            !specialization.trim() ||
            !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(userId.trim())
        ) {
            setError(
                'Select an active staff account and enter the worker name and specialization.',
            );
            return;
        }
        setBusy(true);
        setError('');
        try {
            await api('/api/maintenance/workers' + (item ? '/' + item.id : ''), {
                method: item ? 'PUT' : 'POST',
                body: JSON.stringify({
                    name: name.trim(),
                    specialization: specialization.trim(),
                    ...(item ? { isActive: active } : { userId: userId.trim() }),
                }),
            });
            saved();
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel">
            <h2>{item ? 'Edit worker' : 'Add worker'}</h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    {!item &&
                        (userId ? (
                            <div className="application-section">
                                <p>
                                    Selected account: <strong>{accountName}</strong>
                                </p>
                                <button
                                    type="button"
                                    className="secondary"
                                    onClick={() => {
                                        setUserId('');
                                        setAccountName('');
                                    }}
                                >
                                    Choose another account
                                </button>
                            </div>
                        ) : (
                            <StaffAccountPicker
                                select={(id, username) => {
                                    setUserId(id);
                                    setAccountName(username);
                                }}
                            />
                        ))}
                    <label>
                        Worker name
                        <input
                            required
                            maxLength={200}
                            value={name}
                            onChange={(e) => setName(e.target.value)}
                        />
                    </label>
                    <label>
                        Specialization
                        <input
                            required
                            maxLength={200}
                            value={specialization}
                            onChange={(e) => setSpecialization(e.target.value)}
                        />
                    </label>
                    {item && (
                        <label>
                            Status
                            <select
                                value={String(active)}
                                onChange={(e) => setActive(e.target.value === 'true')}
                            >
                                <option value="true">Active</option>
                                <option value="false">Inactive</option>
                            </select>
                        </label>
                    )}
                    <p className="muted">
                        The worker must have an existing active staff account. Open tasks must be
                        reassigned or resolved before deactivation.
                    </p>
                    <div className="button-row">
                        <button className="primary">Save worker</button>
                        <button type="button" className="secondary" onClick={cancel}>
                            Cancel
                        </button>
                    </div>
                </fieldset>
            </form>
        </section>
    );
}
