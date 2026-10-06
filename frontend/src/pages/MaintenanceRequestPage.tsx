import { useState } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime } from '../lib/applications';
import { priorities, requestStatuses } from '../lib/maintenance';
import type { MaintenanceRequest } from '../lib/maintenance';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { WorkerAssignment, WorkControls, MaintenanceActions } from '../components/MaintenanceWork';

export function MaintenanceRequestPage({ management = false }: { management?: boolean }) {
    const { id } = useParams();
    const resource = useResource<MaintenanceRequest>(
        '/api/maintenance/requests/' + (management ? '' : 'me/') + id,
    );
    if (resource.loading) return <p role="status">Loading request…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <RequestRecord
            key={id + String(management)}
            item={resource.data!}
            management={management}
            update={resource.setData}
            reload={resource.reload}
        />
    );
}
function RequestRecord({
    item,
    management,
    update,
    reload,
}: {
    item: MaintenanceRequest;
    management: boolean;
    update: (item: MaintenanceRequest) => void;
    reload: () => void;
}) {
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [action, setAction] = useState('');
    const [reason, setReason] = useState('');
    const [priority, setPriority] = useState(item.priority);
    const closed = [3, 4, 7].includes(item.status);
    async function save(operation: string) {
        if (busy) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            const saved = await api<MaintenanceRequest>(
                '/api/maintenance/requests/' +
                    (management ? '' : 'me/') +
                    item.id +
                    '/' +
                    operation,
                {
                    method: operation === 'priority' ? 'PUT' : 'POST',
                    ...(operation === 'priority'
                        ? { body: JSON.stringify({ priority }) }
                        : operation === 'reject'
                          ? { body: JSON.stringify({ reason: reason.trim() }) }
                          : {}),
                },
            );
            update(saved);
            setPriority(saved.priority);
            setSuccess('Request updated.');
            setReason('');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setAction('');
        }
    }
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <h1>{item.title}</h1>
                    <StatusBadge status={requestStatuses[item.status - 1]} />
                </div>
                <button className="secondary" disabled={busy} onClick={reload}>
                    Refresh request
                </button>
            </div>
            {error && (
                <p className="notice error" role="alert">
                    {error} Refresh the request before retrying.
                </p>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            <section className="panel">
                <h2>Request details</h2>
                <p className="preserve-lines">{item.description}</p>
                <p>Priority: {priorities[item.priority - 1]}</p>
                <p>Submitted: {dateTime(item.createdAtUtc)}</p>
                <p>Last updated: {dateTime(item.updatedAtUtc)}</p>
                {management && (
                    <>
                        <p className="record-reference">Student reference: {item.studentId}</p>
                        <p className="record-reference">Room reference: {item.roomId}</p>
                    </>
                )}
                {item.rejectionReason && (
                    <>
                        <h3>Reason for rejection</h3>
                        <p className="preserve-lines">{item.rejectionReason}</p>
                    </>
                )}
                {item.resolutionDescription && (
                    <>
                        <h3>Resolution</h3>
                        <p className="preserve-lines">{item.resolutionDescription}</p>
                        <p>{dateTime(item.resolvedAtUtc)}</p>
                    </>
                )}
                {item.cancelledAtUtc && <p>Cancelled: {dateTime(item.cancelledAtUtc)}</p>}
            </section>
            {management && !closed && (
                <section className="panel application-section">
                    <h2>Request priority</h2>
                    <label>
                        Priority
                        <select
                            disabled={busy}
                            value={priority}
                            onChange={(e) => setPriority(Number(e.target.value))}
                        >
                            {priorities.map((p, i) => (
                                <option key={p} value={i + 1}>
                                    {p}
                                </option>
                            ))}
                        </select>
                    </label>
                    <button
                        className="secondary application-section"
                        disabled={busy || priority === item.priority}
                        onClick={() => save('priority')}
                    >
                        Save priority
                    </button>
                </section>
            )}
            {item.status === 1 && (
                <section className="panel application-section">
                    <h2>{management ? 'Review request' : 'Cancel request'}</h2>
                    {management ? (
                        <>
                            <button
                                className="primary"
                                disabled={busy}
                                onClick={() => setAction('accept')}
                            >
                                Accept request
                            </button>
                            <label className="application-section">
                                Reason for rejection
                                <textarea
                                    maxLength={2000}
                                    rows={3}
                                    disabled={busy}
                                    value={reason}
                                    onChange={(e) => setReason(e.target.value)}
                                />
                            </label>
                            <button
                                className="secondary"
                                disabled={busy || !reason.trim()}
                                onClick={() => setAction('reject')}
                            >
                                Reject request
                            </button>
                        </>
                    ) : (
                        <>
                            <p>A request can be cancelled only before it is reviewed.</p>
                            <button
                                className="secondary"
                                disabled={busy}
                                onClick={() => setAction('cancel')}
                            >
                                Cancel request
                            </button>
                        </>
                    )}
                </section>
            )}
            {action && (
                <ConfirmationDialog
                    title={
                        action === 'accept'
                            ? 'Accept request'
                            : action === 'reject'
                              ? 'Reject request'
                              : 'Cancel request'
                    }
                    busy={busy}
                    onClose={() => setAction('')}
                    onConfirm={() => save(action)}
                >
                    <p>Confirm this action for “{item.title}”?</p>
                    {action === 'reject' && <p className="preserve-lines">{reason}</p>}
                </ConfirmationDialog>
            )}
            {management && [2, 5, 6].includes(item.status) && (
                <WorkerAssignment
                    key={item.id + String(item.assignedWorkerId)}
                    item={item}
                    update={update}
                />
            )}
            {management && [5, 6].includes(item.status) && (
                <WorkControls key={item.id + item.status} item={item} update={update} />
            )}
            <MaintenanceActions key={item.updatedAtUtc} id={item.id} student={!management} />
        </>
    );
}
