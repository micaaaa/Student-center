import { useState } from 'react';
import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime } from '../lib/applications';
import type { MaintenanceRequest, MaintenanceWorker, MaintenanceAction } from '../lib/maintenance';
import { RequestError } from './ApplicationUi';
import { ConfirmationDialog } from './ConfirmationDialog';

export function MaintenanceActions({ id, student = false }: { id: string; student?: boolean }) {
    const [page, setPage] = useState(1);
    const resource = useResource<MaintenanceAction[]>(
        `/api/maintenance/${student ? 'requests/me' : 'work/requests'}/${id}/actions?page=${page}&pageSize=20`,
    );
    return (
        <section className="panel application-section">
            <h2>Work history</h2>
            {resource.loading ? (
                <p role="status">Loading work history…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p>No work actions on this page.</p>
            ) : (
                resource.data.map((item) => (
                    <article className="staff-document" key={item.id}>
                        <h3>
                            {
                                ['', 'Assigned', 'Work started', 'Intervention', 'Resolved'][
                                    item.type
                                ]
                            }
                        </h3>
                        <p className="preserve-lines">{item.description}</p>
                        <p className="muted">{dateTime(item.createdAtUtc)}</p>
                    </article>
                ))
            )}
            <div className="button-row">
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
        </section>
    );
}

export function WorkerAssignment({
    item,
    update,
}: {
    item: MaintenanceRequest;
    update: (item: MaintenanceRequest) => void;
}) {
    const [page, setPage] = useState(1);
    const workers = useResource<MaintenanceWorker[]>(
        `/api/maintenance/workers?page=${page}&pageSize=20`,
    );
    const [workerId, setWorkerId] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [confirm, setConfirm] = useState(false);
    const selected = workers.data?.find(
        (w) => w.id === workerId && w.isActive && w.id !== item.assignedWorkerId,
    );
    async function assign() {
        if (busy || !selected) return;
        setBusy(true);
        setError('');
        try {
            update(
                await api<MaintenanceRequest>(`/api/maintenance/requests/${item.id}/assign`, {
                    method: 'POST',
                    body: JSON.stringify({ workerId }),
                }),
            );
            setWorkerId('');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setConfirm(false);
        }
    }
    return (
        <section className="panel application-section">
            <h2>Assign worker</h2>
            {item.assignedWorkerId && (
                <p className="record-reference">
                    Current worker reference: {item.assignedWorkerId}
                </p>
            )}
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            {workers.loading ? (
                <p role="status">Loading workers…</p>
            ) : workers.error ? (
                <RequestError error={workers.error} retry={workers.reload} />
            ) : (
                <>
                    <label>
                        Worker
                        <select
                            disabled={busy}
                            value={workerId}
                            onChange={(e) => setWorkerId(e.target.value)}
                        >
                            <option value="">Select an active worker</option>
                            {workers.data
                                ?.filter((w) => w.isActive && w.id !== item.assignedWorkerId)
                                .map((w) => (
                                    <option key={w.id} value={w.id}>
                                        {w.name} — {w.specialization}
                                    </option>
                                ))}
                        </select>
                    </label>
                    <div className="button-row application-section">
                        <button
                            className="secondary"
                            disabled={busy || page === 1}
                            onClick={() => {
                                setWorkerId('');
                                setPage(page - 1);
                            }}
                        >
                            Previous workers
                        </button>
                        <span>Page {page}</span>
                        <button
                            className="secondary"
                            disabled={busy || workers.data?.length !== 20 || page >= 107374182}
                            onClick={() => {
                                setWorkerId('');
                                setPage(page + 1);
                            }}
                        >
                            Next workers
                        </button>
                        <button
                            className="primary"
                            disabled={busy || !selected}
                            onClick={() => setConfirm(true)}
                        >
                            Assign worker
                        </button>
                    </div>
                    <Link className="text-link" to="/staff/maintenance/workers">
                        Manage workers
                    </Link>
                </>
            )}
            {confirm && (
                <ConfirmationDialog
                    title="Confirm worker assignment"
                    busy={busy}
                    onClose={() => setConfirm(false)}
                    onConfirm={assign}
                >
                    <p>Assign this request to {selected?.name}?</p>
                    {item.status === 6 && (
                        <p>
                            Reassignment returns the request to Assigned. The new worker must start
                            work again.
                        </p>
                    )}
                </ConfirmationDialog>
            )}
        </section>
    );
}

export function WorkControls({
    item,
    update,
    disabled = false,
}: {
    item: MaintenanceRequest;
    update: (item: MaintenanceRequest) => void;
    disabled?: boolean;
}) {
    const [description, setDescription] = useState('');
    const [operation, setOperation] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function save() {
        if (
            busy ||
            disabled ||
            !(
                (item.status === 5 && operation === 'start') ||
                (item.status === 6 &&
                    ['actions', 'resolve'].includes(operation) &&
                    description.trim())
            )
        )
            return;
        setBusy(true);
        setError('');
        try {
            update(
                await api<MaintenanceRequest>(
                    `/api/maintenance/work/requests/${item.id}/${operation}`,
                    {
                        method: 'POST',
                        ...(operation === 'start'
                            ? {}
                            : { body: JSON.stringify({ description: description.trim() }) }),
                    },
                ),
            );
            setDescription('');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setOperation('');
        }
    }
    return (
        <section className="panel application-section">
            <h2>Work progress</h2>
            {disabled && (
                <p className="notice">
                    Your worker profile is inactive. Work actions are unavailable.
                </p>
            )}
            {error && (
                <p className="notice error" role="alert">
                    {error} Refresh the request before retrying.
                </p>
            )}
            {item.status === 5 && (
                <button
                    className="primary"
                    disabled={busy || disabled}
                    onClick={() => setOperation('start')}
                >
                    Start work
                </button>
            )}
            {item.status === 6 && (
                <>
                    <label>
                        Intervention or resolution description
                        <textarea
                            rows={4}
                            maxLength={4000}
                            value={description}
                            disabled={busy || disabled}
                            onChange={(e) => setDescription(e.target.value)}
                        />
                    </label>
                    <div className="button-row">
                        <button
                            className="secondary"
                            disabled={busy || disabled || !description.trim()}
                            onClick={() => setOperation('actions')}
                        >
                            Record intervention
                        </button>
                        <button
                            className="primary"
                            disabled={busy || disabled || !description.trim()}
                            onClick={() => setOperation('resolve')}
                        >
                            Resolve request
                        </button>
                    </div>
                </>
            )}
            {operation && (
                <ConfirmationDialog
                    title={
                        operation === 'start'
                            ? 'Start work'
                            : operation === 'resolve'
                              ? 'Resolve request'
                              : 'Record intervention'
                    }
                    busy={busy}
                    onClose={() => setOperation('')}
                    onConfirm={save}
                >
                    <p>
                        {operation === 'start'
                            ? 'Confirm that work on this request is starting.'
                            : description}
                    </p>
                    {operation === 'resolve' && <p>This closes the request.</p>}
                </ConfirmationDialog>
            )}
        </section>
    );
}
