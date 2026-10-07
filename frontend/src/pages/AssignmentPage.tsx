import { StudentIdentity } from '../components/StudentIdentity';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Assignment, Dorm, Room } from '../lib/accommodation';
import { dateTime } from '../lib/applications';
import { api, errorMessage } from '../lib/api';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function AssignmentPage() {
    const { id } = useParams();
    const record = useResource<Assignment>('/api/accommodations/' + id);
    if (record.loading) return <p role="status">Loading accommodation…</p>;
    if (record.error) return <RequestError error={record.error} retry={record.reload} />;
    return (
        <AssignmentRecord
            key={id}
            item={record.data!}
            update={record.setData}
            reload={record.reload}
        />
    );
}

function AssignmentRecord({
    item,
    update,
    reload,
}: {
    item: Assignment;
    update: (value: Assignment) => void;
    reload: () => void;
}) {
    const room = useResource<Room>('/api/rooms/' + item.roomId);
    const dorms = useResource<Dorm[]>('/api/dorms');
    const [action, setAction] = useState('');
    const [text, setText] = useState('');
    const [confirm, setConfirm] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const labels: Record<string, string> = {
        'move-in': 'Record move-in',
        'move-out': 'Record move-out',
        cancel: 'Cancel assignment',
    };
    const allowed =
        item.status === 'ASSIGNED'
            ? ['move-in', 'cancel']
            : item.status === 'ACTIVE'
              ? ['move-out']
              : [];
    function submit(event: FormEvent) {
        event.preventDefault();
        if (!busy && allowed.includes(action) && text.trim()) setConfirm(true);
    }
    async function save() {
        if (busy || !allowed.includes(action) || !text.trim()) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            update(
                await api<Assignment>(`/api/accommodations/${item.id}/${action}`, {
                    method: 'POST',
                    body: JSON.stringify(
                        action === 'move-in'
                            ? { medicalCertificateReference: text.trim() }
                            : { reason: text.trim() },
                    ),
                }),
            );
            setAction('');
            setText('');
            setSuccess(
                action === 'move-in'
                    ? 'Move-in recorded. Accommodation is now active and the student can report room problems when maintenance categories are available.'
                    : 'Accommodation record updated.',
            );
            room.reload();
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setConfirm(false);
        }
    }
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">ACCOMMODATION RECORD</span>
                    <h1>Manage accommodation</h1>
                    <StatusBadge status={item.status} />
                </div>
                <button className="secondary" disabled={busy} onClick={reload}>
                    Refresh record
                </button>
            </div>
            {error && (
                <p className="notice error" role="alert">
                    {error} Refresh the record before retrying.
                </p>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            {item.status === 'ASSIGNED' && (
                <section className="notice">
                    <h2>Next step: record move-in</h2>
                    <p>
                        The bed is reserved. When the student arrives, verify the medical
                        certificate and record the move-in to activate accommodation.
                    </p>
                    <button
                        className="primary"
                        disabled={busy}
                        onClick={() => {
                            setAction('move-in');
                            setText('');
                            document
                                .getElementById('accommodation-action')
                                ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        }}
                    >
                        Prepare move-in
                    </button>
                </section>
            )}
            <section className="panel">
                <h2>Assignment details</h2>
                <p>
                    <StudentIdentity id={item.studentId} />
                </p>
                <p>Academic year: {item.academicYear}</p>
                <p>Assigned: {dateTime(item.assignedAtUtc)}</p>
                {room.loading || dorms.loading ? (
                    <p role="status">Loading room details…</p>
                ) : room.error ? (
                    <RequestError error={room.error} retry={room.reload} />
                ) : dorms.error ? (
                    <RequestError error={dorms.error} retry={dorms.reload} />
                ) : (
                    <p>
                        {dorms.data?.find((dorm) => dorm.id === room.data?.dormId)?.name ||
                            'Dormitory'}{' '}
                        · Room {room.data?.roomNumber} · Floor {room.data?.floor}
                    </p>
                )}
                {item.moveIn && (
                    <>
                        <h3>Move-in</h3>
                        <p>{dateTime(item.moveIn.dateUtc)}</p>
                        <p className="record-reference">
                            Medical certificate reference: {item.moveIn.medicalCertificateReference}
                        </p>
                    </>
                )}
                {item.moveOut && (
                    <>
                        <h3>Move-out</h3>
                        <p>{dateTime(item.moveOut.dateUtc)}</p>
                        <p className="preserve-lines">{item.moveOut.reason}</p>
                    </>
                )}
                {item.cancelledAtUtc && (
                    <>
                        <h3>Cancellation</h3>
                        <p>{dateTime(item.cancelledAtUtc)}</p>
                        <p className="preserve-lines">{item.cancellationReason}</p>
                    </>
                )}
            </section>
            {!!allowed.length && (
                <section id="accommodation-action" className="panel application-section">
                    <h2>Record an action</h2>
                    <form onSubmit={submit}>
                        <label>
                            Accommodation action
                            <select
                                value={action}
                                disabled={busy}
                                required
                                onChange={(event) => {
                                    setAction(event.target.value);
                                    setText('');
                                }}
                            >
                                <option value="">Select an action</option>
                                {allowed.map((value) => (
                                    <option key={value} value={value}>
                                        {labels[value]}
                                    </option>
                                ))}
                            </select>
                        </label>
                        {action && (
                            <>
                                <label>
                                    {action === 'move-in'
                                        ? 'Medical certificate reference'
                                        : 'Reason'}
                                    <textarea
                                        required
                                        rows={3}
                                        maxLength={action === 'move-in' ? 250 : 1000}
                                        value={text}
                                        disabled={busy}
                                        onChange={(event) => setText(event.target.value)}
                                    />
                                </label>
                                <p className="muted">
                                    {action === 'move-in'
                                        ? 'Enter the reference to the verified medical certificate. This form does not upload the certificate.'
                                        : 'This action releases the reserved bed and cannot be undone.'}
                                </p>
                                <button className="primary" disabled={busy || !text.trim()}>
                                    {labels[action]}
                                </button>
                            </>
                        )}
                    </form>
                </section>
            )}
            {confirm && (
                <ConfirmationDialog
                    title={labels[action]}
                    busy={busy}
                    onClose={() => setConfirm(false)}
                    onConfirm={save}
                >
                    <p>
                        Confirm this action for <StudentIdentity id={item.studentId} />?
                    </p>
                    {action === 'move-in' && (
                        <p>
                            This activates the student's accommodation. Record it only after the
                            student has arrived and the medical certificate has been verified.
                        </p>
                    )}
                    <p className="preserve-lines">{text}</p>
                    {action !== 'move-in' && <p>The bed will become available again.</p>}
                </ConfirmationDialog>
            )}
        </>
    );
}
