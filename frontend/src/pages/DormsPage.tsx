import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Dorm } from '../lib/accommodation';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { InventoryForm } from '../components/InventoryForm';

export function DormsPage() {
    const resource = useResource<Dorm[]>('/api/dorms');
    const [adding, setAdding] = useState(false);
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('');
    const navigate = useNavigate();
    const visible = resource.data?.filter(
        (item) =>
            (!status || item.status === status) &&
            `${item.name} ${item.city} ${item.address}`
                .toLowerCase()
                .includes(search.trim().toLowerCase()),
    );
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">ACCOMMODATION ADMINISTRATION</span>
                    <h1>Dormitories</h1>
                    <p className="muted">Manage dormitories, room capacity and occupancy.</p>
                </div>
                <button className="primary" disabled={adding} onClick={() => setAdding(true)}>
                    Add dormitory
                </button>
            </div>
            {adding && (
                <InventoryForm
                    kind="dorm"
                    cancel={() => setAdding(false)}
                    saved={(item) => navigate('/staff/dorms/' + item.id)}
                />
            )}
            <div className="list-toolbar application-section">
                <label>
                    Search dormitories
                    <input
                        type="search"
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                    />
                </label>
                <label>
                    Dormitory status
                    <select value={status} onChange={(event) => setStatus(event.target.value)}>
                        <option value="">All statuses</option>
                        <option value="ACTIVE">Active</option>
                        <option value="INACTIVE">Inactive</option>
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading dormitories…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !visible?.length ? (
                <section className="panel">No dormitories match the selected filters.</section>
            ) : (
                <div className="competition-list">
                    {visible.map((item) => (
                        <article className="panel competition-card" key={item.id}>
                            <StatusBadge status={item.status} />
                            <h2>{item.name}</h2>
                            <p>
                                {item.address}, {item.city}
                            </p>
                            <p>
                                Category: {item.category} · Capacity: {item.capacity}
                            </p>
                            <Link className="secondary" to={'/staff/dorms/' + item.id}>
                                View rooms and details
                            </Link>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
