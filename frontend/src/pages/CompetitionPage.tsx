import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { ArrowLeft, FilePlus2 } from 'lucide-react';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { acceptsApplications, dateTime, useNow } from '../lib/applications';
import type { Competition, StudentApplication } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function CompetitionPage() {
    const { id } = useParams();
    const resource = useResource<Competition>('/api/competitions/' + id);
    const mine = useResource<StudentApplication[]>('/api/applications/me');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const now = useNow();
    const existing = mine.data?.find((application) => application.competitionId === id);

    async function create() {
        setBusy(true);
        setError('');
        try {
            const application = await api<StudentApplication>('/api/applications', {
                method: 'POST',
                body: JSON.stringify({ competitionId: id }),
            });
            navigate('/applications/' + application.id);
        } catch (error) {
            setError(errorMessage(error));
            mine.reload();
        } finally {
            setBusy(false);
        }
    }

    if (resource.loading) return <p role="status">Loading competition…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    const competition = resource.data!;
    if (competition.status === 'DRAFT')
        return (
            <section className="panel">
                <h1>Competition unavailable</h1>
                <Link to="/competitions">Return to competitions</Link>
            </section>
        );
    return (
        <>
            <Link className="back-link" to="/competitions">
                <ArrowLeft size={16} />
                Competitions
            </Link>
            <div className="page-heading">
                <span className="eyebrow">ACADEMIC YEAR {competition.academicYear}</span>
                <h1>{competition.name}</h1>
                <StatusBadge status={competition.status} />
                <div className="button-row application-section">
                    <Link
                        className="secondary"
                        to={'/competitions/' + competition.id + '/rankings'}
                    >
                        View published rankings
                    </Link>
                </div>
            </div>
            <div className="application-layout">
                <section className="panel">
                    <h2>Competition details</h2>
                    <p className="preserve-lines">
                        {competition.description || 'No additional description provided.'}
                    </p>
                    <dl className="details-grid">
                        <div>
                            <dt>Applications open</dt>
                            <dd>{dateTime(competition.applicationStartDateUtc)}</dd>
                        </div>
                        <div>
                            <dt>Application deadline</dt>
                            <dd>{dateTime(competition.applicationEndDateUtc)}</dd>
                        </div>
                    </dl>
                </section>
                <section className="panel">
                    <h2>Your application</h2>
                    {mine.loading ? (
                        <p role="status">Checking your applications…</p>
                    ) : mine.error ? (
                        <RequestError error={mine.error} retry={mine.reload} />
                    ) : existing ? (
                        <>
                            <StatusBadge status={existing.status} />
                            <p className="muted">
                                An application has already been created for this competition.
                            </p>
                            <Link className="primary" to={'/applications/' + existing.id}>
                                View application
                            </Link>
                        </>
                    ) : acceptsApplications(competition, now) ? (
                        <>
                            <p className="muted">
                                Create a draft, add supporting documents and submit it before the
                                deadline.
                            </p>
                            <button className="primary" disabled={busy} onClick={create}>
                                <FilePlus2 size={17} />
                                {busy ? 'Creating…' : 'Create application'}
                            </button>
                        </>
                    ) : (
                        <p className="muted">
                            This competition is not currently accepting applications.
                        </p>
                    )}
                    {error && (
                        <p className="notice error" role="alert">
                            {error}
                        </p>
                    )}
                </section>
            </div>
        </>
    );
}
