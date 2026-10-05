import { useState } from 'react';
import { Link } from 'react-router';
import { ArrowRight, CalendarDays, Search } from 'lucide-react';
import { useResource } from '../hooks/useResource';
import { acceptsApplications, dateTime, useNow } from '../lib/applications';
import type { Competition } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function CompetitionsPage() {
    const resource = useResource<Competition[]>('/api/competitions');
    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState('open');
    const now = useNow();
    const visible = (resource.data ?? [])
        .filter((item) => item.status !== 'DRAFT')
        .filter((item) => filter === 'all' || acceptsApplications(item, now))
        .filter((item) =>
            (item.name + ' ' + item.academicYear).toLowerCase().includes(search.toLowerCase()),
        )
        .sort((a, b) => b.applicationStartDateUtc.localeCompare(a.applicationStartDateUtc));

    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">ACCOMMODATION APPLICATIONS</span>
                <h1>Competitions</h1>
                <p className="muted">Review competition details and application deadlines.</p>
            </div>
            <div className="list-toolbar">
                <label className="search-field">
                    <span>Search competitions</span>
                    <div>
                        <Search size={18} />
                        <input
                            type="search"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                            placeholder="Competition name or academic year"
                        />
                    </div>
                </label>
                <label>
                    Availability
                    <select value={filter} onChange={(event) => setFilter(event.target.value)}>
                        <option value="open">Accepting applications</option>
                        <option value="all">All published competitions</option>
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading competitions…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : visible.length === 0 ? (
                <section className="panel empty-state">
                    <CalendarDays size={30} />
                    <h2>No competitions found</h2>
                    <p className="muted">No competitions match the selected filters.</p>
                </section>
            ) : (
                <div className="competition-list">
                    {visible.map((competition) => (
                        <article className="panel competition-card" key={competition.id}>
                            <div className="section-heading">
                                <span className="eyebrow">
                                    ACADEMIC YEAR {competition.academicYear}
                                </span>
                                <StatusBadge status={competition.status} />
                            </div>
                            <h2>
                                <Link to={'/competitions/' + competition.id}>
                                    {competition.name}
                                </Link>
                            </h2>
                            <p className="muted description-preview">
                                {competition.description || 'No additional description provided.'}
                            </p>
                            <div className="competition-footer">
                                <div>
                                    <small>APPLICATION DEADLINE</small>
                                    <strong>{dateTime(competition.applicationEndDateUtc)}</strong>
                                </div>
                                <Link className="secondary" to={'/competitions/' + competition.id}>
                                    View details <ArrowRight size={16} />
                                </Link>
                            </div>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
