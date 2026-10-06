import { StudentIdentity, StudentOption } from '../components/StudentIdentity';
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Competition } from '../lib/applications';
import { dateTime } from '../lib/applications';
import type { Assignment, Dorm, ReceivedEligibility, Room } from '../lib/accommodation';
import { api, errorMessage } from '../lib/api';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function AssignmentsPage() {
    const competitions = useResource<Competition[]>('/api/competitions');
    const [search, setSearch] = useSearchParams();
    const id = search.get('competitionId') || '';
    const studentId = search.get('studentId') || '';
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">ACCOMMODATION ADMINISTRATION</span>
                <h1>Room assignments</h1>
                <p className="muted">
                    Assign rooms to eligible students and manage their accommodation records.
                </p>
            </div>
            {studentId && (
                <p>
                    Assigning a room to <StudentIdentity id={studentId} />
                </p>
            )}
            {competitions.loading ? (
                <p role="status">Loading competitions…</p>
            ) : competitions.error ? (
                <RequestError error={competitions.error} retry={competitions.reload} />
            ) : (
                <label>
                    Competition
                    <select
                        value={id}
                        onChange={(event) =>
                            setSearch({
                                ...(studentId ? { studentId } : {}),
                                ...(event.target.value
                                    ? { competitionId: event.target.value }
                                    : {}),
                            })
                        }
                    >
                        <option value="">Select a competition</option>
                        {competitions.data?.map((item) => (
                            <option key={item.id} value={item.id}>
                                {item.name} — {item.academicYear}
                            </option>
                        ))}
                    </select>
                </label>
            )}
            {id && (
                <EligibilityList key={id + studentId} competitionId={id} studentId={studentId} />
            )}
        </>
    );
}

function EligibilityList({
    competitionId,
    studentId,
}: {
    competitionId: string;
    studentId: string;
}) {
    const eligibility = useResource<ReceivedEligibility[]>(
        '/api/competitions/' + competitionId + '/received-eligibilities',
    );
    const [selectedId, setSelectedId] = useState('');
    const options = eligibility.data?.filter((item) => !studentId || item.studentId === studentId);
    const selected =
        options?.find((item) => item.id === selectedId) || (studentId ? options?.[0] : undefined);
    if (eligibility.loading) return <p role="status">Loading eligible students…</p>;
    if (eligibility.error)
        return <RequestError error={eligibility.error} retry={eligibility.reload} />;
    return (
        <section className="application-section">
            <div className="heading-row">
                <h2>Received eligibility decisions</h2>
                <button className="secondary" onClick={eligibility.reload}>
                    Refresh decisions
                </button>
            </div>
            {!options?.length ? (
                <p className="panel">
                    No eligibility decisions have been received for this selection. Decisions become
                    available after final ranking publication and transfer to the accommodation
                    service.
                </p>
            ) : (
                <label>
                    Eligible student
                    <select
                        value={selected?.id || ''}
                        onChange={(event) => setSelectedId(event.target.value)}
                    >
                        <option value="">Select a student</option>
                        {options.map((item) => (
                            <StudentOption
                                key={item.id}
                                value={item.id}
                                id={item.studentId}
                                year={item.academicYear}
                            />
                        ))}
                    </select>
                </label>
            )}
            {selected && <StudentAssignments key={selected.id} eligibility={selected} />}
        </section>
    );
}

function StudentAssignments({ eligibility }: { eligibility: ReceivedEligibility }) {
    const history = useResource<Assignment[]>(
        '/api/students/' + eligibility.studentId + '/accommodations',
    );
    return (
        <>
            <p>
                <StudentIdentity id={eligibility.studentId} />
            </p>
            <p className="muted">Eligibility received: {dateTime(eligibility.grantedAtUtc)}</p>
            {history.loading ? (
                <p role="status">Loading accommodation records…</p>
            ) : history.error ? (
                <RequestError error={history.error} retry={history.reload} />
            ) : (
                <>
                    {history.data?.some((item) => ['ASSIGNED', 'ACTIVE'].includes(item.status)) ? (
                        <p className="notice">
                            This student already has a current accommodation. Open the record to
                            manage move-in, cancellation or move-out.
                        </p>
                    ) : (
                        <AssignRoom eligibility={eligibility} saved={history.reload} />
                    )}
                    <section className="panel application-section">
                        <div className="heading-row">
                            <h2>Accommodation records</h2>
                            <button className="secondary" onClick={history.reload}>
                                Refresh records
                            </button>
                        </div>
                        {!history.data?.length ? (
                            <p>No accommodation records found.</p>
                        ) : (
                            history.data.map((item) => (
                                <article className="staff-document" key={item.id}>
                                    <StatusBadge status={item.status} />
                                    <p>Academic year: {item.academicYear}</p>
                                    <p>Assigned: {dateTime(item.assignedAtUtc)}</p>
                                    <Link
                                        className="secondary"
                                        to={'/staff/accommodations/' + item.id}
                                    >
                                        Manage accommodation
                                    </Link>
                                </article>
                            ))
                        )}
                    </section>
                </>
            )}
        </>
    );
}

function AssignRoom({
    eligibility,
    saved,
}: {
    eligibility: ReceivedEligibility;
    saved: () => void;
}) {
    const dorms = useResource<Dorm[]>('/api/dorms');
    const [dormId, setDormId] = useState('');
    return (
        <section className="panel application-section">
            <h2>Assign a room</h2>
            {dorms.loading ? (
                <p role="status">Loading dormitories…</p>
            ) : dorms.error ? (
                <RequestError error={dorms.error} retry={dorms.reload} />
            ) : !dorms.data?.some((item) => item.status === 'ACTIVE') ? (
                <p>No active dormitories are available.</p>
            ) : (
                <label>
                    Dormitory
                    <select value={dormId} onChange={(event) => setDormId(event.target.value)}>
                        <option value="">Select a dormitory</option>
                        {dorms.data
                            .filter((item) => item.status === 'ACTIVE')
                            .map((item) => (
                                <option key={item.id} value={item.id}>
                                    {item.name}
                                </option>
                            ))}
                    </select>
                </label>
            )}
            {dormId && (
                <RoomChoice key={dormId} dormId={dormId} eligibility={eligibility} saved={saved} />
            )}
        </section>
    );
}

function RoomChoice({
    dormId,
    eligibility,
    saved,
}: {
    dormId: string;
    eligibility: ReceivedEligibility;
    saved: () => void;
}) {
    const rooms = useResource<Room[]>('/api/dorms/' + dormId + '/rooms');
    const [roomId, setRoomId] = useState('');
    const [confirm, setConfirm] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const available = rooms.data?.filter(
        (item) => item.status === 'AVAILABLE' && item.occupiedBeds < item.capacity,
    );
    const selected = available?.find((item) => item.id === roomId);
    async function assign() {
        if (busy || !selected || rooms.loading || rooms.error) return;
        setBusy(true);
        setError('');
        try {
            await api<Assignment>('/api/accommodations', {
                method: 'POST',
                body: JSON.stringify({ eligibilityId: eligibility.id, roomId }),
            });
            saved();
        } catch (error) {
            setError(errorMessage(error));
            rooms.reload();
        } finally {
            setBusy(false);
            setConfirm(false);
        }
    }
    return (
        <div className="application-section">
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            {rooms.loading ? (
                <p role="status">Loading available rooms…</p>
            ) : rooms.error ? (
                <RequestError error={rooms.error} retry={rooms.reload} />
            ) : !available?.length ? (
                <p>No rooms have an available bed in this dormitory.</p>
            ) : (
                <>
                    <label>
                        Room
                        <select
                            value={roomId}
                            disabled={busy}
                            onChange={(event) => setRoomId(event.target.value)}
                        >
                            <option value="">Select a room</option>
                            {available.map((item) => (
                                <option key={item.id} value={item.id}>
                                    {item.roomNumber} — Floor {item.floor} —{' '}
                                    {item.capacity - item.occupiedBeds} available
                                </option>
                            ))}
                        </select>
                    </label>
                    <button
                        className="primary application-section"
                        disabled={busy || !selected}
                        onClick={() => setConfirm(true)}
                    >
                        Assign room
                    </button>
                </>
            )}
            {confirm && (
                <ConfirmationDialog
                    title="Confirm room assignment"
                    busy={busy}
                    onClose={() => setConfirm(false)}
                    onConfirm={assign}
                >
                    <p>
                        Reserve a bed in room {selected?.roomNumber} for student{' '}
                        <StudentIdentity id={eligibility.studentId} />, academic year{' '}
                        {eligibility.academicYear}?
                    </p>
                    <p>Move-in must be recorded separately.</p>
                </ConfirmationDialog>
            )}
        </div>
    );
}
