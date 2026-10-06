import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Assignment, Room, Dorm } from '../lib/accommodation';
import { RequestError, StatusBadge } from './ApplicationUi';

function RoomLabel({ id }: { id: string }) {
    const room = useResource<Room>('/api/rooms/' + id);
    if (room.loading) return <p>Loading room…</p>;
    if (room.error) return <RequestError error={room.error} retry={room.reload} />;
    return <DormLabel room={room.data!} />;
}
function DormLabel({ room }: { room: Room }) {
    const dorm = useResource<Dorm>('/api/dorms/' + room.dormId);
    if (dorm.error) return <RequestError error={dorm.error} retry={dorm.reload} />;
    return (
        <p>
            {dorm.data?.name || 'Loading dormitory…'} · Room {room.roomNumber}
        </p>
    );
}
export function StudentAccommodationRecords({ studentId }: { studentId: string }) {
    const records = useResource<Assignment[]>('/api/students/' + studentId + '/accommodations');
    return (
        <section className="panel application-section">
            <h2>Accommodation</h2>
            {records.loading ? (
                <p role="status">Loading accommodation…</p>
            ) : records.error ? (
                <RequestError error={records.error} retry={records.reload} />
            ) : (
                <>
                    {!records.data?.length && <p>No accommodation records.</p>}
                    {records.data?.map((item) => (
                        <article className="student-result" key={item.id}>
                            <div>
                                <strong>{item.academicYear}</strong>
                                <RoomLabel id={item.roomId} />
                                <StatusBadge status={item.status} />
                            </div>
                            <Link className="text-link" to={'/staff/accommodations/' + item.id}>
                                View accommodation
                            </Link>
                        </article>
                    ))}
                </>
            )}
            <Link className="text-link" to={'/staff/assignments?studentId=' + studentId}>
                Assign a room
            </Link>
        </section>
    );
}
