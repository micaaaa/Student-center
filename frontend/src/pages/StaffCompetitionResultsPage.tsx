import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime, useNow, utcDate } from '../lib/applications';
import type { Competition } from '../lib/applications';
import { useOptionalResource } from '../lib/results';
import type { ConclusionSettings } from '../lib/results';
import type { StaffAppeal, StaffRanking } from '../lib/staffRankings';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { StaffRankingTable } from '../components/StaffRankingTable';
import { ConclusionSettingsForm } from '../components/ConclusionSettingsForm';
import { StaffAppealCard } from '../components/StaffAppealCard';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function StaffCompetitionResultsPage() {
    const { id } = useParams();
    const competition = useResource<Competition>('/api/competitions/' + id);
    if (competition.loading) return <p role="status">Loading competition…</p>;
    if (competition.error)
        return <RequestError error={competition.error} retry={competition.reload} />;
    return <CompetitionResults key={id} competition={competition.data!} />;
}

function CompetitionResults({ competition }: { competition: Competition }) {
    const base = '/api/staff/competitions/' + competition.id;
    const preliminary = useOptionalResource<StaffRanking>(base + '/rankings/preliminary', [
        'Preliminary ranking was not found.',
    ]);
    const final = useOptionalResource<StaffRanking>(base + '/rankings/final', [
        'Final ranking was not found.',
    ]);
    const settings = useResource<ConclusionSettings>(
        '/api/competitions/' + competition.id + '/conclusion-settings',
    );
    const appeals = useResource<StaffAppeal[]>(base + '/appeals');
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [publish, setPublish] = useState<'preliminary' | 'final' | null>(null);
    const [finalInvalidated, setFinalInvalidated] = useState(false);
    const now = useNow();
    const closed = settings.data?.status === 'CLOSED' && !settings.loading && !settings.error;
    const preliminaryPublished =
        preliminary.data?.status === 'PUBLISHED' && !preliminary.loading && !preliminary.error;
    const pendingAppeals = appeals.data?.filter((item) =>
        ['SUBMITTED', 'UNDER_REVIEW'].includes(item.status),
    ).length;
    const finalReady =
        closed &&
        preliminaryPublished &&
        !appeals.loading &&
        !appeals.error &&
        pendingAppeals === 0 &&
        settings.data?.availablePlaces != null &&
        !!settings.data.appealDeadlineUtc &&
        now > utcDate(settings.data.appealDeadlineUtc).getTime();
    const preliminaryBlock = busy
        ? 'An operation is in progress. Please wait.'
        : settings.loading
          ? 'Checking competition status…'
          : settings.error
            ? 'Competition settings could not be loaded. Retry below.'
            : !closed
              ? `The competition is ${settings.data?.status.toLowerCase() || 'unavailable'}. Ranking generation requires a closed competition.`
              : '';
    const finalBlock =
        preliminaryBlock ||
        (preliminary.loading
            ? 'Checking preliminary ranking…'
            : preliminary.error
              ? 'The preliminary ranking could not be loaded. Retry above.'
              : !preliminaryPublished
                ? 'Publish the preliminary ranking first.'
                : settings.data?.availablePlaces == null || !settings.data.appealDeadlineUtc
                  ? 'Save the capacity and appeal deadline first.'
                  : now <= utcDate(settings.data.appealDeadlineUtc).getTime()
                    ? `The appeal period is still open. Generation becomes available after ${dateTime(settings.data.appealDeadlineUtc)}.`
                    : appeals.loading
                      ? 'Checking appeal decisions…'
                      : appeals.error
                        ? 'Appeals could not be loaded. Retry above.'
                        : pendingAppeals !== 0
                          ? `Resolve all appeals first. Unresolved appeals: ${pendingAppeals}.`
                          : '');

    function reload() {
        preliminary.reload();
        final.reload();
        settings.reload();
        appeals.reload();
    }
    async function action(work: () => Promise<void>, message: string) {
        if (busy) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            await work();
            setSuccess(message);
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    async function publishRanking() {
        if (!publish || busy) return;
        const kind = publish;
        if (
            (kind === 'preliminary' && (!closed || preliminary.data?.status !== 'DRAFT')) ||
            (kind === 'final' &&
                (!finalReady || final.data?.status !== 'DRAFT' || finalInvalidated))
        ) {
            setPublish(null);
            setError('The competition state changed. Refresh the results before publishing.');
            return;
        }
        await action(async () => {
            const saved = await api<StaffRanking>(base + '/rankings/' + kind + '/publish', {
                method: 'POST',
            });
            if (kind === 'preliminary') preliminary.setData(saved);
            else {
                final.setData(saved);
                settings.reload();
            }
        }, 'Ranking published.');
        setPublish(null);
    }
    return (
        <>
            <Link className="back-link" to="/staff/rankings">
                Back to rankings and appeals
            </Link>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">ACADEMIC YEAR {competition.academicYear}</span>
                    <h1>{competition.name}</h1>
                    <StatusBadge status={settings.data?.status || competition.status} />
                </div>
                <button className="secondary" disabled={busy} onClick={reload}>
                    Refresh results
                </button>
            </div>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            <Link className="text-link" to={'/staff/applications?competitionId=' + competition.id}>
                Review competition applications
            </Link>
            <section className="panel application-section">
                <h2>Preliminary ranking</h2>
                {preliminary.loading ? (
                    <p role="status">Loading ranking…</p>
                ) : preliminary.error ? (
                    <RequestError error={preliminary.error} retry={preliminary.reload} />
                ) : (
                    <>
                        {preliminary.data ? (
                            <StaffRankingTable ranking={preliminary.data} />
                        ) : (
                            <p>No preliminary ranking has been generated.</p>
                        )}
                        {!preliminaryPublished && (
                            <>
                                <p className="muted">
                                    Close the competition and complete document review and scoring
                                    for every active application before generating. Equal scores
                                    will share a position.
                                </p>
                                <div className="button-row">
                                    <button
                                        className="primary"
                                        aria-describedby="preliminary-generation-status"
                                        disabled={busy || !closed}
                                        onClick={() =>
                                            action(
                                                async () =>
                                                    preliminary.setData(
                                                        await api<StaffRanking>(
                                                            base + '/rankings/preliminary',
                                                            {
                                                                method: 'PUT',
                                                                body: JSON.stringify({
                                                                    tieRule: 1,
                                                                }),
                                                            },
                                                        ),
                                                    ),
                                                'Preliminary draft generated.',
                                            )
                                        }
                                    >
                                        {preliminary.data
                                            ? 'Regenerate preliminary draft'
                                            : 'Generate preliminary draft'}
                                    </button>
                                    {preliminary.data?.status === 'DRAFT' && (
                                        <button
                                            className="secondary"
                                            disabled={busy || !closed}
                                            onClick={() => setPublish('preliminary')}
                                        >
                                            Publish preliminary ranking
                                        </button>
                                    )}
                                </div>
                                {preliminaryBlock && (
                                    <p
                                        id="preliminary-generation-status"
                                        className="notice"
                                        role="status"
                                    >
                                        {preliminaryBlock}
                                    </p>
                                )}
                            </>
                        )}
                    </>
                )}
            </section>
            <section className="panel application-section">
                <h2>Capacity and appeal deadline</h2>
                {settings.loading ? (
                    <p role="status">Loading settings…</p>
                ) : settings.error ? (
                    <RequestError error={settings.error} retry={settings.reload} />
                ) : (
                    <ConclusionSettingsForm
                        key={
                            String(settings.data!.availablePlaces) +
                            settings.data!.appealDeadlineUtc
                        }
                        settings={settings.data!}
                        disabled={busy || !closed || !preliminaryPublished}
                        save={(body) =>
                            action(async () => {
                                settings.setData(
                                    await api<ConclusionSettings>(base + '/conclusion-settings', {
                                        method: 'PUT',
                                        body: JSON.stringify(body),
                                    }),
                                );
                                setFinalInvalidated(true);
                            }, 'Conclusion settings saved. Regenerate any existing final draft.')
                        }
                    />
                )}
                {!preliminaryPublished && (
                    <p className="muted">
                        Publish the preliminary ranking before configuring the appeal period.
                    </p>
                )}
            </section>
            <section className="panel application-section">
                <h2>Appeals</h2>
                {appeals.loading ? (
                    <p role="status">Loading appeals…</p>
                ) : appeals.error ? (
                    <RequestError error={appeals.error} retry={appeals.reload} />
                ) : !appeals.data?.length ? (
                    <p>No appeals have been submitted.</p>
                ) : (
                    <>
                        <p>Unresolved appeals: {pendingAppeals}</p>
                        {appeals.data.map((appeal) => (
                            <StaffAppealCard
                                key={appeal.id + appeal.status}
                                appeal={appeal}
                                disabled={busy || !closed}
                                start={() =>
                                    action(async () => {
                                        const saved = await api<StaffAppeal>(
                                            '/api/staff/appeals/' + appeal.id + '/start-review',
                                            { method: 'POST' },
                                        );
                                        appeals.setData(
                                            appeals.data!.map((item) =>
                                                item.id === saved.id ? saved : item,
                                            ),
                                        );
                                    }, 'Appeal review started.')
                                }
                                resolve={(accepted, response) =>
                                    action(async () => {
                                        const saved = await api<StaffAppeal>(
                                            '/api/staff/appeals/' + appeal.id + '/resolution',
                                            {
                                                method: 'PUT',
                                                body: JSON.stringify({ accepted, response }),
                                            },
                                        );
                                        appeals.setData(
                                            appeals.data!.map((item) =>
                                                item.id === saved.id ? saved : item,
                                            ),
                                        );
                                        setFinalInvalidated(true);
                                    }, 'Appeal decision saved.')
                                }
                            />
                        ))}
                    </>
                )}
            </section>
            <section className="panel application-section">
                <h2>Final ranking</h2>
                {final.loading ? (
                    <p role="status">Loading final ranking…</p>
                ) : final.error ? (
                    <RequestError error={final.error} retry={final.reload} />
                ) : (
                    <>
                        {final.data ? (
                            <StaffRankingTable ranking={final.data} final />
                        ) : (
                            <p>No final ranking has been generated.</p>
                        )}
                        {final.data?.status !== 'PUBLISHED' && (
                            <>
                                <p className="muted">
                                    Final ranking requires an expired appeal deadline, resolved
                                    appeals and current scores. If a tied group crosses the capacity
                                    boundary, adjust the number of places to include or exclude the
                                    whole group.
                                </p>
                                {finalInvalidated && final.data && (
                                    <p className="notice">
                                        Settings or appeal decisions changed. Regenerate the final
                                        draft before publishing.
                                    </p>
                                )}
                                <div className="button-row">
                                    <button
                                        className="primary"
                                        disabled={busy || !finalReady}
                                        aria-describedby="final-generation-status"
                                        onClick={() =>
                                            action(async () => {
                                                final.setData(
                                                    await api<StaffRanking>(
                                                        base + '/rankings/final',
                                                        { method: 'PUT' },
                                                    ),
                                                );
                                                setFinalInvalidated(false);
                                            }, 'Final draft generated.')
                                        }
                                    >
                                        {final.data
                                            ? 'Regenerate final draft'
                                            : 'Generate final draft'}
                                    </button>
                                    {final.data?.status === 'DRAFT' && (
                                        <button
                                            className="secondary"
                                            disabled={busy || !finalReady || finalInvalidated}
                                            onClick={() => setPublish('final')}
                                        >
                                            Publish final ranking
                                        </button>
                                    )}
                                </div>
                                {finalBlock && (
                                    <p
                                        id="final-generation-status"
                                        className="notice"
                                        role="status"
                                    >
                                        {finalBlock}
                                    </p>
                                )}
                            </>
                        )}
                    </>
                )}
            </section>
            {publish && (
                <ConfirmationDialog
                    title={
                        publish === 'final'
                            ? 'Publish final ranking'
                            : 'Publish preliminary ranking'
                    }
                    busy={busy}
                    onClose={() => setPublish(null)}
                    onConfirm={publishRanking}
                >
                    <p>
                        {publish === 'final'
                            ? 'This will finalize the competition and issue accommodation eligibility decisions for the ranked applications.'
                            : 'This ranking will become visible to students and cannot be regenerated after publication.'}
                    </p>
                    <p>Review the displayed ranking before confirming.</p>
                </ConfirmationDialog>
            )}
        </>
    );
}
