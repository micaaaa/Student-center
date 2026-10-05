import { useState } from 'react';
import { Link } from 'react-router';
import { ArrowRight, Files } from 'lucide-react';
import { useResource } from '../hooks/useResource';
import { dateTime, statusLabel } from '../lib/applications';
import type { Competition, StudentApplication } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function ApplicationsPage() {
    const resource = useResource<StudentApplication[]>('/api/applications/me');
    const competitions = useResource<Competition[]>('/api/competitions');
    const [filter, setFilter] = useState('ALL');
    const visible = (resource.data ?? [])
        .filter((item) => filter === 'ALL' || item.status === filter)
        .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc));
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">STUDENT RECORDS</span>
                    <h1>My applications</h1>
                    <p className="muted">Review draft and submitted applications.</p>
                </div>
                <Link className="secondary" to="/competitions">
                    Browse competitions <ArrowRight size={16} />
                </Link>
            </div>
            <div className="list-toolbar">
                <label>
                    Application status
                    <select value={filter} onChange={(event) => setFilter(event.target.value)}>
                        <option value="ALL">All statuses</option>
                        {[
                            'DRAFT',
                            'SUBMITTED',
                            'UNDER_REVIEW',
                            'ACCEPTED',
                            'REJECTED',
                            'WITHDRAWN',
                        ].map((status) => (
                            <option key={status} value={status}>
                                {statusLabel(status)}
                            </option>
                        ))}
                    </select>
                </label>
            </div>
            {!!competitions.error && (
                <RequestError error={competitions.error} retry={competitions.reload} />
            )}
            {resource.loading ? (
                <p role="status">Loading applications…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : visible.length === 0 ? (
                <section className="panel empty-state">
                    <Files size={30} />
                    <h2>No applications found</h2>
                    <p className="muted">
                        Browse competitions to create an application, or select another status
                        filter.
                    </p>
                </section>
            ) : (
                <div className="competition-list">
                    {visible.map((application) => (
                        <article className="panel competition-card" key={application.id}>
                            <div className="section-heading">
                                <span className="eyebrow">APPLICATION</span>
                                <StatusBadge status={application.status} />
                            </div>
                            <h2>
                                <Link to={'/applications/' + application.id}>
                                    {competitions.data?.find(
                                        (item) => item.id === application.competitionId,
                                    )?.name ?? 'Application ' + application.id.slice(0, 8)}
                                </Link>
                            </h2>
                            <dl className="details-grid">
                                <div>
                                    <dt>Created</dt>
                                    <dd>{dateTime(application.createdAtUtc)}</dd>
                                </div>
                                <div>
                                    <dt>Submitted</dt>
                                    <dd>{dateTime(application.submittedAtUtc)}</dd>
                                </div>
                            </dl>
                            <div className="competition-footer">
                                <Link className="secondary" to={'/applications/' + application.id}>
                                    View application <ArrowRight size={16} />
                                </Link>
                            </div>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
