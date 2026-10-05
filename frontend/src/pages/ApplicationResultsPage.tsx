import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime, useNow, utcDate } from '../lib/applications';
import type { StudentApplication } from '../lib/applications';
import { useOptionalResource } from '../lib/results';
import type { Appeal, ConclusionSettings, Eligibility, Ranking, Score } from '../lib/results';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function ApplicationResultsPage() {
    const { id } = useParams();
    const application = useResource<StudentApplication>('/api/applications/' + id);
    if (application.loading) return <p role="status">Loading application…</p>;
    if (application.error)
        return <RequestError error={application.error} retry={application.reload} />;
    return <Results key={id} application={application.data!} />;
}

function Results({ application }: { application: StudentApplication }) {
    const base = '/api/applications/' + application.id;
    const competitionBase = '/api/competitions/' + application.competitionId;
    const score = useOptionalResource<Score>(base + '/score', [
        'The application has not been scored yet.',
    ]);
    const eligibility = useOptionalResource<Eligibility>(competitionBase + '/eligibility/me', [
        'Accommodation eligibility was not found.',
    ]);
    return (
        <>
            <Link className="back-link" to={'/applications/' + application.id}>
                Back to application
            </Link>
            <div className="page-heading">
                <span className="eyebrow">APPLICATION RESULTS</span>
                <h1>Assessment and decision</h1>
                <p className="muted record-reference">Reference: {application.id}</p>
            </div>
            <Link
                className="secondary"
                to={'/competitions/' + application.competitionId + '/rankings'}
            >
                View published rankings
            </Link>
            <div className="application-layout application-section">
                <section className="panel">
                    <h2>Score breakdown</h2>
                    {score.loading ? (
                        <p role="status">Loading score…</p>
                    ) : score.error ? (
                        <RequestError error={score.error} retry={score.reload} />
                    ) : !score.data ? (
                        <p className="muted">This application has not been scored yet.</p>
                    ) : (
                        <>
                            {!score.data.isCurrent && (
                                <p className="notice" role="status">
                                    This score is no longer current. An updated assessment is
                                    required.
                                </p>
                            )}
                            <dl className="details-grid">
                                <div>
                                    <dt>Academic achievement</dt>
                                    <dd>{score.data.academicPoints}</dd>
                                </div>
                                <div>
                                    <dt>Household income</dt>
                                    <dd>{score.data.incomePoints}</dd>
                                </div>
                                <div>
                                    <dt>ECTS credits</dt>
                                    <dd>{score.data.ectsPoints}</dd>
                                </div>
                                <div>
                                    <dt>Study year</dt>
                                    <dd>{score.data.studyYearPoints}</dd>
                                </div>
                                <div>
                                    <dt>Additional criteria</dt>
                                    <dd>{score.data.additionalPoints}</dd>
                                </div>
                                <div>
                                    <dt>Total points</dt>
                                    <dd>
                                        <strong>{score.data.totalPoints}</strong>
                                    </dd>
                                </div>
                            </dl>
                            <p className="muted">
                                Calculated: {dateTime(score.data.calculatedAtUtc)}
                            </p>
                            <p className="muted">
                                Published rankings retain the scores recorded at publication.
                            </p>
                        </>
                    )}
                </section>
                <section className="panel">
                    <h2>Accommodation decision</h2>
                    {eligibility.loading ? (
                        <p role="status">Loading decision…</p>
                    ) : eligibility.error ? (
                        <RequestError error={eligibility.error} retry={eligibility.reload} />
                    ) : !eligibility.data ? (
                        <p className="muted">
                            No accommodation decision has been published for this application.
                        </p>
                    ) : (
                        <>
                            <strong>
                                {eligibility.data.eligible
                                    ? 'Eligible for accommodation'
                                    : 'Not eligible for accommodation'}
                            </strong>
                            <p>Academic year: {eligibility.data.academicYear}</p>
                            <p className="muted">
                                Decision date: {dateTime(eligibility.data.decisionDateUtc)}
                            </p>
                            {eligibility.data.eligible && (
                                <p className="muted">
                                    Room allocation is handled separately. This decision confirms
                                    eligibility only.
                                </p>
                            )}
                        </>
                    )}
                </section>
            </div>
            <AppealSection application={application} />
        </>
    );
}

function AppealSection({ application }: { application: StudentApplication }) {
    const competitionBase = '/api/competitions/' + application.competitionId;
    const path = '/api/applications/' + application.id + '/appeal';
    const appeal = useOptionalResource<Appeal>(path, ['Appeal was not found.']);
    const settings = useResource<ConclusionSettings>(competitionBase + '/conclusion-settings');
    const preliminary = useOptionalResource<Ranking>(competitionBase + '/rankings/preliminary', [
        'Preliminary ranking was not found.',
        'Published preliminary ranking was not found.',
    ]);
    const [reason, setReason] = useState('');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const now = useNow();
    const loading = appeal.loading || settings.loading || preliminary.loading;
    const failed = appeal.error || settings.error || preliminary.error;
    const deadline = settings.data?.appealDeadlineUtc;
    const listed = preliminary.data?.entries.some(
        (entry) => entry.isMine && entry.applicationId === application.id,
    );
    const canSubmit =
        !loading &&
        !failed &&
        appeal.absent &&
        settings.data?.status === 'CLOSED' &&
        listed &&
        !!deadline &&
        now <= utcDate(deadline).getTime();

    function reload() {
        appeal.reload();
        settings.reload();
        preliminary.reload();
    }

    async function submit(event: FormEvent) {
        event.preventDefault();
        if (!canSubmit || busy || !reason.trim()) return;
        setBusy(true);
        setError('');
        try {
            appeal.setData(
                await api<Appeal>(path, {
                    method: 'POST',
                    body: JSON.stringify({ reason: reason.trim() }),
                }),
            );
            setReason('');
        } catch (error) {
            setError(errorMessage(error));
            reload();
        } finally {
            setBusy(false);
        }
    }

    return (
        <section className="panel application-section">
            <h2>Appeal</h2>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            {appeal.data ? (
                <>
                    <StatusBadge status={appeal.data.status} />
                    <p className="muted">Submitted: {dateTime(appeal.data.submittedAtUtc)}</p>
                    <h3>Reason for appeal</h3>
                    <p className="preserve-lines">{appeal.data.reason}</p>
                    {appeal.data.response ? (
                        <>
                            <h3>Official response</h3>
                            <p className="preserve-lines">{appeal.data.response}</p>
                            <p className="muted">
                                Resolved:{' '}
                                {appeal.data.resolvedAtUtc
                                    ? dateTime(appeal.data.resolvedAtUtc)
                                    : 'Pending'}
                            </p>
                        </>
                    ) : (
                        <p className="muted">An official response has not been issued yet.</p>
                    )}
                    <p className="muted">
                        The appeal outcome does not itself confirm accommodation eligibility. Refer
                        to the final decision.
                    </p>
                    <button className="secondary" onClick={reload} disabled={loading}>
                        Refresh appeal status
                    </button>
                </>
            ) : loading ? (
                <p role="status">Checking appeal availability…</p>
            ) : failed ? (
                <RequestError error={failed} retry={reload} />
            ) : (
                <>
                    <p className="muted">
                        Appeal deadline: {deadline ? dateTime(deadline) : 'Not announced'}
                    </p>
                    {canSubmit ? (
                        <form onSubmit={submit}>
                            <p>
                                One appeal may be submitted per application. Submitted appeals
                                cannot be edited.
                            </p>
                            <label>
                                Reason for appeal
                                <textarea
                                    required
                                    maxLength={4000}
                                    rows={6}
                                    value={reason}
                                    onChange={(event) => setReason(event.target.value)}
                                    disabled={busy}
                                />
                            </label>
                            <p className="muted">{reason.length} / 4000 characters</p>
                            <button className="primary" disabled={busy || !reason.trim()}>
                                {busy ? 'Submitting…' : 'Submit appeal'}
                            </button>
                        </form>
                    ) : (
                        <p className="muted">
                            {settings.data?.status === 'FINALIZED'
                                ? 'This competition has been finalized.'
                                : deadline && now > utcDate(deadline).getTime()
                                  ? 'The appeal deadline has passed.'
                                  : preliminary.data && !listed
                                    ? 'Only applications on the preliminary ranking can be appealed.'
                                    : 'Appeals are not currently open for this application.'}
                        </p>
                    )}
                </>
            )}
        </section>
    );
}
