import { useResource } from '../hooks/useResource';
import { useOptionalResource } from '../lib/results';
import type { MyAccommodation } from '../lib/accommodation';
import { dateTime } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function MyAccommodationPage() {
    const current = useOptionalResource<MyAccommodation>('/api/accommodations/me', [
        'You do not have a current accommodation.',
    ]);
    const history = useResource<MyAccommodation[]>('/api/accommodations/me/history');
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">STUDENT ACCOMMODATION</span>
                    <h1>My accommodation</h1>
                </div>
                <button
                    className="secondary"
                    disabled={current.loading || history.loading}
                    onClick={() => {
                        current.reload();
                        history.reload();
                    }}
                >
                    Refresh accommodation
                </button>
            </div>
            <section className="panel">
                <h2>Current accommodation</h2>
                {current.loading ? (
                    <p role="status">Loading accommodation…</p>
                ) : current.error ? (
                    <RequestError error={current.error} retry={current.reload} />
                ) : current.data ? (
                    <AccommodationDetails item={current.data} />
                ) : (
                    <p>
                        No room is currently assigned to you. Eligibility for accommodation is
                        followed by a separate room assignment.
                    </p>
                )}
            </section>
            <section className="panel application-section">
                <h2>Accommodation history</h2>
                {history.loading ? (
                    <p role="status">Loading history…</p>
                ) : history.error ? (
                    <RequestError error={history.error} retry={history.reload} />
                ) : !history.data?.length ? (
                    <p>No accommodation records found.</p>
                ) : (
                    history.data.map((item) => (
                        <article className="staff-document" key={item.id}>
                            <AccommodationDetails item={item} />
                        </article>
                    ))
                )}
            </section>
        </>
    );
}

function AccommodationDetails({ item }: { item: MyAccommodation }) {
    return (
        <>
            <StatusBadge status={item.status} />
            <h3>
                {item.dorm.name} · Room {item.room.number}
            </h3>
            <p>
                {item.dorm.address}, {item.dorm.city}
            </p>
            <p>
                Floor: {item.room.floor} · Academic year: {item.academicYear}
            </p>
            <p>Assigned: {dateTime(item.assignedAtUtc)}</p>
            {item.status === 'ASSIGNED' && (
                <p className="muted">A bed has been reserved. Move-in has not yet been recorded.</p>
            )}
            {item.movedInAtUtc && <p>Moved in: {dateTime(item.movedInAtUtc)}</p>}
            {item.movedOutAtUtc && <p>Moved out: {dateTime(item.movedOutAtUtc)}</p>}
            {item.cancelledAtUtc && <p>Cancelled: {dateTime(item.cancelledAtUtc)}</p>}
        </>
    );
}
