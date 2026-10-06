import { useState } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Dorm, Room } from '../lib/accommodation';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { InventoryForm } from '../components/InventoryForm';

export function DormPage() {
    const { id } = useParams();
    const dorm = useResource<Dorm>('/api/dorms/' + id);
    if (dorm.loading) return <p role="status">Loading dormitory…</p>;
    if (dorm.error) return <RequestError error={dorm.error} retry={dorm.reload} />;
    return <DormRecord key={id} dorm={dorm.data!} update={dorm.setData} reload={dorm.reload} />;
}

function DormRecord({
    dorm,
    update,
    reload,
}: {
    dorm: Dorm;
    update: (value: Dorm) => void;
    reload: () => void;
}) {
    const rooms = useResource<Room[]>('/api/dorms/' + dorm.id + '/rooms');
    const [editing, setEditing] = useState<'dorm' | 'new' | Room | null>(null);
    const [success, setSuccess] = useState('');
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const visible = rooms.data?.filter(
        (item) =>
            (!status || item.status === status) &&
            item.roomNumber.toLowerCase().includes(search.trim().toLowerCase()),
    );
    const total = rooms.data?.reduce((sum, item) => sum + item.capacity, 0) ?? 0;
    const occupied = rooms.data?.reduce((sum, item) => sum + item.occupiedBeds, 0) ?? 0;
    const available =
        dorm.status === 'ACTIVE'
            ? (rooms.data
                  ?.filter((item) => item.status === 'AVAILABLE')
                  .reduce((sum, item) => sum + Math.max(0, item.capacity - item.occupiedBeds), 0) ??
              0)
            : 0;
    function savedRoom(item: Room) {
        rooms.setData(
            [...(rooms.data || []).filter((room) => room.id !== item.id), item].sort((a, b) =>
                a.roomNumber.localeCompare(b.roomNumber, undefined, { numeric: true }),
            ),
        );
        setEditing(null);
        setSuccess('Room saved.');
    }
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">ACCOMMODATION INVENTORY</span>
                    <h1>{dorm.name}</h1>
                    <StatusBadge status={dorm.status} />
                </div>
                <button className="secondary" disabled={!!editing} onClick={reload}>
                    Refresh inventory
                </button>
            </div>
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            <section className="panel">
                <h2>Dormitory details</h2>
                <p>
                    {dorm.address}, {dorm.city}
                </p>
                <p>Category: {dorm.category}</p>
                <p>
                    Dormitory capacity: <strong>{dorm.capacity}</strong>
                </p>
                <button
                    className="secondary"
                    disabled={!!editing}
                    onClick={() => {
                        setEditing('dorm');
                        setSuccess('');
                    }}
                >
                    Edit dormitory
                </button>
            </section>
            {editing === 'dorm' && (
                <InventoryForm
                    kind="dorm"
                    record={dorm}
                    cancel={() => setEditing(null)}
                    saved={(item) => {
                        update(item);
                        setEditing(null);
                        setSuccess('Dormitory saved.');
                    }}
                />
            )}
            <section className="panel application-section">
                <div className="heading-row">
                    <h2>Rooms</h2>
                    <button
                        className="primary"
                        disabled={
                            !!editing || dorm.status !== 'ACTIVE' || rooms.loading || !!rooms.error
                        }
                        onClick={() => {
                            setEditing('new');
                            setSuccess('');
                        }}
                    >
                        Add room
                    </button>
                </div>
                {dorm.status !== 'ACTIVE' && (
                    <p className="notice">
                        Activate this dormitory before adding rooms or making rooms available.
                    </p>
                )}
                {rooms.loading ? (
                    <p role="status">Loading rooms…</p>
                ) : rooms.error ? (
                    <RequestError error={rooms.error} retry={rooms.reload} />
                ) : (
                    <>
                        <dl className="details-grid">
                            <div>
                                <dt>Configured beds</dt>
                                <dd>{total}</dd>
                            </div>
                            <div>
                                <dt>Occupied beds</dt>
                                <dd>{occupied}</dd>
                            </div>
                            <div>
                                <dt>Available for allocation</dt>
                                <dd>{available}</dd>
                            </div>
                            <div>
                                <dt>Capacity not yet assigned to rooms</dt>
                                <dd>{Math.max(0, dorm.capacity - total)}</dd>
                            </div>
                        </dl>
                        <p className="muted">
                            Available beds exclude inactive rooms, rooms under maintenance and
                            inactive dormitories.
                        </p>
                        <div className="list-toolbar">
                            <label>
                                Room number search
                                <input
                                    type="search"
                                    value={search}
                                    onChange={(event) => setSearch(event.target.value)}
                                />
                            </label>
                            <label>
                                Room status
                                <select
                                    value={status}
                                    onChange={(event) => setStatus(event.target.value)}
                                >
                                    <option value="">All statuses</option>
                                    <option value="AVAILABLE">Available</option>
                                    <option value="FULL">Full</option>
                                    <option value="MAINTENANCE">Maintenance</option>
                                    <option value="INACTIVE">Inactive</option>
                                </select>
                            </label>
                        </div>
                        {!visible?.length ? (
                            <p>No rooms match the selected filters.</p>
                        ) : (
                            <div
                                className="ranking-scroll"
                                role="region"
                                aria-label="Room inventory"
                                tabIndex={0}
                            >
                                <table className="ranking-table">
                                    <caption>Room capacity and occupancy</caption>
                                    <thead>
                                        <tr>
                                            <th scope="col">Room</th>
                                            <th scope="col">Floor</th>
                                            <th scope="col">Capacity</th>
                                            <th scope="col">Occupied</th>
                                            <th scope="col">Status</th>
                                            <th scope="col">Action</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {visible.map((item) => (
                                            <tr key={item.id}>
                                                <th scope="row">{item.roomNumber}</th>
                                                <td>{item.floor}</td>
                                                <td>{item.capacity}</td>
                                                <td>{item.occupiedBeds}</td>
                                                <td>
                                                    <StatusBadge status={item.status} />
                                                </td>
                                                <td>
                                                    <button
                                                        className="secondary"
                                                        disabled={!!editing}
                                                        aria-label={'Edit room ' + item.roomNumber}
                                                        onClick={() => {
                                                            setEditing(item);
                                                            setSuccess('');
                                                        }}
                                                    >
                                                        Edit
                                                    </button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </>
                )}
            </section>
            {editing && editing !== 'dorm' && (
                <InventoryForm
                    key={typeof editing === 'string' ? editing : editing.id}
                    kind="room"
                    dormId={dorm.id}
                    record={typeof editing === 'object' ? editing : undefined}
                    cancel={() => setEditing(null)}
                    saved={savedRoom}
                />
            )}
        </>
    );
}
