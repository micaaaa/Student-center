import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime } from '../lib/applications';
import type { Competition } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { CompetitionForm } from '../components/CompetitionForm';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function NewCompetitionPage() {
    const navigate = useNavigate();
    return (
        <>
            <div className="page-heading">
                <h1>Create competition</h1>
                <p className="muted">
                    New competitions are saved as drafts. Open the competition when it is ready for
                    applications.
                </p>
            </div>
            <section className="panel">
                <CompetitionForm
                    saved={(item) => navigate('/staff/competitions/' + item.id, { replace: true })}
                />
            </section>
        </>
    );
}

export function StaffCompetitionPage() {
    const { id } = useParams();
    const resource = useResource<Competition>('/api/competitions/' + id);
    if (resource.loading) return <p role="status">Loading competition…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <CompetitionRecord
            key={id}
            competition={resource.data!}
            update={resource.setData}
            reload={resource.reload}
        />
    );
}

function CompetitionRecord({
    competition,
    update,
    reload,
}: {
    competition: Competition;
    update: (value: Competition) => void;
    reload: () => void;
}) {
    const [editing, setEditing] = useState(false);
    const [confirm, setConfirm] = useState<'open' | 'close' | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    async function changeStatus() {
        if (!confirm || busy) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            update(
                await api<Competition>(`/api/competitions/${competition.id}/${confirm}`, {
                    method: 'POST',
                }),
            );
            setSuccess(confirm === 'open' ? 'Competition opened.' : 'Competition closed.');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setConfirm(null);
        }
    }
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">ACADEMIC YEAR {competition.academicYear}</span>
                <h1>{competition.name}</h1>
                <StatusBadge status={competition.status} />
            </div>
            {error && (
                <div className="notice error" role="alert">
                    <p>{error}</p>
                    <button className="secondary" onClick={reload}>
                        Reload competition
                    </button>
                </div>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            <section className="panel">
                {editing && competition.status === 'DRAFT' ? (
                    <>
                        <h2>Edit draft</h2>
                        <CompetitionForm
                            competition={competition}
                            saved={(item) => {
                                update(item);
                                setEditing(false);
                                setSuccess('Draft saved.');
                            }}
                        />
                    </>
                ) : (
                    <>
                        <h2>Competition details</h2>
                        <p className="preserve-lines">
                            {competition.description || 'No description provided.'}
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
                        <div className="button-row">
                            {competition.status === 'DRAFT' && (
                                <>
                                    <button
                                        className="secondary"
                                        disabled={busy}
                                        onClick={() => {
                                            setEditing(true);
                                            setSuccess('');
                                        }}
                                    >
                                        Edit draft
                                    </button>
                                    <button
                                        className="primary"
                                        disabled={busy}
                                        onClick={() => setConfirm('open')}
                                    >
                                        Open competition
                                    </button>
                                </>
                            )}
                            {competition.status === 'OPEN' && (
                                <button
                                    className="primary"
                                    disabled={busy}
                                    onClick={() => setConfirm('close')}
                                >
                                    Close competition
                                </button>
                            )}
                        </div>
                        <p className="muted application-section">
                            {competition.status === 'DRAFT'
                                ? 'Save all changes before opening. Details cannot be edited after opening.'
                                : competition.status === 'OPEN'
                                  ? 'Applications are accepted only within the configured dates. The competition must be closed explicitly before generating rankings.'
                                  : competition.status === 'CLOSED'
                                    ? 'Applications are closed. Continue with assessment, rankings and appeals.'
                                    : 'This competition was finalized when its final ranking was published.'}
                        </p>
                    </>
                )}
            </section>
            <div className="button-row application-section">
                <Link
                    className="secondary"
                    to={'/staff/applications?competitionId=' + competition.id}
                >
                    Review applications
                </Link>
                <Link className="secondary" to={'/staff/rankings/' + competition.id}>
                    Rankings and appeals
                </Link>
            </div>
            {confirm && (
                <ConfirmationDialog
                    title={confirm === 'open' ? 'Open competition' : 'Close competition'}
                    busy={busy}
                    onClose={() => setConfirm(null)}
                    onConfirm={changeStatus}
                >
                    <p>
                        {confirm === 'open'
                            ? 'Opening makes this competition available to students. Applications will be accepted within its configured dates, and competition details will become read-only.'
                            : 'Closing immediately stops new applications and draft submissions, even if the deadline has not passed. This competition cannot be reopened.'}
                    </p>
                    <p>
                        {competition.name} · {competition.academicYear}
                    </p>
                </ConfirmationDialog>
            )}
        </>
    );
}
