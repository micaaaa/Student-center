import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Competition } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function StaffRankingsPage() {
    const competitions = useResource<Competition[]>('/api/competitions');
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">COMPETITION ADMINISTRATION</span>
                <h1>Rankings and appeals</h1>
                <p className="muted">
                    Select a competition to manage published results and appeal decisions.
                </p>
            </div>
            {competitions.loading ? (
                <p role="status">Loading competitions…</p>
            ) : competitions.error ? (
                <RequestError error={competitions.error} retry={competitions.reload} />
            ) : !competitions.data?.length ? (
                <section className="panel">No competitions found.</section>
            ) : (
                <div className="competition-list">
                    {competitions.data.map((item) => (
                        <article className="panel competition-card" key={item.id}>
                            <StatusBadge status={item.status} />
                            <h2>{item.name}</h2>
                            <p>Academic year: {item.academicYear}</p>
                            <Link className="secondary" to={'/staff/rankings/' + item.id}>
                                Manage results
                            </Link>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
