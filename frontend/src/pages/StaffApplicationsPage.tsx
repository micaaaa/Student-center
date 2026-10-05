import { Link, useSearchParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { dateTime } from '../lib/applications';
import type { Competition, StudentApplication } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

const statuses = [
    ['2', 'Submitted'],
    ['3', 'Under review'],
    ['4', 'Accepted'],
    ['5', 'Rejected'],
    ['6', 'Withdrawn'],
];

export function StaffApplicationsPage() {
    const [search, setSearch] = useSearchParams();
    const competitionId = search.get('competitionId') || '';
    const status = statuses.some(([value]) => value === search.get('status'))
        ? search.get('status')!
        : '';
    const requestedPage = Number(search.get('page') || 1);
    const page =
        Number.isInteger(requestedPage) && requestedPage > 0 && requestedPage <= 42949672
            ? requestedPage
            : 1;
    const query = new URLSearchParams({ page: String(page) });
    if (competitionId) query.set('competitionId', competitionId);
    if (status) query.set('status', status);
    const applications = useResource<StudentApplication[]>('/api/staff/applications?' + query);
    const competitions = useResource<Competition[]>('/api/competitions');

    function filter(key: string, value: string) {
        const next = new URLSearchParams(query);
        if (value) next.set(key, value);
        else next.delete(key);
        next.set('page', '1');
        setSearch(next);
    }

    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">APPLICATION ADMINISTRATION</span>
                <h1>Application review</h1>
                <p className="muted">
                    Review submitted applications, supporting documents and assessment points.
                </p>
            </div>
            <div className="list-toolbar">
                <label>
                    Competition
                    <select
                        value={competitionId}
                        onChange={(event) => filter('competitionId', event.target.value)}
                        disabled={competitions.loading || !!competitions.error}
                    >
                        <option value="">All competitions</option>
                        {competitions.data?.map((item) => (
                            <option key={item.id} value={item.id}>
                                {item.name} — {item.academicYear}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Application status
                    <select
                        value={status}
                        onChange={(event) => filter('status', event.target.value)}
                    >
                        <option value="">All submitted statuses</option>
                        {statuses.map(([value, label]) => (
                            <option key={value} value={value}>
                                {label}
                            </option>
                        ))}
                    </select>
                </label>
            </div>
            {!!competitions.error && (
                <RequestError error={competitions.error} retry={competitions.reload} />
            )}
            {applications.loading ? (
                <p role="status">Loading applications…</p>
            ) : applications.error ? (
                <RequestError error={applications.error} retry={applications.reload} />
            ) : (
                <>
                    {!applications.data?.length ? (
                        <section className="panel">
                            <h2>No applications found</h2>
                            <p>Choose another filter or return to the previous page.</p>
                        </section>
                    ) : (
                        <div className="competition-list">
                            {applications.data.map((application) => (
                                <article className="panel competition-card" key={application.id}>
                                    <StatusBadge status={application.status} />
                                    <h2>
                                        {competitions.data?.find(
                                            (item) => item.id === application.competitionId,
                                        )?.name || 'Application'}
                                    </h2>
                                    <p className="record-reference">
                                        Application: {application.id}
                                    </p>
                                    <p className="record-reference muted">
                                        Student reference: {application.studentId}
                                    </p>
                                    <p>Submitted: {dateTime(application.submittedAtUtc)}</p>
                                    <Link
                                        className="secondary"
                                        to={'/staff/applications/' + application.id}
                                        state={{ returnTo: '/staff/applications?' + query }}
                                    >
                                        Review application
                                    </Link>
                                </article>
                            ))}
                        </div>
                    )}
                </>
            )}
            <div className="button-row application-section" aria-label="Application pages">
                <button
                    className="secondary"
                    disabled={page === 1 || applications.loading}
                    onClick={() => {
                        query.set('page', String(page - 1));
                        setSearch(query);
                    }}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        applications.loading ||
                        !!applications.error ||
                        applications.data?.length !== 50 ||
                        page >= 42949672
                    }
                    onClick={() => {
                        query.set('page', String(page + 1));
                        setSearch(query);
                    }}
                >
                    Next
                </button>
            </div>
        </>
    );
}
