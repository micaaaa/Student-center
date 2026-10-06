import { useState } from 'react';
import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import { dateTime, statusLabel } from '../lib/applications';
import type { Competition } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function StaffCompetitionsPage() {
    const resource = useResource<Competition[]>('/api/competitions');
    const [status, setStatus] = useState('');
    const [search, setSearch] = useState('');
    const visible = resource.data?.filter(
        (item) =>
            (!status || item.status === status) &&
            `${item.name} ${item.academicYear}`.toLowerCase().includes(search.trim().toLowerCase()),
    );
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">COMPETITION ADMINISTRATION</span>
                    <h1>Competitions</h1>
                    <p className="muted">Create competitions and manage application periods.</p>
                </div>
                <Link className="primary" to="/staff/competitions/new">
                    Create competition
                </Link>
            </div>
            <div className="list-toolbar">
                <label>
                    Search competitions
                    <input
                        type="search"
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                    />
                </label>
                <label>
                    Competition status
                    <select value={status} onChange={(event) => setStatus(event.target.value)}>
                        <option value="">All statuses</option>
                        {['DRAFT', 'OPEN', 'CLOSED', 'FINALIZED'].map((value) => (
                            <option key={value} value={value}>
                                {statusLabel(value)}
                            </option>
                        ))}
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading competitions…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !visible?.length ? (
                <section className="panel">No competitions match the selected filters.</section>
            ) : (
                <div className="competition-list">
                    {visible.map((item) => (
                        <article className="panel competition-card" key={item.id}>
                            <StatusBadge status={item.status} />
                            <h2>{item.name}</h2>
                            <p>Academic year: {item.academicYear}</p>
                            <p className="muted">
                                Applications: {dateTime(item.applicationStartDateUtc)} –{' '}
                                {dateTime(item.applicationEndDateUtc)}
                            </p>
                            <Link className="secondary" to={'/staff/competitions/' + item.id}>
                                Manage competition
                            </Link>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
